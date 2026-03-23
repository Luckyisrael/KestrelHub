using KestrelHub.Proxy.Services;
using KestrelHub.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace KestrelHub.Proxy.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoutesController : ControllerBase
{
    private readonly IRouteStore _routeStore;
    private readonly DynamicProxyConfigProvider _configProvider;

    public RoutesController(IRouteStore routeStore, DynamicProxyConfigProvider configProvider)
    {
        _routeStore = routeStore;
        _configProvider = configProvider;
    }

    [HttpPost]
    public async Task<IActionResult> AddRoute([FromBody] AddRouteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Domain))
            return BadRequest("Domain is required.");

        if (request.TargetPort <= 0)
            return BadRequest("TargetPort must be positive.");

        var entry = await _routeStore.AddRouteAsync(new RouteEntry
        {
            Domain = request.Domain,
            TargetPort = request.TargetPort,
            DeploymentId = request.DeploymentId
        });

        await _configProvider.ReloadAsync();

        return Created($"/api/routes/{entry.Id}", entry);
    }

    [HttpDelete("{deploymentId:guid}")]
    public async Task<IActionResult> RemoveRoute(Guid deploymentId)
    {
        var removed = await _routeStore.RemoveRouteAsync(deploymentId);
        if (!removed)
            return NotFound();

        await _configProvider.ReloadAsync();

        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var routes = await _routeStore.GetAllActiveRoutesAsync();
        return Ok(routes);
    }
}

public record AddRouteRequest(
    string Domain,
    int TargetPort,
    Guid DeploymentId);
