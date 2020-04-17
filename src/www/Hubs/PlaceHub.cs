using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Berbot.Www.Hubs {
   public class PlaceHub : Hub {
      public async Task SendMessage(string user, string message) {
         await Clients.All.SendAsync("ReceiveMessage", user, message);
      }
   }
}
