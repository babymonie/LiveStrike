# CS2 Overlay Server

A Node.js server that uses Puppeteer to monitor web pages for CS2 match data (scores, kill feeds, team names) and serves this data via HTTP API for overlay applications.

## Features

- 🎯 **Real-time monitoring** of web pages for match data
- 🔍 **Puppeteer-based scraping** with customizable selectors
- 🌐 **HTTP API** to serve match data
- 🔄 **Auto-refresh** with configurable intervals
- 🎮 **HLTV support** with pre-configured selectors
- ⚙️ **Dynamic configuration** via API endpoints

## Installation

1. Navigate to the server directory:
```bash
cd server
```

2. Install dependencies:
```bash
npm install
```

## Usage

### Starting the Server

```bash
# Development mode (with auto-restart)
npm run dev

# Production mode
npm start
```

The server will start on `http://localhost:3000`

### API Endpoints

#### GET `/gsi` - Get Match Data
Returns current match information:
```json
{
  "team1": "Team A",
  "team2": "Team B",
  "score1": 13,
  "score2": 8,
  "kills": ["Player1 killed Player2", "Player3 killed Player4"],
  "isLive": true,
  "lastUpdate": "2024-01-01T12:00:00.000Z"
}
```

#### GET `/status` - Server Status
Returns server and browser status:
```json
{
  "status": "running",
  "browser": true,
  "page": true,
  "lastUpdate": "2024-01-01T12:00:00.000Z",
  "uptime": 3600
}
```

#### POST `/url` - Update Target URL
Update the URL being monitored:
```json
{
  "url": "https://www.hltv.org/matches/2367309/vitality-vs-navi"
}
```

#### POST `/selectors` - Update CSS Selectors
Update the CSS selectors used for scraping:
```json
{
  "selectors": {
    "team1Name": ".team1 .name",
    "team2Name": ".team2 .name",
    "score1": ".team1 .score",
    "score2": ".team2 .score"
  }
}
```

#### POST `/restart` - Restart Browser
Restarts the Puppeteer browser instance.

## Configuration

Edit the `CONFIG` object in `server.js`:

```javascript
const CONFIG = {
    targetUrl: 'https://www.hltv.org/matches',
    selectors: {
        team1Name: '.team1-side .teamName',
        team2Name: '.team2-side .teamName',
        score1: '.team1-side .won',
        score2: '.team2-side .won',
        killFeed: '.killfeed-container .kill',
        liveIndicator: '.live-indicator'
    },
    updateInterval: 2000, // Check every 2 seconds
    headless: true // Set to false for debugging
};
```

## Popular Sites & Selectors

### HLTV.org
```javascript
selectors: {
    team1Name: '.team1-side .teamName',
    team2Name: '.team2-side .teamName', 
    score1: '.team1-side .won',
    score2: '.team2-side .won',
    killFeed: '.round-history .kill',
    liveIndicator: '.match-status.live'
}
```

### ESL Play
```javascript
selectors: {
    team1Name: '.match-team:first-child .team-name',
    team2Name: '.match-team:last-child .team-name',
    score1: '.match-team:first-child .score',
    score2: '.match-team:last-child .score',
    killFeed: '.killfeed .kill-entry'
}
```

## Troubleshooting

### Browser Issues
- Set `headless: false` to see what Puppeteer is doing
- Check console logs for navigation errors
- Ensure target site allows automated access

### Selector Issues
- Use browser dev tools to find correct CSS selectors
- Test selectors in browser console first
- Update selectors via `/selectors` endpoint

### Network Issues
- Check if target site blocks automation
- Verify CORS settings if accessing from browser
- Monitor network tab for failed requests

## Development

### Adding New Sites
1. Find the correct CSS selectors for your target site
2. Update the `CONFIG.selectors` object
3. Test with `headless: false` mode
4. Adjust `updateInterval` as needed

### Custom Data Extraction
Modify the `scrapeMatchData()` function to extract additional data:

```javascript
const customData = await page.evaluate(() => {
    return {
        mapName: document.querySelector('.map-name')?.textContent,
        round: document.querySelector('.round-number')?.textContent,
        timeLeft: document.querySelector('.timer')?.textContent
    };
});
```

## License

MIT