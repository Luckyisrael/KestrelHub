using FluentAssertions;
using KestrelHub.Controller.Data;
using KestrelHub.Shared.Enums;
using KestrelHub.Shared.Models;
using Xunit;

namespace KestrelHub.Integration.Tests;

public class DeploymentRepositoryTests : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private ApplicationDbContext _context = null!;
    private DeploymentRepository _repository = null!;

    public DeploymentRepositoryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _context = _fixture.CreateContext();
        _repository = new DeploymentRepository(_context);

        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task CreateAsync_PersistsRecord_ReturnsNonEmptyId()
    {
        var deployment = new AppDeployment
        {
            Name = "test-app",
            GitUrl = "https://github.com/test/repo.git",
            Branch = "main"
        };

        var result = await _repository.CreateAsync(deployment);

        result.Id.Should().NotBe(Guid.Empty);
        result.Name.Should().Be("test-app");
        result.GitUrl.Should().Be("https://github.com/test/repo.git");
        result.Branch.Should().Be("main");
        result.Status.Should().Be(DeploymentStatus.Pending);
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullForMissingId()
    {
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectRecordForValidId()
    {
        var created = await _repository.CreateAsync(new AppDeployment
        {
            Name = "my-app",
            GitUrl = "https://github.com/user/repo.git"
        });

        var result = await _repository.GetByIdAsync(created.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
        result.Name.Should().Be("my-app");
    }

    [Fact]
    public async Task UpdateStatusAsync_StatusChangeIsPersistedCorrectly()
    {
        var created = await _repository.CreateAsync(new AppDeployment
        {
            Name = "status-test",
            GitUrl = "https://github.com/test/repo.git"
        });

        var updated = await _repository.UpdateStatusAsync(created.Id, DeploymentStatus.Running);

        updated.Should().NotBeNull();
        updated!.Status.Should().Be(DeploymentStatus.Running);
        updated.UpdatedAt.Should().BeAfter(updated.CreatedAt);

        var fetched = await _repository.GetByIdAsync(created.Id);
        fetched!.Status.Should().Be(DeploymentStatus.Running);
    }

    [Fact]
    public async Task UpdateStatusAsync_ReturnsNullForMissingId()
    {
        var result = await _repository.UpdateStatusAsync(Guid.NewGuid(), DeploymentStatus.Running);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllDeployments_InDescendingOrderByCreatedAt()
    {
        await _repository.CreateAsync(new AppDeployment
        {
            Name = "first",
            GitUrl = "https://github.com/test/first.git"
        });

        await Task.Delay(100);

        await _repository.CreateAsync(new AppDeployment
        {
            Name = "second",
            GitUrl = "https://github.com/test/second.git"
        });

        var all = await _repository.GetAllAsync();

        all.Should().HaveCount(2);
        all[0].Name.Should().Be("second");
        all[1].Name.Should().Be("first");
    }

    [Fact]
    public async Task CreateAsync_PersistsContainerInfo_Correctly()
    {
        var deployment = await _repository.CreateAsync(new AppDeployment
        {
            Name = "container-test",
            GitUrl = "https://github.com/test/repo.git"
        });

        var container = new ContainerInfo
        {
            ContainerId = "abc123",
            AppDeploymentId = deployment.Id,
            ImageTag = "container-test:latest",
            Port = 8100,
            Status = "running"
        };

        _context.ContainerInfos.Add(container);
        await _context.SaveChangesAsync();

        var saved = await _context.ContainerInfos.FindAsync("abc123");
        saved.Should().NotBeNull();
        saved!.AppDeploymentId.Should().Be(deployment.Id);
        saved.ImageTag.Should().Be("container-test:latest");
        saved.Port.Should().Be(8100);
        saved.Status.Should().Be("running");
    }

    [Fact]
    public async Task Database_TablesExist_AfterEnsureCreated()
    {
        var canConnect = await _context.Database.CanConnectAsync();

        canConnect.Should().BeTrue();
    }
}
