using System;

namespace Berbot.Logging {
   public class Log : ILog {
      private readonly LogManager logManager;
      private readonly string name;

      public Log(LogManager logManager, string name) {
         this.logManager = logManager;
         this.name = name;
      }

      public string Name => name;
      public object Sync => logManager.Sync;

      public void WriteLine(string message) {
         logManager.HandleWriteLine(this, message);
      }

      public void WriteException(Exception e) {
         lock (Sync) {
            logManager.HandleWriteLine(this, "=== Begin Exception ===");
            logManager.HandleWriteLine(this, e.ToString());
            logManager.HandleWriteLine(this, "=== End Exception ===");
         }
      }
   }
}