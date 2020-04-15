using System;

namespace Berbot.Utils {
   public static class MiscExtensions {
      public static long ToUnixTimeSeconds(this DateTime dt)
         => (long)dt.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

      public static string ToShortString(this string s, int lengthLimit = 40)
         => s.Substring(0, Math.Min(lengthLimit, s.Length)).Replace("\n", "").Replace("\r", "");
   }
}