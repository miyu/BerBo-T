using System.Collections.Generic;
using Berbot.Commands;
using Newtonsoft.Json;

namespace Berbot {
   public class BerbotPayload {
      [JsonConverter(typeof(CommandListConverter))]
      public List<IBerbotCommand> Commands { get; set; } = new List<IBerbotCommand>();
   }
}
