const express = require("express");
const puppeteer = require("puppeteer");
const cors = require("cors");

const app = express();
const PORT = Number(process.env.PORT) || 3000;

// Enable CORS for all routes
app.use(cors());
app.use(express.json());

// Store the current match data
let matchData = {
  team1: "Team A",
  team2: "Team B",
  score1: 0,
  score2: 0,
  kills: [],         // structured events
  killFeed: [],      // plain-text messages
  isLive: false,
  matchStatus: "waiting", // waiting, live, paused, finished
  winner: "",
  currentRound: "",
  mapName: "",
  timeLeft: "",
  lastUpdate: new Date().toISOString(),
};

// Puppeteer browser and page instances
let browser = null;
let page = null;
let monitorTimer = null;

// Configuration
const CONFIG = {
  targetUrl: process.env.MATCH_URL || null, // dynamic; set via /start or /url
  updateInterval: 2000,
  headless: true,

  // CSS selectors for HLTV scoreboard structure (can be updated via /selectors)
  selectors: {
    // Team names
    team1Name: ".ctTeamHeaderBg .teamName, [data-team1-name]",
    team2Name: ".tTeamHeaderBg .teamName, [data-team2-name]",

    // Scores
    score1: ".ctScore, .topbarBg .score .ctScore",
    score2: ".tScore, .topbarBg .score .tScore",

    // Scoreboard root (has data-* attrs)
    scoreboardElement: "#scoreboardElement",

    // Kill feed (game log)
    killFeed: ".gamelog .playerKill, .gamelog .gamelogBox",

    // Round / map / time
    currentRound: ".currentRoundText, .round .currentRoundText",
    mapName: ".roundText, .round .roundText",
    timeLeft: ".timeText span, .time .timeText span",

    // Live indicator
    liveIndicator: '.live-indicator, .match-status.live, [data-live="true"]',

    // Optional: win-o-meter root (kept fixed)
    winOMeter: ".win-o-meter",

    // Optional: team tables root (kept fixed)
    teamTables: ".scoreboard .team",
  },
};

// Fetch live matches list from HLTV
async function fetchLiveMatches() {
  const MATCHES_URL = "https://www.hltv.org/matches";
  const UA =
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

  let localBrowser = browser;
  let tempBrowser = null;
  let tempPage = null;
  try {
    // Launch a temporary browser if the main one isn't running
    if (!localBrowser) {
      tempBrowser = await puppeteer.launch({
        headless: CONFIG.headless,
        args: [
          "--no-sandbox",
          "--disable-setuid-sandbox",
          "--disable-dev-shm-usage",
          "--disable-web-security",
          "--disable-features=VizDisplayCompositor",
          `--user-agent=${UA}`,
        ],
      });
      localBrowser = tempBrowser;
    }

    tempPage = await localBrowser.newPage();
    await tempPage.setViewport({ width: 1366, height: 768 });
    await tempPage.setUserAgent(UA);
    await tempPage.goto(MATCHES_URL, { waitUntil: "domcontentloaded", timeout: 30000 });

    // Extract LIVE matches based on the current HLTV DOM structure provided
    const matches = await tempPage.evaluate(() => {
      const out = [];

      const wrappers = Array.from(
        document.querySelectorAll(
          ".liveMatchesSection .match-wrapper.live-match-container"
        )
      );

      for (const w of wrappers) {
        // Only include live matches
        const liveAttr = w.getAttribute("live");
        const hasLiveMeta = !!w.querySelector(".match-meta-live");
        const isLive = liveAttr === "true" || hasLiveMeta;
        if (!isLive) continue;

        const a = w.querySelector('a[href^="/matches/"]');
        const href = a ? a.getAttribute("href") || "" : "";
        if (!href.startsWith("/matches/")) continue;

        const id = Number.parseInt(w.getAttribute("data-match-id") || "0", 10);
        const stars = Number.parseInt(w.getAttribute("data-stars") || "0", 10);

        const eventEl = w.querySelector(".match-event");
        const eventHeadline =
          (eventEl && eventEl.getAttribute("data-event-headline")) || "";
        const eventText =
          (eventEl && (eventEl.querySelector(".text-ellipsis")?.textContent || "").trim()) || "";
        const eventName = eventHeadline || eventText;

        const teamNames = Array.from(
          w.querySelectorAll(".match-teamname")
        ).map((n) => (n.textContent || "").trim()).filter(Boolean);

        // BO label (e.g., "bo3")
        const metaTexts = Array.from(w.querySelectorAll(".match-info .match-meta"))
          .map((n) => (n.textContent || "").trim());
        const bo = metaTexts.find((t) => /bo\d/i.test(t)) || "";

        // Current map score if visible
        const scoreSpans = Array.from(
          w.querySelectorAll(".match-team-livescore [data-livescore-current-map-score]")
        );
        const scoreVals = scoreSpans.map((s) => (s.textContent || "").trim());
        const score = scoreVals.length >= 2 ? `${scoreVals[0]}:${scoreVals[1]}` : "";

        // Build absolute URL
        const url = new URL(href, location.origin).toString();
        const m = href.split("/").filter(Boolean);
        const slug = m.length >= 3 ? m[2] : "";

        out.push({
          id,
          slug,
          url,
          status: "LIVE",
          live: true,
          teams: teamNames.slice(0, 2),
          event: eventName,
          bo,
          score,
          time: "",
          stars,
        });
      }
      return out;
    });

    return matches;
  } finally {
    try { if (tempPage) await tempPage.close(); } catch {}
    try { if (tempBrowser) await tempBrowser.close(); } catch {}
  }
}

// ---------- utils ----------
function resolveMatchUrl(input) {
  if (!input || typeof input !== "string") return null;
  try {
    const u = new URL(input.trim());
    // Only allow HLTV match pages for safety
    if (!u.hostname.endsWith("hltv.org")) return null;
    if (!/^\/matches\//.test(u.pathname)) return null;
    return u.toString();
  } catch {
    return null;
  }
}

async function initializeBrowser(url) {
  try {
    const matchUrl = resolveMatchUrl(url || CONFIG.targetUrl);
    if (!matchUrl) throw new Error("A valid HLTV match URL is required");

    console.log("🚀 Starting Puppeteer browser...");
    browser = await puppeteer.launch({
      headless: CONFIG.headless,
      args: [
        "--no-sandbox",
        "--disable-setuid-sandbox",
        "--disable-dev-shm-usage",
        "--disable-web-security",
        "--disable-features=VizDisplayCompositor",
        "--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
      ],
    });

    page = await browser.newPage();
    await page.setViewport({ width: 1920, height: 1080 });

    console.log(`📍 Navigating to: ${matchUrl}`);
    await page.goto(matchUrl, { waitUntil: "networkidle2", timeout: 30000 });

    // Wait best-effort for scoreboard element (don’t hard-fail)
    try {
      await page.waitForSelector(CONFIG.selectors.scoreboardElement, {
        timeout: 15000,
      });
    } catch {
      console.warn("⚠️ scoreboardElement not found yet, scraper will retry...");
    }

    CONFIG.targetUrl = matchUrl;
    console.log("✅ Browser initialized successfully");
    startMonitoring(); // (re)start loop
  } catch (error) {
    console.error("❌ Failed to initialize browser:", error.message);
  }
}

function startMonitoring() {
  console.log("👁️  Starting page monitoring...");
  if (monitorTimer) clearInterval(monitorTimer);
  monitorTimer = setInterval(async () => {
    try {
      await scrapeMatchData();
    } catch (error) {
      console.error("❌ Error during scraping:", error.message);
    }
  }, CONFIG.updateInterval);
}

async function stopMonitoring() {
  if (monitorTimer) {
    clearInterval(monitorTimer);
    monitorTimer = null;
  }
}

// ---------- scraper ----------
async function scrapeMatchData() {
  if (!page) return;

  try {
    const newData = await page.evaluate((selectors) => {
      // ---------- helpers ----------
      const q = (sel, root = document) => root.querySelector(sel);
      const qa = (sel, root = document) => Array.from(root.querySelectorAll(sel));
      const txt = (sel, root = document) => (q(sel, root)?.textContent || "").trim();
      const toInt = (s) => {
        const m = (s || "").replace(/[^\d\-]/g, "");
        const n = parseInt(m, 10);
        return Number.isFinite(n) ? n : 0;
      };
      const toFloat = (s) => {
        const m = (s || "").replace(/[^\d.\-]/g, "");
        const n = parseFloat(m);
        return Number.isFinite(n) ? n : 0;
      };
      const weaponFromSrc = (src) => {
        if (!src) return "unknown";
        const last = src.split("/").pop() || "";
        return last.replace(".png", "").replace(".svg", "").replace(/_/g, " ");
      };

      // ---------- root / dataset ----------
      const sb = q(selectors.scoreboardElement);
      const ds = (sb && sb.dataset) || {};

      // team basics (prefer dataset, fallback DOM)
      let team1 = ds.team1Name || txt(selectors.team1Name) || "Team 1";
      let team2 = ds.team2Name || txt(selectors.team2Name) || "Team 2";
      const team1Id = ds.team1Id ? toInt(ds.team1Id) : 0;
      const team2Id = ds.team2Id ? toInt(ds.team2Id) : 0;
      const team1Logo = ds.team1Logo || "";
      const team2Logo = ds.team2Logo || "";

      // score / timer / map / round
      const score1 = toInt(txt(selectors.score1));
      const score2 = toInt(txt(selectors.score2));
      const rawRound = txt(selectors.currentRound); // e.g. "6 - inferno"
      const rawRoundLbl = txt(selectors.mapName);   // e.g. "R: 6 - inferno"
      const timeLeft = txt(selectors.timeLeft);

      let currentRound = "";
      let mapName = "";
      if (rawRoundLbl) {
        const mR = rawRoundLbl.match(/R:\s*(\d+)/);
        currentRound = mR ? mR[1] : rawRound || "";
        const mMap = rawRoundLbl.match(/-\s*([a-zA-Z_]+)/);
        mapName = mMap ? mMap[1] : "";
      }
      if (!mapName && rawRound) {
        const mMap2 = rawRound.match(/-\s*([a-zA-Z_]+)/);
        mapName = mMap2 ? mMap2[1] : rawRound.replace(/R:|\d+|-|\s+/g, "").trim();
      }

      // ---------- win probability ----------
      let winProbability = { team1: 0, team2: 0, ot: 0 };
      const meter = q(selectors.winOMeter || ".win-o-meter");
      if (meter) {
        const wTeams = qa(".win-o-meter-team", meter);
        let pA = 0, pB = 0, logoA = "", logoB = "";
        if (wTeams[0]) {
          pA = toFloat(txt("span", wTeams[0]));
          logoA = q("img", wTeams[0])?.getAttribute("src") || "";
        }
        if (wTeams[1]) {
          pB = toFloat(txt("span", wTeams[1]));
          logoB = q("img", wTeams[1])?.getAttribute("src") || "";
        }
        const otPct = toFloat(txt(".win-o-meter-line.right.ot .team-offset:last-child", meter));

        const matchLogo = (needle, hay) =>
          needle && hay && hay.includes((needle.split("?")[0].split("/").pop() || ""));

        let t1p = pA, t2p = pB;
        if ((ds.team1Logo || ds.team2Logo) && (logoA || logoB)) {
          const t1Logo = ds.team1Logo || "";
          const aIsT1 = matchLogo(t1Logo, logoA);
          const bIsT1 = matchLogo(t1Logo, logoB);
          if (aIsT1) { t1p = pA; t2p = pB; }
          else if (bIsT1) { t1p = pB; t2p = pA; }
        }
        winProbability = { team1: t1p, team2: t2p, ot: otPct };
      }

      // ---------- players / scoreboard ----------
      const parsePlayerRow = (tr) => {
        const name = txt(".nameCell", tr);
        const weapon = weaponFromSrc(q(".weaponCell img", tr)?.getAttribute("src") || "");
        const hp = toInt(txt(".hp-text", tr));
        let armor = "none";
        const armorImg = q(".armorCell img", tr)?.getAttribute("src") || "";
        if (armorImg.includes("kevlar_helmet")) armor = "helmet";
        else if (armorImg.includes("kevlar.png")) armor = "kevlar";

        const money = toInt(txt(".moneyCell", tr));
        const kills = toInt(txt(".killCell", tr));
        const assists = toInt(txt(".assistCell", tr));
        const deaths = toInt(txt(".deathCell", tr));
        const adr = toFloat(txt(".adrCell", tr));
        return { name, weapon, hp, armor, money, kills, assists, deaths, adr };
      };

      const teamTables = qa(selectors.teamTables || ".scoreboard .team");
      const playersTeam1 = teamTables[0] ? qa("tbody tr.player", teamTables[0]).map(parsePlayerRow) : [];
      const playersTeam2 = teamTables[1] ? qa("tbody tr.player", teamTables[1]).map(parsePlayerRow) : [];

      // ---------- killfeed ----------
      const killEls = qa(selectors.killFeed);
      const parseKill = (el) => {
        const nameSpans = qa(".ctplayer, .tplayer", el).map((s) => ({
          name: (s.textContent || "").trim(),
          side: s.classList.contains("ctplayer") ? "CT" : "T",
        }));
        const killer = nameSpans[0] || { name: "", side: "" };
        const victim = nameSpans[nameSpans.length - 1] || { name: "", side: "" };

        let assist = "";
        const assistHint = q('span[title="Assist"]', el);
        if (assistHint) {
          const after = assistHint.parentElement?.querySelector(".ctplayer, .tplayer");
          if (after) assist = (after.textContent || "").trim();
        }
        const flashAssist = !!q(".flashbangIcon", el);

        const weaponImg = q(".playerWeapon", el)?.getAttribute("src") || "";
        const weapon = weaponFromSrc(weaponImg);
        const headshot = !!q(".headshotIcon", el);
        const throughSmoke = !!q('img[src*="through_smoke"]', el);
        const wallbang = !!q('img[src*="penetration"]', el);

        return {
          killerName: killer.name,
          killerSide: killer.side,
          victimName: victim.name,
          victimSide: victim.side,
          weapon,
          headshot,
          throughSmoke,
          wallbang,
          flashAssist,
          assistName: assist,
        };
      };

      // Most recent near top visually; normalize to newest-first
      const killsStructured = killEls.slice(-20).map(parseKill).reverse();

      // ---------- status ----------
      const liveNode = q(selectors.liveIndicator);
      const isLive =
        document.title.includes("LIVE") ||
        !!rawRoundLbl ||
        !!timeLeft ||
        !!liveNode;

      // Determine status using CS2 MR12 + OT rules
      // Regulation: MR12 per side (max 24 rounds). First to 13 wins (can end early).
      // If 12-12 after 24, OT starts. Each OT set is 6 rounds (MR3 per side).
      // A map can only finish at the end of an OT set, with a 2+ round lead in that set (i.e., total rounds at 24 + 6k and |diff| >= 2).
      const totalRounds = (Number.isFinite(score1) ? score1 : 0) + (Number.isFinite(score2) ? score2 : 0);
      const diff = Math.abs(score1 - score2);

      let matchStatus = "live";
      let winner = "";

      if (totalRounds < 24) {
        // Regulation can end as soon as a team hits 13
        if (score1 >= 13 || score2 >= 13) {
          matchStatus = "finished";
          winner = score1 > score2 ? team1 : team2;
        }
      } else if (totalRounds === 24) {
        // 12-12 -> move to OT, not finished yet
        // If somehow a team shows 13 here (unlikely), treat as finished.
        if (score1 >= 13 || score2 >= 13) {
          matchStatus = "finished";
          winner = score1 > score2 ? team1 : team2;
        }
      } else {
        // Overtime: only finish at OT boundaries, i.e., after 6,12,18... OT rounds
        const otRounds = totalRounds - 24;
        const atBoundary = otRounds % 6 === 0; // end of an OT set
        if (atBoundary && diff >= 2) {
          matchStatus = "finished";
          winner = score1 > score2 ? team1 : team2;
        }
      }

      if (matchStatus !== "finished" && !isLive) {
        matchStatus = "paused";
      }

      return {
        // legacy/top-level
        team1, team2, score1, score2,
        isLive, matchStatus, winner,
        currentRound, mapName, timeLeft,

        // upgrades
        teams: {
          team1: { id: team1Id, name: team1, logo: team1Logo },
          team2: { id: team2Id, name: team2, logo: team2Logo },
        },
        winProbability, // { team1, team2, ot }

        players: {
          team1: playersTeam1,
          team2: playersTeam2,
        },

        // structured kill events + plain text
        kills: killsStructured,
        killFeed: killsStructured.map(
          (k) =>
            `${k.killerName}${k.headshot ? " [HS]" : ""} killed ${k.victimName} with ${k.weapon}` +
            `${k.throughSmoke ? " (smoke)" : ""}${k.wallbang ? " (wallbang)" : ""}` +
            `${k.flashAssist ? " [flash assist]" : ""}` +
            `${k.assistName ? ` + ${k.assistName} (assist)` : ""}`
        ),
      };
    }, CONFIG.selectors);

    // change detection
    const hasChanged =
      newData.team1 !== matchData.team1 ||
      newData.team2 !== matchData.team2 ||
      newData.score1 !== matchData.score1 ||
      newData.score2 !== matchData.score2 ||
      JSON.stringify(newData.kills) !== JSON.stringify(matchData.kills) ||
      JSON.stringify(newData.killFeed) !== JSON.stringify(matchData.killFeed) ||
      newData.isLive !== matchData.isLive ||
      newData.matchStatus !== matchData.matchStatus ||
      newData.winner !== matchData.winner ||
      newData.currentRound !== matchData.currentRound ||
      newData.timeLeft !== matchData.timeLeft ||
      JSON.stringify(newData.winProbability) !== JSON.stringify(matchData.winProbability) ||
      JSON.stringify(newData.players) !== JSON.stringify(matchData.players);

    if (hasChanged) {
      matchData = { ...matchData, ...newData, lastUpdate: new Date().toISOString() };

      const statusEmoji = {
        live: "🔴 LIVE",
        finished: "🏁 FINISHED",
        paused: "⏸️  PAUSED",
        waiting: "⏳ WAITING",
      };

      console.log("📊 Match data updated:", {
        teams: `${matchData.team1} vs ${matchData.team2}`,
        score: `${matchData.score1} : ${matchData.score2}`,
        status: statusEmoji[matchData.matchStatus] || matchData.matchStatus,
        winner: matchData.winner || "TBD",
        round: matchData.currentRound,
        map: matchData.mapName,
        time: matchData.timeLeft,
        winProb: matchData.winProbability,
        t1Top: matchData.players?.team1?.[0]?.name || "-",
        t2Top: matchData.players?.team2?.[0]?.name || "-",
        last3: matchData.killFeed.slice(0, 3),
      });
    }
  } catch (error) {
    console.error("❌ Error extracting data:", error.message);
  }
}

// ---------- API Routes ----------

// Current match data
app.get("/gsi", (_req, res) => {
  res.json(matchData);
});

// Latest live matches from HLTV
app.get("/matches/live", async (_req, res) => {
  try {
    const list = await fetchLiveMatches();
    res.json({
      source: "hltv",
      count: list.length,
      matches: list,
      fetchedAt: new Date().toISOString(),
    });
  } catch (error) {
    res.status(500).json({ error: error.message || String(error) });
  }
});

// Status
app.get("/status", (_req, res) => {
  res.json({
    status: "running",
    browser: browser !== null,
    page: page !== null,
    lastUpdate: matchData.lastUpdate,
    uptime: process.uptime(),
    url: CONFIG.targetUrl,
  });
});

// Start with URL (or env MATCH_URL)
app.post("/start", async (req, res) => {
  const { url } = req.body || {};
  const resolved = resolveMatchUrl(url || CONFIG.targetUrl);
  if (!resolved) return res.status(400).json({ error: "Provide a valid HLTV match URL" });

  if (browser && page) {
    return res.status(200).json({ message: "Already running", url: CONFIG.targetUrl });
  }
  await initializeBrowser(resolved);
  return res.json({ message: "Started", url: CONFIG.targetUrl });
});

// Change URL (starts if needed)
app.post("/url", async (req, res) => {
  const { url } = req.body || {};
  const resolved = resolveMatchUrl(url);
  if (!resolved) {
    return res.status(400).json({ error: 'Provide a valid HLTV match URL (https://www.hltv.org/matches/...)' });
  }

  try {
    CONFIG.targetUrl = resolved;
    if (!browser || !page) {
      await initializeBrowser(resolved);
      return res.json({ message: "Browser started and URL loaded", url: resolved });
    }

    await page.goto(resolved, { waitUntil: "networkidle2", timeout: 30000 });
    // Best-effort wait; scoreboard might appear slightly later
    try { await page.waitForSelector(CONFIG.selectors.scoreboardElement, { timeout: 10000 }); } catch {}
    startMonitoring(); // reset timer
    return res.json({ message: "URL updated successfully", url: resolved });
  } catch (error) {
    return res.status(500).json({ error: error.message });
  }
});

// Stop
app.post("/stop", async (_req, res) => {
  try {
    await stopMonitoring();
    if (browser) await browser.close();
    browser = null;
    page = null;
    return res.json({ message: "Stopped" });
  } catch (e) {
    return res.status(500).json({ error: e.message });
  }
});

// Config
app.get("/config", (_req, res) => {
  res.json({
    targetUrl: CONFIG.targetUrl,
    updateInterval: CONFIG.updateInterval,
    headless: CONFIG.headless,
    running: !!browser && !!page,
  });
});

// Update selectors at runtime
app.post("/selectors", (req, res) => {
  const { selectors } = req.body || {};
  if (!selectors || typeof selectors !== "object") {
    return res.status(400).json({ error: "Selectors object is required" });
  }
  Object.assign(CONFIG.selectors, selectors);
  res.json({ message: "Selectors updated successfully", selectors: CONFIG.selectors });
});

// Restart (optional URL)
app.post("/restart", async (req, res) => {
  try {
    const { url } = req.body || {};
    const resolved = resolveMatchUrl(url || CONFIG.targetUrl);
    if (!resolved) return res.status(400).json({ error: "Provide a valid HLTV match URL" });

    await stopMonitoring();
    if (browser) await browser.close();
    browser = null;
    page = null;

    await initializeBrowser(resolved);
    res.json({ message: "Monitoring restarted successfully", url: CONFIG.targetUrl });
  } catch (error) {
    res.status(500).json({ error: error.message });
  }
});

// Graceful shutdown
process.on("SIGINT", async () => {
  console.log("\n🛑 Shutting down gracefully...");
  try {
    await stopMonitoring();
    if (browser) await browser.close();
  } finally {
    process.exit(0);
  }
});

process.on("SIGTERM", async () => {
  console.log("\n🛑 Shutting down gracefully...");
  try {
    await stopMonitoring();
    if (browser) await browser.close();
  } finally {
    process.exit(0);
  }
});

// Start server (no auto-start scraping)
app.listen(PORT, () => {
  console.log(`🌐 Server running on http://localhost:${PORT}`);
  console.log(`📡 GSI endpoint:   http://localhost:${PORT}/gsi`);
  console.log(`📊 Status endpoint: http://localhost:${PORT}/status`);
  console.log(`👉 Start with:  curl -X POST http://localhost:${PORT}/start -H "Content-Type: application/json" -d '{"url":"https://www.hltv.org/matches/XXXXX/slug"}'`);
});
