using System.Collections.Generic;
using Dargon.Commons;

namespace Berbot.Monitoring {
   public class UserHistorySnapshot {
      public List<CommentSnapshot> Comments = new List<CommentSnapshot>();
      public List<PostSnapshot> Posts = new List<PostSnapshot>();

      public void Validate() {
         Comments.ThrowIfNull("Comments");

         foreach (var comment in Comments) {
            comment.Validate();
         }
      }
   }
}