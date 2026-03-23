using KestrelHub.Proxy.Data;
using KestrelHub.Proxy.Services;
using Microsoft.EntityFrameworkCore;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<ProxyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Services
builder.Services.AddScoped<IRouteStore, RouteStore>();
builder.Services.AddSingleton<DynamicProxyConfigProvider>();
builder.Services.AddSingleton<IProxyConfigProvider>(sp => sp.GetRequiredService<DynamicProxyConfigProvider>());

builder.Services.AddControllers();
builder.Services.AddReverseProxy();

var app = builder.Build();

app.MapControllers();
app.MapReverseProxy();

// Make Program accessible for tests
public partial class Program { }
