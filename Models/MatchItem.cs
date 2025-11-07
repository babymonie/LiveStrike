namespace CS2Overlay.Models
{
    public class MatchItem
    {
        public string Id { get; set; } = "";
        public string Url { get; set; } = "";
        public string Event { get; set; } = "";
        public string Team1 { get; set; } = "";
        public string Team2 { get; set; } = "";
        public string Status { get; set; } = ""; // Live / Upcoming
        public string Bo { get; set; } = "";     // bo3 / bo1 etc
        public string ScoreNow { get; set; } = ""; // e.g. "7:9"
        public int Stars { get; set; } = 0;

        public string Title => $"{Team1} vs {Team2}";
    }
}
