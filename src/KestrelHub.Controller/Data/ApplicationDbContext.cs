using KestrelHub.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KestrelHub.Controller.Data;

public class ApplicationDbContext : IdentityDbContext<KestrelHubUser, KestrelHubRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppDeployment> AppDeployments => Set<AppDeployment>();
    public DbSet<ContainerInfo> ContainerInfos => Set<ContainerInfo>();
    public DbSet<DeploymentLog> DeploymentLogs => Set<DeploymentLog>();
    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Seed roles
        modelBuilder.Entity<KestrelHubRole>().HasData(
            new KestrelHubRole { Id = "1", Name = "Admin", NormalizedName = "ADMIN" },
            new KestrelHubRole { Id = "2", Name = "Developer", NormalizedName = "DEVELOPER" },
            new KestrelHubRole { Id = "3", Name = "Viewer", NormalizedName = "VIEWER" }
        );

        // Seed SystemSettings (single row)
        modelBuilder.Entity<SystemSettings>().HasData(
            new SystemSettings
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                IsSetupComplete = false,
                InstanceId = Guid.NewGuid(),
                InstalledAt = DateTime.UtcNow
            }
        );

        modelBuilder.Entity<KestrelHubUser>(entity =>
        {
            entity.Property(e => e.DisplayName).HasMaxLength(200);
        });

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

        modelBuilder.Entity<SystemSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TokenHash).IsRequired().HasMaxLength(128);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UserId);
        });
    }
}
