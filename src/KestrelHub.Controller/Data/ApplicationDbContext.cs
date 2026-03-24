using KestrelHub.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace KestrelHub.Controller.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppDeployment> AppDeployments => Set<AppDeployment>();
    public DbSet<ContainerInfo> ContainerInfos => Set<ContainerInfo>();
    public DbSet<DeploymentLog> DeploymentLogs => Set<DeploymentLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppDeployment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.GitUrl).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Branch).HasMaxLength(100);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.AssignedDomain).HasMaxLength(300);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<ContainerInfo>(entity =>
        {
            entity.HasKey(e => e.ContainerId);
            entity.Property(e => e.ContainerId).HasMaxLength(100);
            entity.Property(e => e.ImageTag).IsRequired().HasMaxLength(300);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.HasIndex(e => e.AppDeploymentId);
        });

        modelBuilder.Entity<DeploymentLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(2000);
            entity.HasIndex(e => e.DeploymentId);
        });
    }
}
