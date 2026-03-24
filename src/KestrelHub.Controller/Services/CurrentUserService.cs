using System.Security.Claims;

namespace KestrelHub.Controller.Services;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? Email { get; }
    bool IsAdmin { get; }
    bool IsDeveloper { get; }
    bool IsAuthenticated { get; }
}

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value;
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
    public bool IsAdmin => User?.IsInRole("Admin") ?? false;
    public bool IsDeveloper => User?.IsInRole("Developer") ?? false || IsAdmin;
}
