namespace KestrelHub.Shared.Models;

public class AppSetting
{
    public Guid Id { get; set; }
    public Guid DeploymentId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; }
}
