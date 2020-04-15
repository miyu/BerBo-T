using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dargon.Commons;
using Dargon.Commons.Collections;
using Dargon.Commons.Comparers;
using Reddit;
using Reddit.Controllers;
using Reddit.Things;
using Comment = Reddit.Controllers.Comment;
using Post = Reddit.Controllers.Post;

namespace Berbot.Utils {
   public static class RedditClientExtensions {
      public static IEnumerable<Comment> EnumerateUserCommentsTimeDescending(this RedditClient client, string username) {
         return EnumerateUserCommentsBatchedTimeDescending(client, username).SelectMany(x => x);
      }

      public static IEnumerable<List<Comment>> EnumerateUserCommentsBatchedTimeDescending(this RedditClient client, string username) {
         var user = client.User(username);
         var afterFullName = "";
         while (true) {
            var comments = user.GetCommentHistory(after: afterFullName, limit: 100);
            if (comments.Count == 0) break;

            yield return comments;

            afterFullName = comments[^1].Fullname;
         }
      }

      public static IEnumerable<Message> EnumerateUserMessages(this RedditClient client) {
         string lastReadMessageFullName = "";

         while (true) {
            var messages = client.Account.Messages.GetMessages("inbox", after: lastReadMessageFullName);
            if (messages.Count == 0) break;

            foreach (var message in messages) {
               yield return message;
            }

            lastReadMessageFullName = messages[^1].Fullname;
         }
      }

      /// <summary>
      /// Note: this is slightly bugged. in https://old.reddit.com/r/SandersForPresident/comments/g0j7gq/bernie_sanders_reportedly_penning_new_book_on/
      /// there are 1414 comments but this only finds 1327... I've a hunch it either skips deleted comments or really deeply nested threads.
      /// </summary>
      public static IEnumerable<Comment> EnumeratePostComments(this Post post) {
         var res = new AddOnlyOrderedHashSet<Comment>(RedditUtils.CommentEqualityComparer);

         foreach (var c in post.Comments.GetComments(limit: 100000, context: 8)) res.Add(c);

         for (var i = 0; i < res.Count; i++) {
            var c = res[i];

            yield return c;

            if (c.replies != null) {
               foreach (var r in c.replies) {
                  res.Add(r);
               }
            }

            if (c.More != null) {
               foreach (var more in c.More) {
                  if (more.Children.Count == 0) continue;

                  var morec = post.MoreChildren(more.Children.Join(","), false, "new");
                  foreach (var child in morec.Comments) {
                     var dispatch = (Dispatch)typeof(Post).GetField("Dispatch", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(post);
                     var com = new Comment(dispatch, child);
                     res.Add(com);
                  }
               }
            }
         }
      }
   }

   public static class RedditUtils {
      public static readonly IEqualityComparer<Comment> CommentEqualityComparer = new LambdaEqualityComparer<Comment>((a, b) => a.Id == b.Id, c => c.Id.GetHashCode());

      public static readonly IEqualityComparer<Post> PostEqualityComparer = new LambdaEqualityComparer<Post>((a, b) => a.Id == b.Id, p => p.Id.GetHashCode());
   }
}