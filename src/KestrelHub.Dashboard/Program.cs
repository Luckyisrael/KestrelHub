using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using KestrelHub.Dashboard.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<KestrelHub.Dashboard.App>("#app");

// MudBlazor
builder.Services.AddMudServices();

// HttpClient pointing to API
builder.Services.AddScoped(sp =>
{
    var http = new HttpClient { BaseAddress = new Uri("http://localhost:5001") };
    return http;
});

// Auth
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());

await builder.Build().RunAsync();
