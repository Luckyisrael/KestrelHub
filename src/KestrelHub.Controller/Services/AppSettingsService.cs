using KestrelHub.Controller.Data;
using KestrelHub.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KestrelHub.Controller.Services;

public interface IAppSettingsService
{
    Task<List<AppSetting>> GetSettingsAsync(Guid deploymentId);
    Task<AppSetting> SetSettingAsync(Guid deploymentId, string key, string value);
    Task<bool> DeleteSettingAsync(Guid id);
}

public class AppSettingsService : IAppSettingsService
{
    private readonly ApplicationDbContext _context;

    public AppSettingsService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AppSetting>> GetSettingsAsync(Guid deploymentId)
    {
        return await _context.AppSettings
            .Where(s => s.DeploymentId == deploymentId)
            .OrderBy(s => s.Key)
            .ToListAsync();
    }

    public async Task<AppSetting> SetSettingAsync(Guid deploymentId, string key, string value)
    {
        var existing = await _context.AppSettings
            .FirstOrDefaultAsync(s => s.DeploymentId == deploymentId && s.Key == key);

        if (existing is not null)
        {
            existing.Value = value;
            existing.AppliedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return existing;
        }

        var setting = new AppSetting
        {
            Id = Guid.NewGuid(),
            DeploymentId = deploymentId,
            Key = key,
            Value = value,
            AppliedAt = DateTime.UtcNow
        };

        _context.AppSettings.Add(setting);
        await _context.SaveChangesAsync();
        return setting;
    }

    public async Task<bool> DeleteSettingAsync(Guid id)
    {
        var setting = await _context.AppSettings.FindAsync(id);
        if (setting is null)
            return false;

        _context.AppSettings.Remove(setting);
        await _context.SaveChangesAsync();
        return true;
    }
}
