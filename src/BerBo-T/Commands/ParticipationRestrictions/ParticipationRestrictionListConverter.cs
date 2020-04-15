using System;
using Berbot.Utils;

namespace Berbot.Commands.ParticipationRestrictions {
   public class ParticipationRestrictionListConverter : PolymorphicListConverter {
      public override object ConstructPocoOfType(string type) {
         switch (type) {
            case SubredditKarmaParticipationRestriction.SerializationType:
               return new SubredditKarmaParticipationRestriction();
            default:
               throw new NotSupportedException(type);
         }
      }

      public override string GetPocoType(object poco)
         => ((IParticipationRestriction)poco).Type;
   }
}