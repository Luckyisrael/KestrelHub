namespace KestrelHub.Shared.Models;

public class SecretAuditLog
{
    public Guid Id { get; set; }
    public Guid SecretId { get; set; }
    public SecretAction Action { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string ActorEmail { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
