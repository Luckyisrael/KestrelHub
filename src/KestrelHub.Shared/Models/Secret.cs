namespace KestrelHub.Shared.Models;

public class Secret
{
    public Guid Id { get; set; }
    public Guid? DeploymentId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string EncryptedValue { get; set; } = string.Empty;
    public string Environment { get; set; } = "Production";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public bool IsDeleted { get; set; }
}
