using Berbot.Auditing;
using Berbot.Logging;
using Dargon.Commons;
using Reddit.Controllers;

namespace Berbot.Monitoring {
   public class UserFlairContext {
      private readonly AuditClient auditClient;
      private readonly Flairs flairsController;
      private string remoteText;
      private string remoteCssClass;

      public UserFlairContext(AuditClient auditClient, Flairs flairsController, string username, string text, string flairCssClass) {
         username.ThrowIfNull(nameof(username));
         text ??= "";
         flairCssClass ??= "";

         this.auditClient = auditClient;
         this.flairsController = flairsController;
         Username = username;

         remoteText = text;
         remoteCssClass = flairCssClass;

         Text = text;
         FlairCssClass = flairCssClass;
      }

      public string Username { get; }
      public string Text { get; set; }
      public string FlairCssClass { get; set; }

      public bool Commit() {
         if (!IsSemanticallyChanged) {
            return false;
         }

         auditClient.WriteAuditFlairUpdate(Username, remoteText, remoteCssClass, Text, FlairCssClass);

         if (!BerbotConfiguration.ExecuteReadOnlyDryMode) {
            flairsController.CreateUserFlair(Username, Text, FlairCssClass);
         }

         remoteText = Text;
         remoteCssClass = FlairCssClass;

         return true;
      }

      public bool IsSemanticallyChanged 
         => Text.Trim() != remoteText.Trim() || (!string.IsNullOrWhiteSpace(Text) && FlairCssClass != remoteCssClass);

      public void SetNewContributor(bool value) {
         var fragment = "🌱 New Contributor";
         var allFragments = new[] { fragment }; // including previous versions of flair

         // Strip old new contributor flair text
         if (!string.IsNullOrWhiteSpace(Text)) {
            foreach (var s in allFragments) {
               Text = Text.Replace(s, "");
            }

            Text = Text.Trim().TrimStart(' ', '|');
         }

         // Add new contributor text maybe
         if (value) {
            Text = string.IsNullOrWhiteSpace(Text)
               ? fragment
               : fragment + " | " + Text;
         }
      }
   }
}