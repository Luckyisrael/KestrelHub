namespace KestrelHub.Shared.Models;

public class RouteEntry
{
    public Guid Id { get; set; }
    public string Domain { get; set; } = string.Empty;
    public int TargetPort { get; set; }
    public Guid DeploymentId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
