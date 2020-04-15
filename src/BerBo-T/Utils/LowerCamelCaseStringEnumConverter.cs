using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Dargon.Commons;
using Newtonsoft.Json;

namespace Berbot.Utils {
   /// <summary>
   /// Converts an <see cref="T:System.Enum" /> to and from its name string value.
   /// </summary>
   public class SnakeCaseStringEnumConverter : JsonConverter {
      public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
         if (value == null) {
            writer.WriteNull();
         } else {
            writer.WriteValue(value.ToString().ToDashedSnakeCase());
         }
      }

      /// <summary>Reads the JSON representation of the object.</summary>
      /// <param name="reader">The <see cref="T:Newtonsoft.Json.JsonReader" /> to read from.</param>
      /// <param name="objectType">Type of the object.</param>
      /// <param name="existingValue">The existing value of object being read.</param>
      /// <param name="serializer">The calling serializer.</param>
      /// <returns>The object value.</returns>
      public override object ReadJson(
        JsonReader reader,
        Type objectType,
        object existingValue,
        JsonSerializer serializer) {
         if (reader.TokenType == JsonToken.Null) {
            if (Nullable.GetUnderlyingType(objectType) == null)
               throw new JsonSerializationException($"Cannot convert null value to {objectType}.");
            return (object)null;
         }

         bool flag = Nullable.GetUnderlyingType(objectType) != null;
         Type type = flag ? Nullable.GetUnderlyingType(objectType) : objectType;
         if (reader.TokenType == JsonToken.String) {
            string str = reader.Value.ToString();
            return str == string.Empty & flag ? (object)null : Enum.Parse(objectType, str.ToUpperCamelCase());
         }

         throw new JsonSerializationException($"Unexpected token {reader.TokenType} when parsing enum.");
      }

      /// <summary>
      /// Determines whether this instance can convert the specified object type.
      /// </summary>
      /// <param name="objectType">Type of the object.</param>
      /// <returns>
      /// <c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.
      /// </returns>
      public override bool CanConvert(Type objectType) {
         return (Nullable.GetUnderlyingType(objectType) != null ? Nullable.GetUnderlyingType(objectType) : objectType).IsEnum;
      }
   }
}
