using KestrelHub.Shared.Enums;

namespace KestrelHub.Shared.Models;

public class AppDeployment
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string GitUrl { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
    public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;
    public string? AssignedDomain { get; set; }
    public int? AssignedPort { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
