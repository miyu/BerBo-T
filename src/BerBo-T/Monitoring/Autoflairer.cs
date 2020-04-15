using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Berbot.Logging;
using Berbot.Utils;
using Dargon.Commons;
using Reddit;

namespace Berbot.Monitoring {
   public class Autoflairer {
      private readonly BerbotConnectionFactory connectionFactory;
      private readonly UserFlairContextFactory userFlairContextFactory;
      private readonly ILog log;
      private readonly UserHistoryCache userHistoryCache;
      private readonly DbClient dbClient;
      private readonly RedditClient redditClient;

      private const string KVSTORE_TYPE_IS_NOOB_CACHE = "flair-newb";

      private Dictionary<string, (DateTime t, bool lastIsNewb)> userToNextEvaluationTime = new Dictionary<string, (DateTime t, bool lastIsNewb)>();

      public Autoflairer(BerbotConnectionFactory connectionFactory, UserFlairContextFactory userFlairContextFactory, ILog log, UserHistoryCache userHistoryCache) {
         this.connectionFactory = connectionFactory;
         this.userFlairContextFactory = userFlairContextFactory;
         this.log = log;
         this.userHistoryCache = userHistoryCache;
         this.dbClient = connectionFactory.CreateDbClient();
         this.redditClient = connectionFactory.CreateModRedditClient();
      }

      public void HandleContentPosted(UserContentPostedEventArgs e) {
         if (e.IsDeletedAuthor || BerbotConfiguration.AutoflareUserIgnoreList.Contains(e.Author)) {
            return;
         }

         log.WriteLine($"Handling {e.Id} by {e.Author} {(e.Title ?? e.Content).ToShortString()}");

         var now = DateTime.Now;
         if (userToNextEvaluationTime.TryGetValue(e.Author, out var record)) {
            if (e.IsCatchUpLog) {
               log.WriteLine($"Skipping catch-up for {e.Author} as already evaluated.");
               log.WriteLine($"IsNewContributor: {record.lastIsNewb}");
               return;
            }

            var isPastReevaluationThreshold = now > record.t;
            log.WriteLine($"User visited previously. Next reevaluation at {record}, now {now}. Past? {isPastReevaluationThreshold}");
            if (!isPastReevaluationThreshold) {
               log.WriteLine($"IsNewContributor: {record.lastIsNewb}");
               return;
            }
         }

         const int NewbieKarmaThreshold = 200;

         var subredditScore = 0;
         var tooNewCommentCount = 0;
         var tooNewCommentScore = 0;
         var postsAnalyzed = 0;
         var subredditPostsAnalyzed = 0;

         var userHistory = userHistoryCache.Query(e.Author);
         foreach (var comment in userHistory.Comments.OrderByDescending(c => c.CreationTime)) {
            // Only count scores from our sub
            if (comment.Subreddit == BerbotConfiguration.RedditSubredditName) {
               subredditPostsAnalyzed++;

               // Don't count comments from before mods can get to them.
               if ((now - comment.CreationTime) < TimeSpan.FromDays(3)) {
                  tooNewCommentCount++;
                  tooNewCommentScore += comment.Score;
               } else {
                  subredditScore += comment.Score;
               }
            }

            postsAnalyzed++;

            // Bail if looking too far back
            if ((DateTime.Now - comment.CreationTime) > TimeSpan.FromDays(365 * 2)) {
               log.WriteLine($"Bailing at comment age threshold, score {subredditScore}");
               break;
            }

            // Bail if we can guess the outcome
            if (subredditScore > NewbieKarmaThreshold * 2 || subredditScore < -500) {
               log.WriteLine($"Bailing at score threshold, score {subredditScore}");
               break;
            }
         }

         log.WriteLine($"Done counting {postsAnalyzed} comments for {e.Author}, {subredditPostsAnalyzed} in sub, total score {subredditScore}");

         if (tooNewCommentCount != 0) {
            log.WriteLine($"{tooNewCommentCount} comments were too new to be counted, score {tooNewCommentScore}");
         }

         var isNoob = subredditScore < NewbieKarmaThreshold;
         log.WriteLine($"Evaluted IsNoob for {e.Author} => {isNoob}");

         bool isNewContributor = isNoob;
         void UpdateFlairContext(UserFlairContext context) {
            isNewContributor = isNoob && !BerbotConfiguration.AutoflareCssClassIgnoreList.Contains(context.FlairCssClass);
            context.SetNewContributor(isNewContributor);
         }

         // First, trial run with flair from comment state, which can be quite old depending on queue depth
         var staleFlareContext = userFlairContextFactory.CreatePreloadedFlairContext(e.Author, e.AuthorFlairText, e.AuthorFlairCssClass);

         UpdateFlairContext(staleFlareContext);

         if (staleFlareContext.IsSemanticallyChanged) {
            // Now pull latest flare context, update, and push that
            var flareContext = userFlairContextFactory.CreateAndFetchLatestFlairContext(e.Author);
            UpdateFlairContext(flareContext);
            flareContext.Commit();
         }

         // debounce by 5 minutes.
         // for future: if no post happens between now and then, we should probably still exec in 5m to make
         // circumvention harder.
         userToNextEvaluationTime[e.Author] = (now + TimeSpan.FromMinutes(5), isNewContributor);
         log.WriteLine($"IsNewContributor: {isNewContributor}");
      }

      private bool IsNoobCacheEntryValid(KeyValueEntry entry, out bool isNoob) {
         isNoob = false;

         if (!entry.ExistedInDatabase) return false;
         if (!bool.TryParse(entry.Value, out isNoob)) return false;

         return !isNoob || DateTime.Now - entry.UpdatedAt > TimeSpan.FromHours(24);
      }
   }
}
