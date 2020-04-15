using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Berbot.Logging;
using Berbot.Utils;
using Dargon.Commons;

namespace Berbot.Monitoring {
   public class Autoflairer {
      private readonly BerbotConnectionFactory connectionFactory;
      private readonly UserFlairContextFactory userFlairContextFactory;
      private readonly ILog log;
      private readonly BlockingCollection<UserContentPostedEventArgs> contentQueue;
      private readonly DbClient dbClient;

      private const string KVSTORE_TYPE_IS_NOOB_CACHE = "flair-newb";

      private Dictionary<string, DateTime> userToNextEvaluationTime = new Dictionary<string, DateTime>();

      public Autoflairer(BerbotConnectionFactory connectionFactory, UserFlairContextFactory userFlairContextFactory, ILog log, BlockingCollection<UserContentPostedEventArgs> contentQueue) {
         this.connectionFactory = connectionFactory;
         this.userFlairContextFactory = userFlairContextFactory;
         this.log = log;
         this.contentQueue = contentQueue;
         this.dbClient = connectionFactory.CreateDbClient();
      }

      public void HandleContentPosted(UserContentPostedEventArgs e) {
         if (e.IsDeletedAuthor) {
            return;
         }

         var now = DateTime.Now;
         if (userToNextEvaluationTime.TryGetValue(e.Author, out var nextEvaluationTime)) {
            var shouldReevaluate = now > nextEvaluationTime;
            log.WriteLine($"User visited previously. Next reevaluation OK after {nextEvaluationTime}, now {now}. Past? {shouldReevaluate}");
            if (!shouldReevaluate) return;
         }

         // debounce by 5 minutes.
         // for future: if no post happens between now and then, we should probably still exec in 5m to make
         // circumvention harder.
         userToNextEvaluationTime[e.Author] = now + TimeSpan.FromMinutes(5);

         var entry = dbClient.GetKeyValueEntry(KVSTORE_TYPE_IS_NOOB_CACHE, e.Author);

         bool isNoob;
         if (IsNoobCacheEntryValid(entry, out isNoob)) {
            log.WriteLine($"Use cached isNoob for User {e.Author}: {isNoob}");
         } else {
            log.WriteLine("Refetch Subreddit Karma for User " + e.Author);

            var redditClient = connectionFactory.CreateModRedditClient();
            var subredditScore = 0;

            const int NewbieKarmaThreshold = 200;
            const int MaximumObservedComments = 1000;

            var tooNewCommentCount = 0;
            var tooNewCommentScore = 0;
            var postsAnalyzed = 0;
            var subredditPostsAnalyzed = 0;

            foreach (var comment in redditClient.EnumerateUserPostsTimeDescending(e.Author)) {
               // Only count scores from our sub
               if (comment.Subreddit == connectionFactory.SubredditName) {
                  subredditPostsAnalyzed++;

                  // Don't count comments from before mods can get to them.
                  if ((now - comment.Created) < TimeSpan.FromDays(3)) {
                     tooNewCommentCount++;
                     tooNewCommentScore += comment.Score;
                  } else {
                     subredditScore += comment.Score;
                  }
               }

               postsAnalyzed++;

               // Bail if we've looked at too many comments
               if (postsAnalyzed >= MaximumObservedComments) {
                  log.WriteLine($"Bailing at comments analyzed threshold {postsAnalyzed}, score {subredditScore}");
                  break;
               }

               // Bail if looking too far back
               if ((DateTime.Now - comment.Created) > TimeSpan.FromDays(365 * 2)) {
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

            isNoob = subredditScore < NewbieKarmaThreshold;
            log.WriteLine($"Evaluted IsNoob for {e.Author} => {isNoob}");

            entry.SetBoolValue(isNoob);
            dbClient.PutKeyValueEntry(entry);
         }

         var userFlairContext = userFlairContextFactory.CreateFlairContext(e.Author);
         userFlairContext.FlairCssClass = "BBS";
         userFlairContext.SetNewContributor(isNoob);
         userFlairContext.Update();
      }

      private bool IsNoobCacheEntryValid(KeyValueEntry entry, out bool isNoob) {
         isNoob = false;

         if (!entry.ExistedInDatabase) return false;
         if (!bool.TryParse(entry.Value, out isNoob)) return false;

         return !isNoob || DateTime.Now - entry.UpdatedAt > TimeSpan.FromHours(24);
      }

      public void Initialize() {
      }
   }
}
