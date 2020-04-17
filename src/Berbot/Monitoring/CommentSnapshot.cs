using System;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Berbot.Utils;
using Dargon.Commons;
using Reddit;

namespace Berbot.Monitoring {
   public class CommentSnapshot {
      public string FullName;
      public string Subreddit;
      public int Score;
      public string Text;
      public DateTime CreationTime;

      public void Validate() {
         FullName.ThrowIfNull(nameof(FullName));
         Subreddit.ThrowIfNull(nameof(Subreddit));

         if (CreationTime == default) throw new Exception("Creation time was empty.");
      }
   }
}
