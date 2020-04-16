using System;
using System.Collections.Generic;
using System.Text;

namespace Berbot.Logging {
   public class LogManager {
      private readonly object sync = new object();

      public object Sync => sync;

      public ILog CreateContextLog(string name) {
         return new Log(this, name);
      }

      internal void HandleWriteLine(Log log, string message) {
         lock (sync) {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Console.WriteLine($"{timestamp} [{log.Name}] {message}");
         }
      }
   }
}
