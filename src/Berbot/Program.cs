using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Berbot.Auditing;
using Berbot.Commands;
using Berbot.Commands.ParticipationRestrictions;
using Berbot.Commands.Polling.Creation;
using Berbot.Logging;
using Berbot.Monitoring;
using Berbot.Utils;
using Dargon.Commons;
using Npgsql;
using Reddit;
using Reddit.Controllers;
using Reddit.Inputs.Listings;
using Reddit.Inputs.Users;
using Reddit.Things;
using Post = Reddit.Things.Post;

namespace Berbot {
   public class Program {
      public static void Main(string[] args) {
         var logManager = new LogManager();
         var initLog = logManager.CreateContextLog("init");
         initLog.WriteLine("Load Config");

         var connectionFactory = new BerbotConnectionFactory(logManager);
         
         initLog.WriteLine("Load Audit");
         var initAudit = connectionFactory.CreateAuditClient();
         initAudit.WriteAudit("init", Environment.MachineName, "Initializing");

         initLog.WriteLine("Load Content Monitor");
         var contentMonitor = new UserContentMonitor(logManager.CreateContextLog("content-monitor"), connectionFactory);

         initLog.WriteLine("Load Autoflairer");
         var userFlairContextFactory = new UserFlairContextFactory(connectionFactory);
         new Thread(() => {
            var autoflairerCatchUpQueue = new ConcurrentQueue<UserContentPostedEventArgs>();
            var autoflairerNewContentQueue = new BlockingCollection<UserContentPostedEventArgs>();
            contentMonitor.ContentPosted += x => {
               if (x.IsCatchUpLog) autoflairerCatchUpQueue.Enqueue(x);
               else autoflairerNewContentQueue.Add(x);
            };

            var autoflairerLog = logManager.CreateContextLog("autoflairer");
            var userHistoryCache = new UserHistoryCache(logManager.CreateContextLog("autoflairer-user-history"), connectionFactory);
            userHistoryCache.Query("ItzWarty");
            var autoflairer = new Autoflairer(connectionFactory, userFlairContextFactory, autoflairerLog, userHistoryCache);

            for (long i = 0;; i++) {
               if (i % 100 == 0) {
                  autoflairerLog.WriteLine($"Processing element {i} of autoflairer queue, new backlog {autoflairerNewContentQueue.Count}, catch up {autoflairerCatchUpQueue.Count}.");
               }

               // Take from new queue, else take from catch-up queue, else wait on new queue.
               var e =
                  autoflairerNewContentQueue.Count == 0 && autoflairerCatchUpQueue.TryDequeue(out var catchUpEvent)
                     ? catchUpEvent
                     : autoflairerNewContentQueue.Take();

               try {
                  autoflairer.HandleContentPosted(e);
               } catch (Exception ex) {
                  autoflairerLog.WriteLine($"THREW ON {e}");
                  autoflairerLog.WriteException(ex);
               }
            }
         }).Start();

         initLog.WriteLine("Initialize Content Monitor");
         contentMonitor.BeginMonitoring();
         contentMonitor.NotifyInitialActiveSet();

         initLog.WriteLine("Load Inbox Monitor");
         var inboxMonitor = new InboxMonitor(logManager.CreateContextLog("inbox-monitor"), connectionFactory);
         inboxMonitor.BeginMonitoring();

         initAudit.WriteAudit("init", Environment.MachineName, "Initialized");
         initLog.WriteLine("Initialized");
         new ManualResetEvent(false).WaitOne();
      }
   }
}
