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
               HandleCommentAdded(c, false);
            }
         };
         subreddit.Comments.MonitorNew();
         subreddit.Posts.NewUpdated += (_, e) => {
            foreach (var p in e.Added) {
               HandlePostAdded(p, false);
            }
         };
         subreddit.Posts.MonitorNew();
      }

      public void NotifyInitialActiveSet() {
         var client = connectionFactory.CreateModRedditClient();
         var subreddit = client.Subreddit(BerbotConfiguration.RedditSubredditName);

         log.WriteLine("Notifying initial active set");
         foreach (var post in subreddit.Posts.New.Concat(subreddit.Posts.Hot).Distinct(RedditUtils.PostEqualityComparer)) {
            log.WriteLine($"Post {post.Id} {post.Author} {post.Title.ToShortString()}");

            var comments = post.EnumeratePostComments().ToList();
            log.WriteLine($" => {comments.Count} comments");

            HandlePostAdded(post, true);

            foreach (var comment in comments) {
               HandleCommentAdded(comment, true);
            }
         }
      }

      private void HandlePostAdded(Post p, bool isCatchUpLog) {
         if (p is SelfPost sp) {
            HandleSelfPostAdded(sp, isCatchUpLog);
         } else if (p is LinkPost lp) {
            HandleLinkPostAdded(lp, isCatchUpLog);
         }
      }

      private void HandleLinkPostAdded(LinkPost lp, bool isCatchUpLog) {
         InvokeContentPosted(new UserContentPostedEventArgs {
            Id = lp.Id,
            FullName = lp.Fullname,
            Author = lp.Author,
            Title = lp.Title,
            Content = null,
            AuthorFlairText = lp.Listing.AuthorFlairText,
            AuthorFlairCssClass = lp.Listing.AuthorFlairCSSClass,
            IsCatchUpLog = isCatchUpLog,
         }, isCatchUpLog);
      }

      private void HandleSelfPostAdded(SelfPost sp, bool isCatchUpLog) {
         InvokeContentPosted(new UserContentPostedEventArgs {
            Id = sp.Id,
            FullName = sp.Fullname,
            Author = sp.Author,
            Title = sp.Title,
            Content = sp.SelfText,
            AuthorFlairText = sp.Listing.AuthorFlairText,
            AuthorFlairCssClass = sp.Listing.AuthorFlairCSSClass,
            IsCatchUpLog = isCatchUpLog,
         }, isCatchUpLog);
      }


      private void HandleCommentAdded(Comment c, bool isCatchUpLog) {
         InvokeContentPosted(new UserContentPostedEventArgs {
            Id = c.Id,
            FullName = c.Fullname,
            Author = c.Author,
            Title = null,
            Content = c.Body,
            AuthorFlairText = c.Listing.AuthorFlairText,
            AuthorFlairCssClass = c.Listing.AuthorFlairCSSClass,
            IsCatchUpLog = isCatchUpLog,
         }, isCatchUpLog);
      }

      private void InvokeContentPosted(UserContentPostedEventArgs e, bool isCatchUpLog) {
         if (!isCatchUpLog) {
            log.WriteLine($"Emit {e.Id} {e.Author} {(e.Title ?? e.Content).ToShortString()}");
         }

         ContentPosted?.Invoke(e);
      }
   }

   public delegate void UserContentPostedEventHandler(UserContentPostedEventArgs e);

   public class UserContentPostedEventArgs {
      public string Id;
      public string FullName;
      public string Author;
      public string Title;
      public string Content;
      public string AuthorFlairText;
      public string AuthorFlairCssClass;
      public bool IsCatchUpLog;

      public bool IsDeletedAuthor => Author.Contains("[deleted]", StringComparison.OrdinalIgnoreCase);

      public override string ToString()
         => $"Author {Author?.ToShortString() ?? "[null]"} Title {Title?.ToShortString() ?? "[null]"} Content {Content?.ToShortString() ?? "[null]"}";
   }
}
