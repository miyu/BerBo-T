using System;
using Berbot.Commands.Polling.Creation;
using Berbot.Utils;

namespace Berbot.Commands {
   public class CommandListConverter : PolymorphicListConverter {
      public override object ConstructPocoOfType(string type) {
         switch (type) {
            case CreatePollCommand.SerializationType:
               return new CreatePollCommand();
            case VotePollCommand.SerializationType:
               return new VotePollCommand();
            default:
               throw new NotImplementedException(type);
         }
      }

      public override string GetPocoType(object poco) {
         return ((IBerbotCommand)poco).Type;
      }
   }
}