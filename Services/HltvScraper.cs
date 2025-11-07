using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using CS2Overlay.Models;

namespace CS2Overlay.Services
{
    public static class HltvScraper
    {
        private static readonly HttpClient _http = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true
        });

        static HltvScraper()
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120 Safari/537.36");
            _http.Timeout = TimeSpan.FromSeconds(15);
        }

        public static async Task<List<MatchItem>> GetMatchesAsync(string baseUrl = "https://www.hltv.org/matches")
        {
            var list = new List<MatchItem>();
            var html = await _http.GetStringAsync(baseUrl);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var wrappers = doc.DocumentNode.SelectNodes("//div[contains(@class,'match-wrapper')]");
            if (wrappers == null) return list;

            foreach (var w in wrappers)
            {
                try
                {
                    var item = new MatchItem();
                    item.Id = w.GetAttributeValue("data-match-id", "");
                    int.TryParse(w.GetAttributeValue("data-stars", "0"), out var starsVal);
                    item.Stars = starsVal;

                    var a = w.SelectSingleNode(".//a[contains(@href,'/matches/')]");
                    var href = a?.GetAttributeValue("href", "");
                    if (string.IsNullOrWhiteSpace(href)) continue;
                    item.Url = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? href
                        : $"https://www.hltv.org{href}";

                    var ev = w.SelectSingleNode(".//div[contains(@class,'match-event')]");
                    item.Event =
                        ev?.GetAttributeValue("data-event-headline", "") ??
                        ev?.SelectSingleNode(".//div[contains(@class,'text-ellipsis')]")?.InnerText?.Trim() ?? "";

                    var teamNameNodes = w.SelectNodes(".//div[contains(@class,'match-teamname')]");
                    if (teamNameNodes != null && teamNameNodes.Count >= 2)
                    {
                        item.Team1 = HtmlEntity.DeEntitize(teamNameNodes[0].InnerText.Trim());
                        item.Team2 = HtmlEntity.DeEntitize(teamNameNodes[1].InnerText.Trim());
                    }

                    var metaLive = w.SelectSingleNode(".//div[contains(@class,'match-meta') and contains(@class,'match-meta-live')]");
                    item.Status = metaLive != null ? "Live" : "Upcoming";

                    var boNode = w.SelectNodes(".//div[contains(@class,'match-meta')]")
                                  ?.FirstOrDefault(n => n.InnerText.Contains("bo", StringComparison.OrdinalIgnoreCase));
                    item.Bo = boNode?.InnerText.Trim() ?? "";

                    var scores = w.SelectNodes(".//span[@data-livescore-current-map-score]");
                    if (scores != null && scores.Count >= 2)
                    {
                        var s1 = HtmlEntity.DeEntitize(scores[0].InnerText).Trim();
                        var s2 = HtmlEntity.DeEntitize(scores[1].InnerText).Trim();
                        item.ScoreNow = $"{s1}:{s2}";
                    }

                    list.Add(item);
                }
                catch { /* ignore bad card */ }
            }

            return list
                .GroupBy(x => x.Url).Select(g => g.First())
                .OrderByDescending(x => x.Status.Equals("Live", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(x => x.Stars)
                .ThenBy(x => x.Event)
                .ToList();
        }
    }
}
