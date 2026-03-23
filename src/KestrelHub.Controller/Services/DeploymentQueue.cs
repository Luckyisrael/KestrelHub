using System.Threading.Channels;
using KestrelHub.Shared.Models;

namespace KestrelHub.Controller.Services;

public interface IDeploymentQueue
{
    ValueTask EnqueueAsync(Guid deploymentId);
    ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken);
}

public class DeploymentQueue : IDeploymentQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public ValueTask EnqueueAsync(Guid deploymentId)
    {
        return _channel.Writer.WriteAsync(deploymentId);
    }

    public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
