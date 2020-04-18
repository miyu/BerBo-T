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

         DateTime latestDateToFetch = DateTime.MinValue;
         var isUpdateMode = existingRecord != null && existingRecord.Comments.Count > 0;
         if (isUpdateMode) {
            var newestComment = existingRecord.Comments.MaxBy(c => c.CreationTime);

            // Note: In 7 days a power user makes about ~50-70 comments. Double that for
            // hand-wavy math and this is 1-2 pages of 100 batched comments from the API
            // max. This is a reasonable request rate. Most users will fall under the 1 page category.
            latestDateToFetch = newestComment.CreationTime - TimeSpan.FromDays(7);
         }

         log.WriteLine("Fetching latest user history: " + username + $" for update?: {isUpdateMode}");

         var snapshot = existingRecord ?? new UserHistorySnapshot();
         var stats = UpdateUserRecordInternal(username, snapshot, isUpdateMode, latestDateToFetch);
         dbClient.PutKeyValueEntry(KVSTORE_USER_HISTORY_ENTRY_TYPE, username, JsonUtils.ToJson(snapshot));

         log.WriteLine($"Done. {snapshot.Comments.Count} Comments, {stats.added} Added, {stats.updated} Updated.");
         return snapshot;
      }

      private (int added, int updated) UpdateUserRecordInternal(string username, UserHistorySnapshot record, bool isUpdateMode, DateTime oldestDateToFetch) {
         var commentsByFullname = record.Comments.ToDictionary(c => c.FullName);
         var subredditCommentCount = record.Comments.Count(c => c.Subreddit == BerbotConfiguration.RedditSubredditName);
         
         var commentsAdded = 0;
         var commentsUpdated = 0;
         foreach (var (i, batch) in redditClient.EnumerateUserCommentsBatchedTimeDescendingLimit1000ish(username).Enumerate()) {
            var batchCommentsDescending = batch.OrderByDescending(c => c.Created).ToArray();
            foreach (var c in batchCommentsDescending) {
               if (!commentsByFullname.ContainsKey(c.Fullname)) {
                  commentsAdded++;
               } else {
                  commentsUpdated++;
               }

               commentsByFullname[c.Fullname] = new CommentSnapshot {
                  FullName = c.Fullname,
                  Subreddit = c.Subreddit,
                  Score = c.Score,
                  Text = c.Body,
                  CreationTime = c.Created,
                  Removed = c.Removed,
               };

               if (c.Subreddit == BerbotConfiguration.RedditSubredditName) {
                  subredditCommentCount++;
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
            if (commentsAdded + commentsUpdated > 150) {
               const int earlyBatchCommentThreshold = 5;
               if (subredditCommentCount < earlyBatchCommentThreshold) {
                  log.WriteLine($"Bailed after {commentsByFullname.Count} comments, as only {subredditCommentCount} in {BerbotConfiguration.RedditSubredditName}; threshold {earlyBatchCommentThreshold}" + username);
                  // return; // disabled to keep fetching til 1k comments or latestDateToFetch
               }
            }

            var oldestComment = batchCommentsDescending[^1];
            if (oldestComment.Created < oldestDateToFetch) {
               log.WriteLine($"At batch {i} bailing after adding {commentsAdded} updating {commentsUpdated}, as paging={oldestComment.Created} is past {oldestDateToFetch}" + username);
               break;
            }
         }

         record.Comments = commentsByFullname.Values.OrderByDescending(c => c.CreationTime).ToList();
         return (commentsAdded, commentsUpdated);
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