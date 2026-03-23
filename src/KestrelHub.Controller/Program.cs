using KestrelHub.Controller.Data;
using KestrelHub.Controller.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IDeploymentRepository, DeploymentRepository>();
builder.Services.AddScoped<IGitService, GitService>();
builder.Services.AddScoped<IProjectScanner, ProjectScanner>();
builder.Services.AddScoped<IDockerfileGenerator, DockerfileGenerator>();
builder.Services.AddScoped<IDockerService, DockerService>();
builder.Services.AddScoped<IPortAllocator, PortAllocator>();
builder.Services.AddScoped<IDeploymentOrchestrator, DeploymentOrchestrator>();
builder.Services.AddSingleton<IDeploymentQueue, DeploymentQueue>();
builder.Services.AddHostedService<DeploymentQueueHostedService>();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

// Make Program accessible for WebApplicationFactory in tests
public partial class Program { }
