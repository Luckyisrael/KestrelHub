using KestrelHub.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KestrelHub.Proxy.Data;

public class ProxyDbContext : DbContext
{
    public ProxyDbContext(DbContextOptions<ProxyDbContext> options)
        : base(options)
    {
    }

    public DbSet<RouteEntry> RouteEntries => Set<RouteEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RouteEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Domain).IsRequired().HasMaxLength(300);
            entity.HasIndex(e => e.Domain);
            entity.HasIndex(e => e.DeploymentId);
        });
    }
}
