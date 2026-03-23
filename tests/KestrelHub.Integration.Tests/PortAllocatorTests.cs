using FluentAssertions;
using KestrelHub.Controller.Data;
using KestrelHub.Controller.Services;
using KestrelHub.Shared.Models;
using Xunit;

namespace KestrelHub.Integration.Tests;

public class PortAllocatorTests : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private ApplicationDbContext _context = null!;
    private PortAllocator _allocator = null!;

    public PortAllocatorTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _context = _fixture.CreateContext();
        await _context.Database.EnsureCreatedAsync();
        _allocator = new PortAllocator(_context);
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task AllocateNextPort_Returns8100_ForFirstAllocation()
    {
        var port = await _allocator.AllocateNextPortAsync();

        port.Should().Be(8100);
    }

    [Fact]
    public async Task AllocateNextPort_Returns8101_ForSecondAllocation()
    {
        _context.ContainerInfos.Add(new ContainerInfo
        {
            ContainerId = "c1",
            AppDeploymentId = Guid.NewGuid(),
            ImageTag = "app:1",
            Port = 8100,
            Status = "running"
        });
        await _context.SaveChangesAsync();

        var port = await _allocator.AllocateNextPortAsync();

        port.Should().Be(8101);
    }

    [Fact]
    public async Task AllocateNextPort_DoesNotReturnPortAlreadyInUse()
    {
        _context.ContainerInfos.AddRange(
            new ContainerInfo { ContainerId = "c1", AppDeploymentId = Guid.NewGuid(), ImageTag = "app:1", Port = 8100, Status = "running" },
            new ContainerInfo { ContainerId = "c2", AppDeploymentId = Guid.NewGuid(), ImageTag = "app:2", Port = 8101, Status = "running" },
            new ContainerInfo { ContainerId = "c3", AppDeploymentId = Guid.NewGuid(), ImageTag = "app:3", Port = 8102, Status = "running" }
        );
        await _context.SaveChangesAsync();

        var port = await _allocator.AllocateNextPortAsync();

        port.Should().Be(8103);
    }

    [Fact]
    public async Task AllocateNextPort_FindsGap_InPortRange()
    {
        _context.ContainerInfos.AddRange(
            new ContainerInfo { ContainerId = "c1", AppDeploymentId = Guid.NewGuid(), ImageTag = "app:1", Port = 8100, Status = "running" },
            new ContainerInfo { ContainerId = "c2", AppDeploymentId = Guid.NewGuid(), ImageTag = "app:2", Port = 8102, Status = "running" }
        );
        await _context.SaveChangesAsync();

        var port = await _allocator.AllocateNextPortAsync();

        port.Should().Be(8101);
    }
}
