using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Berbot;
using Berbot.Logging;
using Berbot.Monitoring;

namespace FlairSweeper {
   class Program {
      static void Main(string[] args) {
         var logManager = new LogManager();
         var log = logManager.CreateContextLog("flair-sweeper");
         log.WriteLine("Load Config");

         var connectionFactory = new BerbotConnectionFactory(logManager);

         var userHistoryCache = new UserHistoryCache(
            logManager.CreateContextLog("autoflairer-history-cache"), 
            connectionFactory);

         var userFlairContextFactory = new UserFlairContextFactory(connectionFactory);
         var test = userFlairContextFactory.CreateAndFetchLatestFlairContext("ItzWarty");
         test.SetNewContributor(false);
         test.Commit();
         return;

         var autoflairer = new Autoflairer(
            connectionFactory, 
            userFlairContextFactory, 
            logManager.CreateContextLog("autoflairer"), 
            userHistoryCache);

         var usernames = userHistoryCache.GetKnownUsernames().OrderBy(x => x.ToLower()).ToList();
         log.WriteLine($"Processing {usernames.Count} usernames!");

         var usernameToFlairResult = new Dictionary<string, Autoflairer.ReflairResult>();
         foreach (var username in usernames) {
            log.WriteLine($"Process: {username}");
            var result = autoflairer.Reflair(username, null, null);
            usernameToFlairResult.Add(username, result);
         }

         var contentMonitor = new UserContentMonitor(logManager.CreateContextLog("content-monitor"), connectionFactory);
         contentMonitor.ContentPosted += e => {
            if (usernameToFlairResult.TryGetValue(e.Author, out var reflairResult)) {
               log.WriteLine(reflairResult.DebugString);
            }
         };
         contentMonitor.NotifyInitialActiveSet();
      }
   }
}
