using FluentAssertions;
using KestrelHub.Proxy.Data;
using KestrelHub.Proxy.Services;
using KestrelHub.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KestrelHub.Integration.Tests;

public class RouteStoreTests : IAsyncLifetime
{
    private ProxyDbContext _context = null!;
    private RouteStore _store = null!;

    public Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ProxyDbContext>()
            .UseInMemoryDatabase("routestore-test-" + Guid.NewGuid().ToString("N")[..8])
            .Options;

        _context = new ProxyDbContext(options);
        _store = new RouteStore(_context);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AddRouteAsync_PersistsToDb()
    {
        var entry = new RouteEntry
        {
            Domain = "myapp.localhost",
            TargetPort = 8100,
            DeploymentId = Guid.NewGuid()
        };

        var result = await _store.AddRouteAsync(entry);

        result.Id.Should().NotBe(Guid.Empty);
        result.IsActive.Should().BeTrue();
        result.Domain.Should().Be("myapp.localhost");
        result.TargetPort.Should().Be(8100);
    }

    [Fact]
    public async Task GetAllActiveRoutes_ReturnsOnlyActiveTrue()
    {
        var deploymentId = Guid.NewGuid();
        await _store.AddRouteAsync(new RouteEntry { Domain = "active.localhost", TargetPort = 8100, DeploymentId = deploymentId });
        await _store.AddRouteAsync(new RouteEntry { Domain = "inactive.localhost", TargetPort = 8101, DeploymentId = Guid.NewGuid() });

        // Soft-delete the second one
        var secondEntry = await _context.RouteEntries.OrderByDescending(r => r.CreatedAt).FirstAsync(r => r.Domain == "inactive.localhost");
        secondEntry.IsActive = false;
        await _context.SaveChangesAsync();

        var active = await _store.GetAllActiveRoutesAsync();

        active.Should().HaveCount(1);
        active[0].Domain.Should().Be("active.localhost");
    }

    [Fact]
    public async Task RemoveRouteAsync_SoftDeletes_IsActiveFalse()
    {
        var deploymentId = Guid.NewGuid();
        await _store.AddRouteAsync(new RouteEntry { Domain = "test.localhost", TargetPort = 8100, DeploymentId = deploymentId });

        var removed = await _store.RemoveRouteAsync(deploymentId);

        removed.Should().BeTrue();
        var active = await _store.GetAllActiveRoutesAsync();
        active.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveRouteAsync_ReturnsFalse_WhenNotFound()
    {
        var removed = await _store.RemoveRouteAsync(Guid.NewGuid());

        removed.Should().BeFalse();
    }
}
