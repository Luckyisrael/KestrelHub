using KestrelHub.Shared.Enums;
using KestrelHub.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KestrelHub.Controller.Data;

public interface IDeploymentRepository
{
    Task<AppDeployment> CreateAsync(AppDeployment deployment);
    Task<AppDeployment?> GetByIdAsync(Guid id);
    Task<List<AppDeployment>> GetAllAsync();
    Task<AppDeployment?> UpdateStatusAsync(Guid id, DeploymentStatus status);
}

public class DeploymentRepository : IDeploymentRepository
{
    private readonly ApplicationDbContext _context;

    public DeploymentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AppDeployment> CreateAsync(AppDeployment deployment)
    {
        deployment.Id = Guid.NewGuid();
        deployment.CreatedAt = DateTime.UtcNow;
        deployment.UpdatedAt = DateTime.UtcNow;

        _context.AppDeployments.Add(deployment);
        await _context.SaveChangesAsync();
        return deployment;
    }

    public async Task<AppDeployment?> GetByIdAsync(Guid id)
    {
        return await _context.AppDeployments.FindAsync(id);
    }

    public async Task<List<AppDeployment>> GetAllAsync()
    {
        return await _context.AppDeployments
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<AppDeployment?> UpdateStatusAsync(Guid id, DeploymentStatus status)
    {
        var deployment = await _context.AppDeployments.FindAsync(id);
        if (deployment is null)
            return null;

        deployment.Status = status;
        deployment.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return deployment;
    }
}
