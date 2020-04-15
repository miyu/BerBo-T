using System;

namespace Berbot.Logging {
   public interface ILog {
      void WriteLine(string s);
      void WriteException(Exception e);
   }
}