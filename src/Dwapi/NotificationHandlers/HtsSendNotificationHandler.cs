using System.Threading.Tasks;
using Dwapi.SharedKernel.Events;
using Dwapi.UploadManagement.Core.Notifications.Hts;
using Microsoft.AspNetCore.SignalR;

namespace Dwapi.NotificationHandlers
{
    public class HtsSendNotificationHandler : IHandler<HtsSendNotification>
    {
        public async Task Handle(HtsSendNotification domainEvent)
        {
            await Startup.HtsHubContext.Clients.All.SendAsync("ShowHtsSendProgress", domainEvent.Progress);

            if (domainEvent.Progress.Done)
                await Startup.HtsHubContext.Clients.All.SendAsync("ShowHtsSendProgressDone",
                    domainEvent.Progress.Extract);
        }
    }
}
