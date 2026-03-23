namespace KestrelHub.Shared.Models;

public class ContainerInfo
{
    public string ContainerId { get; set; } = string.Empty;
    public Guid AppDeploymentId { get; set; }
    public string ImageTag { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Status { get; set; } = string.Empty;
}
