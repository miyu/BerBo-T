using System;
using System.Collections.Generic;
using System.Text;
using Berbot.Commands.Polling.Creation;
using Berbot.Logging;
using Berbot.Utils;
using Dargon.Commons;
using Reddit;

namespace Berbot.Commands {
   public class InboxMonitor {
      private readonly ILog log;
      private readonly RedditClient botRedditClient;

      public InboxMonitor(ILog log, BerbotConnectionFactory config) {
         this.log = log;
         botRedditClient = config.CreateBotRedditClient();
      }

      public void BeginMonitoring() {
         foreach (var message in botRedditClient.EnumerateUserMessages()) {
            Console.WriteLine("Message: " + message.Author + message.Body.ToShortString());

            if (!TryExtractBerbotPayload(message.Body, out var payloadJson)) continue;

            BerbotPayload payload;
            try {
               Console.WriteLine("Parsing JSON: " + payloadJson);
               payload = JsonUtils.Parse<BerbotPayload>(payloadJson);
               Console.WriteLine("Payload: " + JsonUtils.ToJson(payload));

               foreach (var command in payload.Commands) {
                  Console.WriteLine("Process Command: " + JsonUtils.ToJson(command));

                  switch (command) {
                     case CreatePollCommand cpc:
                        break;
                     case VotePollCommand vpc:
                        break;
                  }
               }
            } catch (Exception e) {
               DumpException(e);
               continue;
            }
         }
      }

      private static bool TryExtractBerbotPayload(string messageBody, out string payloadJson) {
         messageBody = messageBody.Trim();

         var berbotTagName = "BerBo-T";
         var berbotBeginIndex = messageBody.IndexOf($"<{berbotTagName}>", StringComparison.OrdinalIgnoreCase) + berbotTagName.Length + 2;
         if (berbotBeginIndex < 0) goto json_body_fallback;

         var berbotEndIndex = messageBody.IndexOf($"</{berbotTagName}>", berbotBeginIndex, StringComparison.OrdinalIgnoreCase);
         if (berbotEndIndex < 0) goto json_body_fallback;

         var payloadBase64 = messageBody.Substring(berbotBeginIndex, berbotEndIndex - berbotBeginIndex).Trim();
         payloadJson = payloadBase64.IsBase64() ? Encoding.UTF8.GetString(Convert.FromBase64String(payloadBase64)) : payloadBase64;
         return true;

         json_body_fallback:
         var openCurlyIndex = messageBody.IndexOf('{');
         Console.WriteLine(openCurlyIndex);
         if (openCurlyIndex < 0) goto bail;

         var closeCurlyIndex = messageBody.LastIndexOf('}');
         Console.WriteLine(closeCurlyIndex);
         if (closeCurlyIndex < 0) goto bail;

         payloadJson = messageBody.Substring(openCurlyIndex, closeCurlyIndex - openCurlyIndex + 1);
         return true;

         bail:
         payloadJson = null;
         return false;
      }

      private static void DumpException(Exception e) {
         Console.WriteLine("== Begin Exception ==");
         Console.WriteLine(e);
         Console.WriteLine("== End Exception ==");
      }
   }
}
