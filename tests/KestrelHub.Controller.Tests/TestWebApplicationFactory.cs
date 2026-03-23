using KestrelHub.Controller.Data;
using KestrelHub.Controller.Services;
using KestrelHub.Shared.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KestrelHub.Controller.Tests;

public class TestAppFactory : IDisposable
{
    private readonly WebApplication _app;
    private readonly HttpClient _client;
    private readonly SqliteConnection _sqliteConnection;

    public HttpClient Client => _client;
    public IServiceProvider Services => _app.Services;

    public TestAppFactory()
    {
        _sqliteConnection = new SqliteConnection("DataSource=:memory:");
        _sqliteConnection.Open();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });

        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton(_sqliteConnection);
        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseSqlite(sp.GetRequiredService<SqliteConnection>());
        });

        builder.Services.AddScoped<IDeploymentRepository, DeploymentRepository>();
        builder.Services.AddScoped<IGitService, FakeGitServiceForApiTests>();
        builder.Services.AddScoped<IProjectScanner, FakeProjectScannerForApiTests>();
        builder.Services.AddScoped<IDockerfileGenerator, FakeDockerfileGeneratorForApiTests>();
        builder.Services.AddScoped<IDockerService, FakeDockerServiceForApiTests>();
        builder.Services.AddScoped<IPortAllocator, FakePortAllocatorForApiTests>();
        builder.Services.AddScoped<IDeploymentOrchestrator, DeploymentOrchestrator>();
        builder.Services.AddSingleton<IDeploymentQueue, DeploymentQueue>();
        builder.Services.AddHostedService<DeploymentQueueHostedService>();
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(Program).Assembly);

        _app = builder.Build();
        _app.MapControllers();

        // Create schema
        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
        }

        _app.StartAsync().GetAwaiter().GetResult();

        _client = _app.GetTestClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _app.StopAsync().GetAwaiter().GetResult();
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _sqliteConnection.Dispose();
    }
}

public class FakeDockerServiceForApiTests : IDockerService
{
    public Task BuildImageAsync(string contextPath, string dockerfilePath, string imageTag, IProgress<string> progress)
        => Task.CompletedTask;

    public Task<ContainerInfo> RunContainerAsync(string imageTag, int hostPort, int containerPort, Dictionary<string, string> envVars)
        => Task.FromResult(new ContainerInfo { ContainerId = "fake-id", ImageTag = imageTag, Port = hostPort, Status = "running" });

    public Task StopContainerAsync(string containerId) => Task.CompletedTask;
    public Task<string> GetContainerStatusAsync(string containerId) => Task.FromResult("running");
}

public class FakeGitServiceForApiTests : IGitService
{
    public Task CloneAsync(string gitUrl, string branch, string targetPath)
    {
        Directory.CreateDirectory(targetPath);
        File.WriteAllText(Path.Combine(targetPath, "app.csproj"), "<Project></Project>");
        return Task.CompletedTask;
    }

    public Task CleanupAsync(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
        return Task.CompletedTask;
    }
}

public class FakeProjectScannerForApiTests : IProjectScanner
{
    public Task<ScanResult?> ScanAsync(string repoPath)
        => Task.FromResult<ScanResult?>(new ScanResult
        {
            ProjectType = KestrelHub.Shared.Enums.ProjectType.SingleProject,
            PrimaryProjectPath = "app.csproj",
            DotNetVersion = "net10.0"
        });
}

public class FakeDockerfileGeneratorForApiTests : IDockerfileGenerator
{
    public string? Generate(ScanResult scan, string repoPath) => "FROM scratch";
}

public class FakePortAllocatorForApiTests : IPortAllocator
{
    public Task<int> AllocateNextPortAsync() => Task.FromResult(8100);
}
