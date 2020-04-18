using System;
using System.ComponentModel;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Berbot.Utils;
using Dargon.Commons;
using Newtonsoft.Json;
using Reddit;

namespace Berbot.Monitoring {
   public class CommentSnapshot {
      public string FullName;
      public string Subreddit;
      public int Score;
      public string Text;
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
}
