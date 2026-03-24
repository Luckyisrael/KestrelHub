namespace KestrelHub.Shared.Models;

public class SystemSettings
{
    public Guid Id { get; set; }
    public bool IsSetupComplete { get; set; }
    public Guid InstanceId { get; set; }
    public DateTime InstalledAt { get; set; }
}
