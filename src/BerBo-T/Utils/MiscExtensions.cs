using System;

namespace Berbot.Utils {
   public static class MiscExtensions {
      public static string ToShortString(this string s, int lengthLimit = 40) {
         if (s == null) return "[null]";
         return s.Substring(0, Math.Min(lengthLimit, s.Length)).Replace("\n", "").Replace("\r", "");
      }
   }
}