using System.Text;
using KestrelHub.Controller.Data;
using KestrelHub.Controller.Services;
using KestrelHub.Shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

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

        // JWT config for tests
        builder.Configuration["Jwt:Secret"] = "Test-Secret-Key-Min32CharactersLong!";
        builder.Configuration["Jwt:Issuer"] = "KestrelHub";
        builder.Configuration["Jwt:Audience"] = "KestrelHub";
        builder.Configuration["Jwt:AccessTokenMinutes"] = "15";
        builder.Configuration["Jwt:RefreshTokenDays"] = "7";

        builder.Services.AddSingleton(_sqliteConnection);
        builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseSqlite(sp.GetRequiredService<SqliteConnection>());
        });

        // Identity
        builder.Services.AddIdentity<KestrelHubUser, KestrelHubRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 12;
            options.Password.RequiredUniqueChars = 4;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.AllowedForNewUsers = true;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // JWT Auth
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("Test-Secret-Key-Min32CharactersLong!")),
                ValidateIssuer = true,
                ValidIssuer = "KestrelHub",
                ValidateAudience = true,
                ValidAudience = "KestrelHub",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
            options.AddPolicy("DeveloperOrAbove", policy => policy.RequireRole("Admin", "Developer"));
            options.AddPolicy("AnyAuthenticatedUser", policy => policy.RequireAuthenticatedUser());
        });

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
        builder.Services.AddScoped<IJwtService, JwtService>();
        builder.Services.AddScoped<IDeploymentRepository, DeploymentRepository>();
        builder.Services.AddScoped<IGitService, FakeGitServiceForApiTests>();
        builder.Services.AddScoped<IProjectScanner, FakeProjectScannerForApiTests>();
        builder.Services.AddScoped<IDockerfileGenerator, FakeDockerfileGeneratorForApiTests>();
        builder.Services.AddScoped<IDockerService, FakeDockerServiceForApiTests>();
        builder.Services.AddScoped<IPortAllocator, FakePortAllocatorForApiTests>();
        builder.Services.AddScoped<IRouteService, FakeRouteServiceForApiTests>();
        builder.Services.AddScoped<IDeploymentOrchestrator, DeploymentOrchestrator>();
        builder.Services.AddSingleton<IDeploymentQueue, DeploymentQueue>();
        builder.Services.AddHostedService<DeploymentQueueHostedService>();
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(Program).Assembly);

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
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

public class FakeRouteServiceForApiTests : IRouteService
{
    public Task<string> AssignDomainAsync(AppDeployment deployment, int port)
        => Task.FromResult($"{deployment.Name}.apps.localhost");

    public Task RemoveRouteAsync(Guid deploymentId) => Task.CompletedTask;
}
