using System;

namespace Berbot.Commands.ParticipationRestrictions {
   public class SubredditKarmaParticipationRestriction : IParticipationRestriction {
      public const string SerializationType = "subreddit-karma";

      public string Type => SerializationType;

      public long Threshold { get; set; }
      
      public DateTime? DateTimeRangeBegin { get; set; }

      public DateTime? DateTimeRangeEnd { get; set; }

      public string DateTimeRangeBeginAgo { get; set; }

      public string DateTimeRangeEndAgo { get; set; }
   }
}