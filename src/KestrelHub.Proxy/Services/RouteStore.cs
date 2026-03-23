using KestrelHub.Shared.Models;
using KestrelHub.Proxy.Data;
using Microsoft.EntityFrameworkCore;

namespace KestrelHub.Proxy.Services;

public interface IRouteStore
{
    Task<RouteEntry> AddRouteAsync(RouteEntry entry);
    Task<bool> RemoveRouteAsync(Guid deploymentId);
    Task<List<RouteEntry>> GetAllActiveRoutesAsync();
}

public class RouteStore : IRouteStore
{
    private readonly ProxyDbContext _context;

    public RouteStore(ProxyDbContext context)
    {
        _context = context;
    }

    public async Task<RouteEntry> AddRouteAsync(RouteEntry entry)
    {
        entry.Id = Guid.NewGuid();
        entry.IsActive = true;
        entry.CreatedAt = DateTime.UtcNow;

        _context.RouteEntries.Add(entry);
        await _context.SaveChangesAsync();
        return entry;
    }

    public async Task<bool> RemoveRouteAsync(Guid deploymentId)
    {
        var route = await _context.RouteEntries
            .FirstOrDefaultAsync(r => r.DeploymentId == deploymentId && r.IsActive);

        if (route is null)
            return false;

        route.IsActive = false;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<RouteEntry>> GetAllActiveRoutesAsync()
    {
        return await _context.RouteEntries
            .Where(r => r.IsActive)
            .ToListAsync();
    }
}
