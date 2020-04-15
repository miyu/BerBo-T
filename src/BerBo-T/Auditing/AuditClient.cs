using System;
using System.Collections.Generic;
using System.Text;
using Berbot.Logging;

namespace Berbot.Auditing {
   public class AuditClient {
      private readonly ILog log;
      private readonly DbClient db;

      public AuditClient(ILog log, BerbotConnectionFactory connectionFactory) {
         this.log = log;
         db = connectionFactory.CreateDbClient();
      }

      public void WriteAuditFlairUpdate(string subject, string oldText, string oldCssClass, string newText, string newCssClass) {
         WriteAuditInternal("flair-update", subject, $"Update '{oldText}'/'{oldCssClass}' to '{newText}'/'{newCssClass}'");
      }

      public void WriteAudit(string type, string subject, string data) {
         WriteAuditInternal(type, subject, data);
      }

      private void WriteAuditInternal(string type, string subject, string data) {
         log.WriteLine($"{type}, {subject}: {data}");
         db.WriteAudit(type, subject, data);
      }
   }
}
