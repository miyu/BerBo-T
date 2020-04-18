using System;
using System.ComponentModel;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Berbot.Utils;
using Dargon.Commons;
using Newtonsoft.Json;
using Reddit;

namespace Berbot.Monitoring {
   public class ContributionSnapshot {
      public string FullName;
      public string Subreddit;
      public int Score;
      public DateTime CreationTime;

      [DefaultValue(false)]
      [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
      public bool Removed; // removed by mods, not deleted by user.

      public void Validate() {
         FullName.ThrowIfNull(nameof(FullName));
         Subreddit.ThrowIfNull(nameof(Subreddit));

         if (CreationTime == default) throw new Exception("Creation time was empty.");
      }
   }
   
   public class CommentSnapshot : ContributionSnapshot {
      public string Text;
   }

   public class PostSnapshot : ContributionSnapshot {
      public string Title;
      public string Content;
      public PostType PostType;
   }

   public enum PostType {
      Link,
      Self,
   }
}
