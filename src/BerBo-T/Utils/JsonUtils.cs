using System.ComponentModel.Design;
using Dargon.Commons;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RestSharp.Extensions;

namespace Berbot.Utils {
   public static class JsonUtils {
      private static readonly JsonSerializerSettings settings = new JsonSerializerSettings {
         ContractResolver = new LowerCamelCaseContractResolver()
      };

      public static string ToJson(object x) => JsonConvert.SerializeObject(x, Formatting.Indented, settings);
      
      public static T Parse<T>(string json) => JsonConvert.DeserializeObject<T>(json, settings);

      public class LowerCamelCaseContractResolver : DefaultContractResolver {
         protected override string ResolvePropertyName(string x)
            => x.ToLowerCamelCase();
      }
   }
}
