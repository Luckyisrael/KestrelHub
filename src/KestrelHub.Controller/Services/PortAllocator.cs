using KestrelHub.Controller.Data;
using Microsoft.EntityFrameworkCore;

namespace KestrelHub.Controller.Services;

public interface IPortAllocator
{
    Task<int> AllocateNextPortAsync();
}

public class PortAllocator : IPortAllocator
{
    private const int StartPort = 8100;
    private readonly ApplicationDbContext _context;

    public PortAllocator(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> AllocateNextPortAsync()
    {
        var usedPorts = await _context.ContainerInfos
            .Select(c => c.Port)
            .Distinct()
            .OrderBy(p => p)
            .ToListAsync();

        var port = StartPort;
        foreach (var used in usedPorts)
        {
            if (used == port)
                port++;
            else if (used > port)
                break;
        }

        return port;
    }
}
