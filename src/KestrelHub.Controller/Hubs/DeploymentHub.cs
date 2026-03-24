using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace KestrelHub.Controller.Hubs;

[Authorize]
public class DeploymentHub : Hub<IDeploymentHubClient>
{
    public async Task JoinDeployment(Guid deploymentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, deploymentId.ToString());
    }

    public async Task LeaveDeployment(Guid deploymentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, deploymentId.ToString());
    }
}

public interface IDeploymentHubClient
{
    Task ReceiveLog(Guid deploymentId, string message, bool isError);
    Task StatusChanged(Guid deploymentId, string newStatus);
}
