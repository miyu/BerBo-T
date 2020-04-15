using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Berbot.Logging;
using Berbot.Utils;
using Dargon.Commons;
using Microsoft.Win32.SafeHandles;
using Reddit;
using Reddit.Controllers;

namespace Berbot.Monitoring {
   public class UserContentMonitor {
      private readonly ILog log;
      private readonly BerbotConnectionFactory connectionFactory;

      public UserContentMonitor(ILog log, BerbotConnectionFactory connectionFactory) {
         this.log = log;
         this.connectionFactory = connectionFactory;
      }

      public event UserContentPostedEventHandler ContentPosted;

      private object contentPostedSync = new object();

      public void BeginMonitoring() {
         var client = connectionFactory.CreateModRedditClient();
         var subreddit = client.Subreddit(BerbotConfiguration.RedditSubredditName);

         subreddit.Comments.NewUpdated += (_, e) => {
            foreach (var c in e.Added) {
               HandleCommentAdded(c);
            }
         };
         subreddit.Comments.MonitorNew();
         subreddit.Posts.NewUpdated += (_, e) => {
            foreach (var p in e.Added) {
               HandlePostAdded(p);
            }
         };
      }

      public void NotifyInitialActiveSet() {
         var client = connectionFactory.CreateModRedditClient();
         var subreddit = client.Subreddit(BerbotConfiguration.RedditSubredditName);

         log.WriteLine("Notifying initial active set");
         foreach (var post in subreddit.Posts.New.Concat(subreddit.Posts.Hot).Distinct(RedditUtils.PostEqualityComparer)) {
            log.WriteLine($"Post {post.Id} {post.Author} {post.Title.ToShortString()}");
            HandlePostAdded(post);

            var comments = post.EnumeratePostComments().ToList();
            log.WriteLine($" => {comments.Count} comments");

            foreach (var comment in comments) {
               HandleCommentAdded(comment);
            }
         }
      }

      private void HandlePostAdded(Post p) {
         if (p is SelfPost sp) {
            HandleSelfPostAdded(sp);
         } else if (p is LinkPost lp) {
            HandleLinkPostAdded(lp);
         }
      }

      private void HandleLinkPostAdded(LinkPost lp) {
         lock (contentPostedSync) {
            ContentPosted?.Invoke(new UserContentPostedEventArgs {
               Author = lp.Author,
               Title = lp.Title,
               Content = null,
            });
         }
      }

      private void HandleSelfPostAdded(SelfPost sp) {
         lock (contentPostedSync) {
            ContentPosted?.Invoke(new UserContentPostedEventArgs {
               Author = sp.Author,
               Title = sp.Title,
               Content = sp.SelfText,
            });
         }
      }

      private void HandleCommentAdded(Comment c) {
         lock (contentPostedSync) {
            ContentPosted?.Invoke(new UserContentPostedEventArgs {
               Author = c.Author,
               Title = null,
               Content = c.Body,
            });
         }
      }
   }

   public delegate void UserContentPostedEventHandler(UserContentPostedEventArgs e);

   public class UserContentPostedEventArgs {
      public string Author;
      public string Title;
      public string Content;

      public bool IsDeletedAuthor => Author.Contains("[deleted]", StringComparison.OrdinalIgnoreCase);

      public override string ToString()
         => $"Author {Author} Title {Title.ToShortString()} Content {Content.ToShortString()}";
   }
}
