using System;
using System.Collections.Generic;
using System.Linq;
using Berbot.Logging;
using Berbot.Utils;
using Dargon.Commons;
using Reddit;
using Reddit.Controllers;

namespace Berbot.Monitoring {
   public class UserHistoryCache {
      private readonly ILog log;
      private readonly DbClient dbClient;
      private readonly RedditClient redditClient;

      private const string KVSTORE_USER_HISTORY_ENTRY_TYPE = "user-history";

      public UserHistoryCache(ILog log, BerbotConnectionFactory connectionFactory) {
         this.log = log;
         this.dbClient = connectionFactory.CreateDbClient();
         this.redditClient = connectionFactory.CreateModRedditClient();
      }

      public UserHistorySnapshot Query(string username, bool forceReevaluation = false) {
         var entry = dbClient.GetKeyValueEntry(KVSTORE_USER_HISTORY_ENTRY_TYPE, username);

         UserHistorySnapshot existingRecord = null;
         if (entry.ExistedInDatabase && 
             TryParseUserHistorySnapshot(entry.Value, out existingRecord) &&
             !forceReevaluation &&
             (DateTime.Now - entry.UpdatedAt) < TimeSpan.FromDays(1)) {
            log.WriteLine("Using cached user history: " + username);
            return existingRecord;
         }

         log.WriteLine("Fetching latest user history: " + username);

         var snapshot = existingRecord ?? new UserHistorySnapshot();
         var commentStats = UpdateUserRecordCommentsInternal(username, snapshot);
         var postStats = UpdateUserRecordPostsInternal(username, snapshot);
         dbClient.PutKeyValueEntry(KVSTORE_USER_HISTORY_ENTRY_TYPE, username, JsonUtils.ToJson(snapshot));

         log.WriteLine($"Done. {snapshot.Comments.Count}c/{snapshot.Posts.Count}p, {commentStats.added}c/{postStats.added}p Added, {commentStats.updated}c/{postStats.updated}p Updated.");
         return snapshot;
      }

      private (int added, int updated) UpdateUserRecordCommentsInternal(string username, UserHistorySnapshot record) {
         var (added, updated, nextComments) = UpdateUserRecordContributionsInternal<CommentSnapshot, Comment>(
            username,
            record.Comments,
            c => new CommentSnapshot {
               FullName = c.Fullname,
               Subreddit = c.Subreddit,
               Score = c.Score,
               Text = c.Body,
               CreationTime = c.Created,
               Removed = c.Removed,
            },
            () => redditClient.EnumerateUserCommentsBatchedTimeDescendingLimit1000ish(username),
            c => c.Created,
            c => c.Fullname,
            c => c.Subreddit,
            "comment");
         record.Comments = nextComments;
         return (added, updated);
      }

      private (int added, int updated) UpdateUserRecordPostsInternal(string username, UserHistorySnapshot record) {
         var (added, updated, nextPosts) = UpdateUserRecordContributionsInternal<PostSnapshot, Post>(
            username,
            record.Posts,
            p => {
               var (content, postType) =
                  p is LinkPost lp ? (lp.URL, PostType.Link) :
                  p is SelfPost sp ? (sp.SelfText, PostType.Self) :
                  throw new NotSupportedException("Unknown post type: " + p.GetType().FullName);

               return new PostSnapshot {
                  FullName = p.Fullname,
                  Subreddit = p.Subreddit,
                  Score = p.Score,
                  Title = p.Title,
                  Content = content,
                  PostType = postType,
                  CreationTime = p.Created,
                  Removed = p.Removed,
               };
            },
            () => redditClient.EnumerateUserPostsBatchedTimeDescendingLimit1000ish(username),
            p => p.Created,
            p => p.Fullname,
            p => p.Subreddit,
            "comment");
         record.Posts = nextPosts;
         return (added, updated);
      }

      private (int added, int updated, List<TContributionSnapshot>) UpdateUserRecordContributionsInternal<TContributionSnapshot, TThingController>(
         string username, 
         IReadOnlyList<TContributionSnapshot> initialContributions,
         Func<TThingController, TContributionSnapshot> projectThingToSnapshot,
         Func<IEnumerable<List<TThingController>>> enumerateBatches,
         Func<TThingController, DateTime> queryCreationDate,
         Func<TThingController, string> getFullName,
         Func<TThingController, string> getSubreddit,
         string typeVanity
      ) where TContributionSnapshot : ContributionSnapshot {
         // Note: In 7 days a power user makes about ~50-70 comments. Double that for
         // hand-wavy math and this is 1-2 pages of 100 batched comments from the API
         // max. This is a reasonable request rate. Most users will fall under the 1 page category.
         DateTime oldestDateToFetch = initialContributions.Count == 0
            ? DateTime.MinValue
            : (initialContributions.MaxBy(c => c.CreationTime).CreationTime - TimeSpan.FromDays(7));

         var contributionsByFullName = initialContributions.ToDictionary(c => c.FullName);
         var subredditContributionCount = initialContributions.Count(c => c.Subreddit == BerbotConfiguration.RedditSubredditName);
         
         var added = 0;
         var updated = 0;
         foreach (var (i, batch) in enumerateBatches().Enumerate()) {
            var batchCommentsDescending = batch.OrderByDescending(queryCreationDate).ToArray();
            foreach (var c in batchCommentsDescending) {
               var fullName = getFullName(c);
               var subreddit = getSubreddit(c);

               if (!contributionsByFullName.ContainsKey(fullName)) {
                  added++;
               } else {
                  updated++;
               }

               contributionsByFullName[fullName] = projectThingToSnapshot(c);

               if (subreddit == BerbotConfiguration.RedditSubredditName) {
                  subredditContributionCount++;
               }
            }

            // After a few batches (with a poweruser posting 20-30 comments a day), we have about 2 weeks worth of data
            // If only a small % of those posts are in the subreddit, we're mostly dealing with a lurker & don't need
            // to fetch more data. This saves API calls.
            // 
            // Eventually, we will collect enough data (through combining multiple history snapshots) that we'll have
            // more history than the comment APIs can give us anyway.
            //
            // It is extremely unlikely that someone makes more than 150 posts in a day, given in 16 hours awake,
            // that'd be one post every 6.4 minutes, so I'm not too worried about dropping messages here.
            if (added + updated > 150) {
               const int earlyBatchCommentThreshold = 5;
               if (subredditContributionCount < earlyBatchCommentThreshold) {
                  log.WriteLine($"Bailed on {username} after {contributionsByFullName.Count} {typeVanity}, as only {subredditContributionCount} in {BerbotConfiguration.RedditSubredditName}; threshold {earlyBatchCommentThreshold}");
                  // return; // disabled to keep fetching til 1k comments or latestDateToFetch
               }
            }

            var oldestComment = batchCommentsDescending[^1];
            var oldestCommentCreationTime = queryCreationDate(oldestComment);
            if (oldestCommentCreationTime < oldestDateToFetch) {
               log.WriteLine($"Bailed on {username} at batch {i} after adding {added} updating {updated}, as paging={oldestCommentCreationTime} is past {oldestDateToFetch}" + username);
               break;
            }
         }

         return (added, updated, contributionsByFullName.Values.OrderByDescending(c => c.CreationTime).ToList());
      }

      public List<string> GetKnownUsernames() {
         return dbClient.EnumerateKeyValueEntries(KVSTORE_USER_HISTORY_ENTRY_TYPE)
                        .Select(x => x.Key)
                        .ToList();
      }

      private bool TryParseUserHistorySnapshot(string json, out UserHistorySnapshot res) {
         try {
            res = JsonUtils.Parse<UserHistorySnapshot>(json);
            res.Validate();
            return res != null;
         } catch (Exception e) {
            log.WriteException(e);
            res = null;
            return false;
         }
      }
   }
}