namespace Berbot.Auditing {
   public class ProcessedPostDataPoint {
      public string FullName { get; set; }
      public string Author { get; set; }
      public string ShortText { get; set; }
      public bool IsNewContributor { get; set; }
      public bool IsCatchUp { get; set; }
      public int SubredditScore { get; set; }
      public int SubredditTooNewScore { get; set; }
      public int SubredditCommentsAnalyzed { get; set; }
      public int SubredditTooNewCommentsCount { get; set; }
      public int TotalCommentsAnalyzed { get; set; }
      public bool FlairChanged { get; set; }
   }
}