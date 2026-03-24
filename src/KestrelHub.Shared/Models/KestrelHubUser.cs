using Microsoft.AspNetCore.Identity;

namespace KestrelHub.Shared.Models;

public class KestrelHubUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
}
