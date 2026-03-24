using KestrelHub.Shared.Models;

namespace KestrelHub.Controller.Services;

public interface IRouteService
{
    Task<string> AssignDomainAsync(AppDeployment deployment, int port);
    Task RemoveRouteAsync(Guid deploymentId);
}

public class RouteService : IRouteService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseDomain;

    public RouteService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _baseDomain = configuration["KestrelHub:BaseDomain"] ?? "apps.localhost";
    }

    public async Task<string> AssignDomainAsync(AppDeployment deployment, int port)
    {
        var domain = $"{deployment.Name}.{_baseDomain}";

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/routes", new
            {
                Domain = domain,
                TargetPort = port,
                DeploymentId = deployment.Id
            });
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            // Route assignment failed — deployment still works, just not routed
        }

        return domain;
    }

    public async Task RemoveRouteAsync(Guid deploymentId)
    {
        try
        {
            await _httpClient.DeleteAsync($"/api/routes/{deploymentId}");
        }
        catch
        {
            // Best effort
        }
    }
}
