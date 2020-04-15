using Berbot.Auditing;
using Dargon.Commons;
using Reddit;
using Reddit.Things;
using Subreddit = Reddit.Controllers.Subreddit;
using User = Reddit.Controllers.User;

namespace Berbot.Monitoring {
   public class UserFlairContextFactory {
      private readonly BerbotConnectionFactory connectionFactory;
      private readonly AuditClient auditClient;
      private readonly RedditClient modRedditClient;
      private readonly Subreddit subreddit;

      public UserFlairContextFactory(BerbotConnectionFactory connectionFactory) {
         this.connectionFactory = connectionFactory;
         this.auditClient = connectionFactory.CreateAuditClient();
         this.modRedditClient = connectionFactory.CreateModRedditClient();
         this.subreddit = modRedditClient.Subreddit(BerbotConfiguration.RedditSubredditName);
      }

      public UserFlairContext CreateAndFetchLatestFlairContext(string username) {
         var currentFlair = subreddit.Flairs.FlairSelector(username);
         return new UserFlairContext(auditClient, subreddit.Flairs, username, currentFlair.Current.FlairText, currentFlair.Current.FlairCssClass);
      }

      public UserFlairContext CreatePreloadedFlairContext(string username, string flairText, string flairCssClass) {
         return new UserFlairContext(auditClient, subreddit.Flairs, username, flairText, flairCssClass);
      }
   }
}
