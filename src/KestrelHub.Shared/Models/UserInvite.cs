namespace KestrelHub.Shared.Models;

public class UserInvite
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Viewer";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }

    public bool IsValid => UsedAt is null && ExpiresAt > DateTime.UtcNow;
}
