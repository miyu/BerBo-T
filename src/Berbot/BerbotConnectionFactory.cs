using System.Collections.Generic;
using System.Text;
using Berbot.Auditing;
using Berbot.Logging;
using Npgsql;
using Reddit;

namespace Berbot {
   public class BerbotConnectionFactory {
      private readonly LogManager logManager;

      public BerbotConnectionFactory(LogManager logManager) {
         this.logManager = logManager;
      }

      public RedditClient CreateModRedditClient() => new RedditClient(
         BerbotConfiguration.RedditAppId, 
         BerbotConfiguration.RedditModRefreshToken, 
         BerbotConfiguration.RedditAppSecret,
         BerbotConfiguration.RedditModAccessToken, 
         "BerBo-T Mod");

      public RedditClient CreateBotRedditClient() => new RedditClient(
         BerbotConfiguration.RedditAppId,
         BerbotConfiguration.RedditBotRefreshToken,
         BerbotConfiguration.RedditAppSecret,
         BerbotConfiguration.RedditBotAccessToken,
         "BerBo-T Bot");

      public DbClient CreateDbClient() {
         var dbConnectionString = "User ID=postgres;Password=password;Host=localhost;Port=5432;Database=postgres;Pooling=true;SearchPath=berbot";
         var connection = new NpgsqlConnection(dbConnectionString);
         Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
         return new DbClient(connection);
      }

      public AuditClient CreateAuditClient() {
         return new AuditClient(logManager.CreateContextLog("audit"), this);
      }
   }
}
