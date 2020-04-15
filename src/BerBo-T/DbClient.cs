using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Npgsql;

namespace Berbot {
   public class DbClient {
      private readonly NpgsqlConnection conn;

      public DbClient(NpgsqlConnection conn) {
         this.conn = conn;
      }

      public void WriteAudit(string type, string subject, string data) {
         conn.Execute("INSERT INTO audit_log (type, subject, data) VALUES (@Type, @Subject, @Data)", new {
            Type = type,
            Subject = subject,
            Data = data,
         });
      }

      public KeyValueEntry GetKeyValueEntry(string type, string key) {
         var entry = conn.QueryFirstOrDefault<KeyValueEntry>("SELECT * FROM kvstore WHERE type=@Type AND key=@Key", new { Type = type, Key = key });

         if (entry != null) {
            entry.ExistedInDatabase = true;
            return entry;
         }

         entry = new KeyValueEntry(type, key, null);
         entry.ExistedInDatabase = false;
         return entry;
      }

      public KeyValueEntry PutKeyValueEntry(string type, string key, string value) {
         return conn.QuerySingle<KeyValueEntry>(@"
INSERT INTO kvstore (type, key, value)
VALUES (@Type, @Key, @Value) 
ON CONFLICT (type, key)
DO UPDATE SET value = @Value
RETURNING *", new {
            Type = type,
            Key = key,
            Value = value,
         });
      }

      public KeyValueEntry PutKeyValueEntry(KeyValueEntry entry) 
         => PutKeyValueEntry(entry.Type, entry.Key, entry.Value);
   }

   public class KeyValueEntry {
      public KeyValueEntry() { }

      public KeyValueEntry(string type, string key, bool value) : this(type, key, value.ToString()) { }
      
      public KeyValueEntry(string type, string key, int value) : this(type, key, value.ToString()) { }

      public KeyValueEntry(string type, string key, string value) {
         Type = type;
         Key = key;
         Value = value;
      }

      public string Type { get; set; }
      public string Key { get; set; }
      public string Value { get; set; }

      public DateTime CreatedAt { get; set; }
      public DateTime UpdatedAt { get; set; }

      public bool ExistedInDatabase { get; set; }

      public void SetBoolValue(bool b) => Value = b.ToString();
      public bool GetBoolValue() => bool.Parse(Value);

      public void SetIntValue(int i) => Value = i.ToString();
      public int GetIntValue() => int.Parse(Value);
   }
}