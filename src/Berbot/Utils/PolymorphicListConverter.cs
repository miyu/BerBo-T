using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Berbot.Utils {
   public abstract class PolymorphicListConverter : JsonConverter {
      private const string TYPE_PROPERTY_NAME = "type";

      public override bool CanWrite => true;

      public override bool CanConvert(Type objectType) {
         return objectType.IsGenericType && (objectType.GetGenericTypeDefinition() == typeof(List<>));
      }

      public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
         var res = Activator.CreateInstance(objectType) as IList;

         var token = JToken.Load(reader);
         foreach (var element in token) {
            var type = element.Value<string>(TYPE_PROPERTY_NAME);
            var inst = ConstructPocoOfType(type);
            serializer.Populate(element.CreateReader(), inst);
            res.Add(inst);
         }

         return res;
      }

      public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
         serializer.Serialize(writer, value);
      }

      public abstract object ConstructPocoOfType(string type);
      public abstract string GetPocoType(object poco);
   }
}