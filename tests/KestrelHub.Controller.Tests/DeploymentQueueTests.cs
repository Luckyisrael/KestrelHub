using System.Threading.Channels;
using FluentAssertions;
using KestrelHub.Controller.Services;
using Xunit;

namespace KestrelHub.Controller.Tests;

public class DeploymentQueueTests
{
    [Fact]
    public async Task EnqueueAndDequeue_ReturnsCorrectOrder()
    {
        var queue = new DeploymentQueue();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        await queue.EnqueueAsync(id1);
        await queue.EnqueueAsync(id2);

        var result1 = await queue.DequeueAsync(CancellationToken.None);
        var result2 = await queue.DequeueAsync(CancellationToken.None);

        result1.Should().Be(id1);
        result2.Should().Be(id2);
    }

    [Fact]
    public async Task DequeueAsync_WaitsUntilItemAvailable()
    {
        var queue = new DeploymentQueue();
        var id = Guid.NewGuid();

        var dequeueTask = queue.DequeueAsync(CancellationToken.None);
        dequeueTask.IsCompleted.Should().BeFalse();

        await queue.EnqueueAsync(id);
        var result = await dequeueTask;

        result.Should().Be(id);
    }

    [Fact]
    public async Task DequeueAsync_ThrowsOnCancellation()
    {
        var queue = new DeploymentQueue();
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var act = () => queue.DequeueAsync(cts.Token).AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task QueueProcessesSequentially()
    {
        var queue = new DeploymentQueue();
        var results = new List<Guid>();

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        await queue.EnqueueAsync(id1);
        await queue.EnqueueAsync(id2);
        await queue.EnqueueAsync(id3);

        while (results.Count < 3)
        {
            var id = await queue.DequeueAsync(CancellationToken.None);
            results.Add(id);
        }

        results.Should().ContainInOrder(id1, id2, id3);
    }
}
