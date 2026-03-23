using KestrelHub.Controller.Services;
using Microsoft.Extensions.Hosting;

namespace KestrelHub.Controller.Services;

public class DeploymentQueueHostedService : BackgroundService
{
    private readonly IDeploymentQueue _queue;
    private readonly IServiceProvider _serviceProvider;

    public DeploymentQueueHostedService(IDeploymentQueue queue, IServiceProvider serviceProvider)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var deploymentId = await _queue.DequeueAsync(stoppingToken);

            using var scope = _serviceProvider.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IDeploymentOrchestrator>();

            await orchestrator.RunDeploymentAsync(deploymentId);
        }
    }
}
