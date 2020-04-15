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

      public UserHistorySnapshot Query(string username) {
         var entry = dbClient.GetKeyValueEntry(KVSTORE_USER_HISTORY_ENTRY_TYPE, username);

         if (entry.ExistedInDatabase && 
             (DateTime.Now - entry.UpdatedAt) < TimeSpan.FromDays(1) &&
             TryParseUserHistorySnapshot(entry.Value, out var record)) {
            log.WriteLine("Using cached user history: " + username);
            return record;
         }

         log.WriteLine("Fetching latest user history: " + username);

         var comments = new List<Comment>();
         var bailed = false;
         foreach (var (i, batch) in redditClient.EnumerateUserCommentsBatchedTimeDescending(username).Enumerate()) {
            comments.AddRange(batch);

            // After a few batches (with a poweruser posting 20-30 comments a day), we have about 2 weeks worth of data
            // If only a small % of those posts are in the subreddit, we're mostly dealing with a lurker & don't need
            // to fetch more data. This saves API calls.
            // 
            // Eventually, we will collect enough data (through combining multiple history snapshots) that we'll have
            // more history than the comment APIs can give us anyway.
            //
            // It is extremely unlikely that someone makes more than 150 posts in a day, given in 16 hours awake,
            // that'd be one post every 6.4 minutes, so I'm not too worried about dropping messages here.
            if (!bailed && comments.Count > 150) {
               var subredditCommentCount = comments.Count(c => c.Subreddit == BerbotConfiguration.RedditSubredditName);
               const int earlyBatchCommentThreshold = 5;
               if (subredditCommentCount < earlyBatchCommentThreshold) {
                  log.WriteLine($"Bailed after {comments.Count} comments, as only {subredditCommentCount} in {BerbotConfiguration.RedditSubredditName}; threshold {earlyBatchCommentThreshold}" + username);
                  bailed = true;
               }
            }
         }

         var snapshot = new UserHistorySnapshot {
            Comments = comments.Map(c => new CommentSnapshot {
               FullName = c.Fullname,
               Subreddit = c.Subreddit,
               Score = c.Score,
               Text = c.Body,
               CreationTime = c.Created,
            }).ToList()
         };
         dbClient.PutKeyValueEntry(KVSTORE_USER_HISTORY_ENTRY_TYPE, username, JsonUtils.ToJson(snapshot));

         log.WriteLine($"Done. {snapshot.Comments.Count} Comments");

         return snapshot;
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