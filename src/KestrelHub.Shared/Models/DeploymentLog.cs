namespace KestrelHub.Shared.Models;

public class DeploymentLog
{
    public Guid Id { get; set; }
    public Guid DeploymentId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsError { get; set; }
}
