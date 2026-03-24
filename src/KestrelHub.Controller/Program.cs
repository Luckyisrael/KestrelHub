using System.Text;
using System.Threading.RateLimiting;
using KestrelHub.Controller.Data;
using KestrelHub.Controller.Middleware;
using KestrelHub.Controller.Services;
using KestrelHub.Shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity with strict password policy
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

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is required and must be at least 32 characters.");

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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "KestrelHub",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "KestrelHub",
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
builder.Services.AddScoped<IGitService, GitService>();
builder.Services.AddScoped<IProjectScanner, ProjectScanner>();
builder.Services.AddScoped<IDockerfileGenerator, DockerfileGenerator>();
builder.Services.AddScoped<IDockerService, DockerService>();
builder.Services.AddScoped<IPortAllocator, PortAllocator>();
builder.Services.AddScoped<IDeploymentOrchestrator, DeploymentOrchestrator>();
builder.Services.AddHttpClient<IRouteService, RouteService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["KestrelHub:ProxyUrl"] ?? "http://localhost:5002");
});
builder.Services.AddSingleton<IDeploymentQueue, DeploymentQueue>();
builder.Services.AddHostedService<DeploymentQueueHostedService>();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 0;
    });
    options.RejectionStatusCode = 429;
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseMiddleware<SetupGuardMiddleware>();
app.UseAuthorization();
app.UseMiddleware<ActiveUserMiddleware>();
app.UseRateLimiter();

app.MapControllers();

app.Run();

public partial class Program { }
