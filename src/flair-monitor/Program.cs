using System;
using System.Collections.Concurrent;
using System.Threading;
using Berbot.Logging;
using Berbot.Monitoring;
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
         var signalRepopulateCatchUpQueue = new BlockingCollection<object>();
         signalRepopulateCatchUpQueue.Add(null);

         var userHistoryCache = new UserHistoryCache(logManager.CreateContextLog("autoflairer-user-history"), connectionFactory);
         userHistoryCache.Query("ItzWarty", true);

         var autoflairerLog = logManager.CreateContextLog("autoflairer");
         var autoflairer = new Autoflairer(connectionFactory, userFlairContextFactory, autoflairerLog, userHistoryCache);
         autoflairer.Reflair("ItzWarty");

         new Thread(() => {
            var autoflairerCatchUpQueue = new ConcurrentQueue<UserContentPostedEventArgs>();
            var autoflairerNewContentQueue = new BlockingCollection<UserContentPostedEventArgs>();
            contentMonitor.ContentPosted += x => {
               if (x.IsCatchUpLog) autoflairerCatchUpQueue.Enqueue(x);
               else autoflairerNewContentQueue.Add(x);
            };

            for (long i = 0;; i++) {
               if (i % 100 == 0) {
                  autoflairerLog.WriteLine($"Processing element {i} of autoflairer queue, new backlog {autoflairerNewContentQueue.Count}, catch up {autoflairerCatchUpQueue.Count}.");
               }

               // Take from new queue, else take from catch-up queue, else wait on new queue.
               UserContentPostedEventArgs e = null;
               while (e == null) {
                  try {
                     e = autoflairerNewContentQueue.Count == 0 && autoflairerCatchUpQueue.TryDequeue(out var catchUpEvent)
                        ? catchUpEvent
                        : autoflairerNewContentQueue.Take(new CancellationTokenSource(1000).Token);
                  } catch (OperationCanceledException) {
                     // Expected, unstuck ourselves every second in case more's been added to catch-up queue.
                     // Additionally, signal for queue to be repopulated so we periodically scan front-page
                     if (autoflairerCatchUpQueue.IsEmpty) signalRepopulateCatchUpQueue.Add(null);
                  }
               }

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

         new Thread(() => {
            var nextRepopulateTime = DateTime.MinValue;
            while (true) {
               while (DateTime.Now < nextRepopulateTime) {
                  signalRepopulateCatchUpQueue.Take();
               }

               autoflairer.IncrementMonitoringEpoch();
               contentMonitor.NotifyInitialActiveSet();
               nextRepopulateTime = DateTime.Now + TimeSpan.FromMinutes(5);

               // drain queue
               while (signalRepopulateCatchUpQueue.Count > 0) signalRepopulateCatchUpQueue.TryTake(out _);
            }
         }).Start();

         initAudit.WriteAudit("init", Environment.MachineName, "Initialized");
         initLog.WriteLine("Initialized");
         new ManualResetEvent(false).WaitOne();
      }
   }
}