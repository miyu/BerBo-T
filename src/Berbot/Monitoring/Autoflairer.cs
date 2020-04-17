using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Berbot.Auditing;
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
      private readonly AuditClient auditClient;
      private int currentMonitoringEpoch = 0;

      public const string KVSTORE_TYPE_IS_NOOB_CACHE = "flair-newb";

      private Dictionary<string, (DateTime t, string newContributorString, int monitoringEpoch)> userToNextEvaluationTime = new Dictionary<string, (DateTime t, string newContributorString, int monitoringEpoch)>();

      public Autoflairer(BerbotConnectionFactory connectionFactory, UserFlairContextFactory userFlairContextFactory, ILog log, UserHistoryCache userHistoryCache) {
         this.connectionFactory = connectionFactory;
         this.userFlairContextFactory = userFlairContextFactory;
         this.log = log;
         this.userHistoryCache = userHistoryCache;
         this.dbClient = connectionFactory.CreateDbClient();
         this.redditClient = connectionFactory.CreateModRedditClient();
         this.auditClient = connectionFactory.CreateAuditClient();
      }

      public void IncrementMonitoringEpoch() {
         currentMonitoringEpoch++;
      }

      public void HandleContentPosted(UserContentPostedEventArgs e) {
         if (e.IsDeletedAuthor || BerbotConfiguration.AutoflareUserIgnoreList.Contains(e.Author)) {
            return;
         }

         log.WriteLine($"Handling {e.Id} by {e.Author} {(e.Title ?? e.Content).ToShortString()}");

         var now = DateTime.Now;
         if (userToNextEvaluationTime.TryGetValue(e.Author, out var record)) {
            if (record.monitoringEpoch != currentMonitoringEpoch) {
               // TODO: Detect autoflairer circumvention here.
            } else {
               if (e.IsCatchUpLog) {
                  log.WriteLine($"Skipping catch-up for {e.Author} as already evaluated.");
                  log.WriteLine(record.newContributorString);
                  return;
               }

               var isPastReevaluationThreshold = now > record.t;
               log.WriteLine($"User visited previously. Next reevaluation at {record}, now {now}. Past? {isPastReevaluationThreshold}");
               if (!isPastReevaluationThreshold) {
                  log.WriteLine(record.newContributorString);
                  return;
               }
            }
         }

         var result = Reflair(e.Author, e.AuthorFlairText ?? "", e.AuthorFlairCssClass ?? "");

         auditClient.WriteAuditPostDataPoint(new ProcessedPostDataPoint {
            Author = e.Author,
            FullName = e.IsCatchUpLog ? "[catch-up]" : e.FullName,
            IsNewContributor = result.IsNewContributor,
            ShortText = (e.Title ?? e.Content).ToShortString(),
            IsCatchUp = e.IsCatchUpLog,
            FlairChanged = result.FlairChanged,

            SubredditScore = result.SubredditScore,
            SubredditTooNewScore = result.SubredditTooNewScore,
            SubredditCommentsAnalyzed = result.SubredditCommentsAnalyzed,
            SubredditTooNewCommentsCount = result.SubredditTooNewCommentsCount,
            TotalCommentsAnalyzed = result.TotalCommentsAnalyzed,
         });
      }

      public ReflairResult Reflair(string username, string knownFlairTextOpt, string knownFlairCssClassOpt) {
         // Stop aggregating karma past this positive point.
         // This ensures that if a user has a negative past but a sufficiently positive future, their negative
         // past doesn't outweigh it.
         const int StopAggregationKarmaThreshold = 250;
         (int posts, int karma)[] PostsAndKarmaThresholds = new[] {
            (10, 200),
            (20, 100),
            (30, 50)
         };


         var subredditScore = 0;
         var tooNewCommentCount = 0;
         var tooNewCommentScore = 0;
         var postsAnalyzed = 0;
         var subredditPostsAnalyzed = 0;
         
         var now = DateTime.Now;
         var userHistory = userHistoryCache.Query(username);
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
            if (subredditScore > StopAggregationKarmaThreshold * 2 || subredditScore < -500) {
               log.WriteLine($"Bailing at score threshold, score {subredditScore}");
               break;
            }
         }

         log.WriteLine($"Done counting {postsAnalyzed} comments for {username}, {subredditPostsAnalyzed} in sub, total score {subredditScore}");

         if (tooNewCommentCount != 0) {
            log.WriteLine($"{tooNewCommentCount} comments were too new to be counted, score {tooNewCommentScore}");
         }

         var isNoob = true;
         foreach (var (postCountThreshold, karmaThreshold) in PostsAndKarmaThresholds) {
            var pass = subredditPostsAnalyzed >= postCountThreshold && subredditScore >= karmaThreshold;
            if (pass) {
               log.WriteLine($"Passed Noob Threshold >= {postCountThreshold} Posts && >= {karmaThreshold} Score");
               isNoob = false;
               break;
            }
         }
         log.WriteLine($"Evaluted IsNoob for {username} => {isNoob}");

         bool isNewContributor = isNoob;
         void UpdateFlairContext(UserFlairContext context) {
            isNewContributor = isNoob && !BerbotConfiguration.AutoflareCssClassIgnoreList.Contains(context.FlairCssClass);
            context.SetNewContributor(isNewContributor);
         }

         // First, trial run with flair from comment state, which can be quite old depending on queue depth
         var trialRunEnabled = knownFlairTextOpt != null;
         UserFlairContext trialFlairContext = null;

         if (trialRunEnabled) {
            trialFlairContext = userFlairContextFactory.CreatePreloadedFlairContext(username, knownFlairTextOpt, knownFlairCssClassOpt);
            UpdateFlairContext(trialFlairContext);
         }

         var flairChanged = false;
         if (!trialRunEnabled || trialFlairContext.IsSemanticallyChanged) {
            // Now pull latest flare context, update, and push that
            var flareContext = userFlairContextFactory.CreateAndFetchLatestFlairContext(username);
            UpdateFlairContext(flareContext);
            flairChanged = flareContext.Commit();
         }

         // debounce by 5 minutes.
         // for future: if no post happens between now and then, we should probably still exec in 5m to make
         // circumvention harder.
         var newContributorString = $"{username} IsNewContributor: {isNewContributor}, Score {subredditScore} ({tooNewCommentScore}), Posts {subredditPostsAnalyzed} ({tooNewCommentCount}) of {postsAnalyzed}";
         userToNextEvaluationTime[username] = (now + TimeSpan.FromMinutes(5), newContributorString, currentMonitoringEpoch);
         log.WriteLine(newContributorString);

         return new ReflairResult {
            DebugString = newContributorString,
            FlairChanged = flairChanged,
            IsNewContributor = isNoob,
            SubredditScore = subredditScore,
            SubredditTooNewScore = tooNewCommentScore,
            SubredditCommentsAnalyzed = subredditPostsAnalyzed,
            SubredditTooNewCommentsCount = tooNewCommentCount,
            TotalCommentsAnalyzed = postsAnalyzed,
         };
      }

      private bool IsNoobCacheEntryValid(KeyValueEntry entry, out bool isNoob) {
         isNoob = false;

         if (!entry.ExistedInDatabase) return false;
         if (!bool.TryParse(entry.Value, out isNoob)) return false;

         return !isNoob || DateTime.Now - entry.UpdatedAt > TimeSpan.FromHours(24);
      }

      public class ReflairResult {
         public string DebugString;
         public bool FlairChanged;
         public bool IsNewContributor;
         public int SubredditScore;
         public int SubredditTooNewScore;
         public int SubredditCommentsAnalyzed;
         public int SubredditTooNewCommentsCount;
         public int TotalCommentsAnalyzed;
      }
   }
}
