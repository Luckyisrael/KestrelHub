using KestrelHub.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace KestrelHub.Controller.Data;

public class ApplicationDbContext : IdentityDbContext<KestrelHubUser, KestrelHubRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    public DbSet<AppDeployment> AppDeployments => Set<AppDeployment>();
    public DbSet<ContainerInfo> ContainerInfos => Set<ContainerInfo>();
    public DbSet<DeploymentLog> DeploymentLogs => Set<DeploymentLog>();
    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Secret> Secrets => Set<Secret>();
    public DbSet<SecretAuditLog> SecretAuditLogs => Set<SecretAuditLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<UserInvite> UserInvites => Set<UserInvite>();

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
                InstanceId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                InstalledAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
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

        modelBuilder.Entity<Secret>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(200);
            entity.Property(e => e.EncryptedValue).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Environment).HasMaxLength(50);
            entity.HasIndex(e => e.DeploymentId);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        modelBuilder.Entity<SecretAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ActorUserId).HasMaxLength(100);
            entity.Property(e => e.ActorEmail).HasMaxLength(200);
            entity.HasIndex(e => e.SecretId);
        });
    }
}
