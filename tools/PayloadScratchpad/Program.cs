using System;
using System.Collections.Generic;
using System.Text;
using Berbot;
using Berbot.Commands.ParticipationRestrictions;
using Berbot.Commands.Polling.Creation;
using Berbot.Utils;

namespace PayloadScratchpad {
   public class Program {
      public static void Main(string[] args) {
         var bp = new BerbotPayload {
            Commands = {
               new CreatePollCommand {
                  Name = "This is a test",
                  Options = new List<string> { "Lorem", "Ipsum", "Dolor" },
                  PollingMode = PollingMode.Single,
                  ParticipationRestrictions = new List<IParticipationRestriction> {
                     new SubredditKarmaParticipationRestriction {
                        Threshold = 100,
                        DateTimeRangeBeginAgo = "365d"
                     },
                  },
               },
            },
         };

         var json = JsonUtils.ToJson(bp);
         Console.WriteLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));

         var next = JsonUtils.Parse<BerbotPayload>(json.Replace("\"dateTimeRangeEnd\": null,", ""));
         Console.WriteLine(JsonUtils.ToJson(next));
      }
   }
}
