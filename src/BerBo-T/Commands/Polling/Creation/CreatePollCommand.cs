using System;
using System.Collections.Generic;
using Berbot.Commands.ParticipationRestrictions;
using Berbot.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Berbot.Commands.Polling.Creation {
   public class CreatePollCommand : IBerbotCommand {
      public const string SerializationType = "create-poll";
      public string Type => SerializationType;

      public string Name { get; set; }
      public List<string> Options { get; set; }

      [JsonConverter(typeof(SnakeCaseStringEnumConverter))]
      public PollingMode PollingMode { get; set; }

      [JsonConverter(typeof(ParticipationRestrictionListConverter))]
      public List<IParticipationRestriction> ParticipationRestrictions { get; set; }
   }

   public enum PollingMode {
      Unspecified,

      Single,
      Multi,
   }

   public class VotePollCommand : IBerbotCommand {
      public const string SerializationType = "vote-poll";
      public string Type => SerializationType;

      public Guid PollId { get; set; }
      public int OptionIndex { get; set; }
   }
}