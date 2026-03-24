using FluentAssertions;
using KestrelHub.Controller.Data;
using KestrelHub.Controller.Services;
using KestrelHub.Shared.Enums;
using KestrelHub.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KestrelHub.Controller.Tests;

public class DeploymentOrchestratorTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly FakeGitService _gitService;
    private readonly FakeProjectScanner _projectScanner;
    private readonly FakeDockerfileGenerator _dockerfileGenerator;
    private readonly FakeDockerService _dockerService;
    private readonly FakePortAllocator _portAllocator;
    private readonly FakeRouteService _routeService;
    private readonly DeploymentRepository _repository;
    private readonly DeploymentOrchestrator _orchestrator;

    public DeploymentOrchestratorTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _repository = new DeploymentRepository(_context);
        _gitService = new FakeGitService();
        _projectScanner = new FakeProjectScanner();
        _dockerfileGenerator = new FakeDockerfileGenerator();
        _dockerService = new FakeDockerService();
        _portAllocator = new FakePortAllocator();
        _routeService = new FakeRouteService();

        _orchestrator = new DeploymentOrchestrator(
            _repository, _gitService, _projectScanner,
            _dockerfileGenerator, _dockerService, _portAllocator, _routeService, _context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task RunDeploymentAsync_Success_StatusIsRunning()
    {
        var deployment = await _repository.CreateAsync(new AppDeployment
        {
            Name = "test-app",
            GitUrl = "https://github.com/test/repo.git",
            Branch = "main"
        });

        _projectScanner.Result = new ScanResult
        {
            ProjectType = ProjectType.SingleProject,
            PrimaryProjectPath = "test-app.csproj",
            DotNetVersion = "net10.0"
        };

        await _orchestrator.RunDeploymentAsync(deployment.Id);

        var updated = await _repository.GetByIdAsync(deployment.Id);
        updated!.Status.Should().Be(DeploymentStatus.Running);
        _dockerService.ContainerCreated.Should().BeTrue();
        _gitService.CleanupCalled.Should().BeTrue();
    }

    [Fact]
    public async Task RunDeploymentAsync_GitServiceThrows_StatusIsFailed()
    {
        var deployment = await _repository.CreateAsync(new AppDeployment
        {
            Name = "fail-app",
            GitUrl = "https://github.com/invalid/repo.git",
            Branch = "main"
        });

        _gitService.ShouldThrowOnClone = true;

        await _orchestrator.RunDeploymentAsync(deployment.Id);

        var updated = await _repository.GetByIdAsync(deployment.Id);
        updated!.Status.Should().Be(DeploymentStatus.Failed);
    }

    [Fact]
    public async Task RunDeploymentAsync_BuildThrows_StatusIsFailed()
    {
        var deployment = await _repository.CreateAsync(new AppDeployment
        {
            Name = "build-fail",
            GitUrl = "https://github.com/test/repo.git",
            Branch = "main"
        });

        _projectScanner.Result = new ScanResult
        {
            ProjectType = ProjectType.SingleProject,
            PrimaryProjectPath = "app.csproj",
            DotNetVersion = "net10.0"
        };
        _dockerService.ShouldThrowOnBuild = true;

        await _orchestrator.RunDeploymentAsync(deployment.Id);

        var updated = await _repository.GetByIdAsync(deployment.Id);
        updated!.Status.Should().Be(DeploymentStatus.Failed);
    }

    [Fact]
    public async Task RunDeploymentAsync_CleanupCalledEvenOnFailure()
    {
        var deployment = await _repository.CreateAsync(new AppDeployment
        {
            Name = "cleanup-test",
            GitUrl = "https://github.com/test/repo.git",
            Branch = "main"
        });

        _gitService.ShouldThrowOnClone = true;

        await _orchestrator.RunDeploymentAsync(deployment.Id);

        _gitService.CleanupCalled.Should().BeTrue();
    }

    [Fact]
    public async Task RunDeploymentAsync_LogsAreCreated()
    {
        var deployment = await _repository.CreateAsync(new AppDeployment
        {
            Name = "log-test",
            GitUrl = "https://github.com/test/repo.git",
            Branch = "main"
        });

        _projectScanner.Result = new ScanResult
        {
            ProjectType = ProjectType.SingleProject,
            PrimaryProjectPath = "log-test.csproj",
            DotNetVersion = "net10.0"
        };

        await _orchestrator.RunDeploymentAsync(deployment.Id);

        var logs = await _context.DeploymentLogs
            .Where(l => l.DeploymentId == deployment.Id)
            .ToListAsync();

        logs.Should().NotBeEmpty();
        logs.Should().Contain(l => l.Message.Contains("Cloning repository"));
        logs.Should().Contain(l => l.Message.Contains("running on port"));
    }

    [Fact]
    public async Task RunDeploymentAsync_UnknownProject_StatusIsFailed()
    {
        var deployment = await _repository.CreateAsync(new AppDeployment
        {
            Name = "unknown-app",
            GitUrl = "https://github.com/test/repo.git",
            Branch = "main"
        });

        _projectScanner.Result = new ScanResult
        {
            ProjectType = ProjectType.Unknown,
            PrimaryProjectPath = "",
            DotNetVersion = "Unknown"
        };

        await _orchestrator.RunDeploymentAsync(deployment.Id);

        var updated = await _repository.GetByIdAsync(deployment.Id);
        updated!.Status.Should().Be(DeploymentStatus.Failed);
    }
}

#region Fakes

public class FakeGitService : IGitService
{
    public bool ShouldThrowOnClone { get; set; }
    public bool CleanupCalled { get; private set; }

    public Task CloneAsync(string gitUrl, string branch, string targetPath)
    {
        if (ShouldThrowOnClone)
            throw new InvalidOperationException("Clone failed");

        Directory.CreateDirectory(targetPath);
        File.WriteAllText(Path.Combine(targetPath, "app.csproj"), "<Project></Project>");
        return Task.CompletedTask;
    }

    public Task CleanupAsync(string path)
    {
        CleanupCalled = true;
        if (Directory.Exists(path))
            Directory.Delete(path, true);
        return Task.CompletedTask;
    }
}

public class FakeProjectScanner : IProjectScanner
{
    public ScanResult? Result { get; set; }

    public Task<ScanResult?> ScanAsync(string repoPath)
    {
        return Task.FromResult(Result);
    }
}

public class FakeDockerfileGenerator : IDockerfileGenerator
{
    public string? Generate(ScanResult scan, string repoPath)
    {
        if (scan.ProjectType is ProjectType.Unknown)
            return null;
        return "FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build";
    }
}

public class FakeDockerService : IDockerService
{
    public bool ShouldThrowOnBuild { get; set; }
    public bool ContainerCreated { get; private set; }

    public Task BuildImageAsync(string contextPath, string dockerfilePath, string imageTag, IProgress<string> progress)
    {
        if (ShouldThrowOnBuild)
            throw new InvalidOperationException("Build failed");
        progress.Report("Step 1/5 : FROM base");
        return Task.CompletedTask;
    }

    public Task<ContainerInfo> RunContainerAsync(string imageTag, int hostPort, int containerPort, Dictionary<string, string> envVars)
    {
        ContainerCreated = true;
        return Task.FromResult(new ContainerInfo
        {
            ContainerId = "fake-container-id",
            ImageTag = imageTag,
            Port = hostPort,
            Status = "running"
        });
    }

    public Task StopContainerAsync(string containerId) => Task.CompletedTask;

    public Task<string> GetContainerStatusAsync(string containerId) => Task.FromResult("running");
}

public class FakePortAllocator : IPortAllocator
{
    public Task<int> AllocateNextPortAsync() => Task.FromResult(8100);
}

public class FakeRouteService : IRouteService
{
    public Task<string> AssignDomainAsync(AppDeployment deployment, int port)
        => Task.FromResult($"{deployment.Name}.apps.localhost");

    public Task RemoveRouteAsync(Guid deploymentId) => Task.CompletedTask;
}

#endregion
