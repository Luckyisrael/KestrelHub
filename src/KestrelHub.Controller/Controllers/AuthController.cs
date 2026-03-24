using KestrelHub.Controller.Data;
using KestrelHub.Controller.DTOs;
using KestrelHub.Controller.Services;
using KestrelHub.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace KestrelHub.Controller.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<KestrelHubUser> _userManager;
    private readonly SignInManager<KestrelHubUser> _signInManager;
    private readonly IJwtService _jwtService;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(
        UserManager<KestrelHubUser> userManager,
        SignInManager<KestrelHubUser> signInManager,
        IJwtService jwtService,
        ApplicationDbContext context,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtService = jwtService;
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // Generic error message — no user enumeration
        var genericError = "Invalid email or password.";

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized(new { Error = genericError });

        if (!user.IsActive)
            return Unauthorized(new { Error = genericError });

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
                return Unauthorized(new { Error = "Account is locked. Please try again later." });
            return Unauthorized(new { Error = genericError });
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        // Generate tokens
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtService.GenerateAccessToken(user, roles);
        var refreshToken = _jwtService.GenerateRefreshToken();

        // Store refresh token hash
        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = JwtService.HashToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(_configuration["Jwt:RefreshTokenDays"] ?? "7")),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        _context.Set<RefreshToken>().Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        // Set refresh token as HttpOnly cookie
        SetRefreshTokenCookie(refreshToken);

        return Ok(new { AccessToken = accessToken });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(new { Error = "Invalid token." });

        var tokenHash = JwtService.HashToken(refreshToken);

        var storedToken = await _context.Set<RefreshToken>()
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (storedToken is null || !storedToken.IsActive)
        {
            // Theft detection — revoke all user tokens if a revoked token is reused
            if (storedToken is not null)
            {
                var userTokens = await _context.Set<RefreshToken>()
                    .Where(t => t.UserId == storedToken.UserId && t.RevokedAt == null)
                    .ToListAsync();

                foreach (var t in userTokens)
                    t.RevokedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }

            return Unauthorized(new { Error = "Invalid token." });
        }

        // Rotate token
        var newRefreshToken = _jwtService.GenerateRefreshToken();
        var newTokenHash = JwtService.HashToken(newRefreshToken);

        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.RevokedByIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var newRefreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = storedToken.UserId,
            TokenHash = newTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(_configuration["Jwt:RefreshTokenDays"] ?? "7")),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        storedToken.ReplacedByTokenId = newRefreshTokenEntity.Id;

        _context.Set<RefreshToken>().Add(newRefreshTokenEntity);
        await _context.SaveChangesAsync();

        // Generate new access token
        var user = await _userManager.FindByIdAsync(storedToken.UserId);
        if (user is null || !user.IsActive)
            return Unauthorized(new { Error = "Invalid token." });

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtService.GenerateAccessToken(user, roles);

        SetRefreshTokenCookie(newRefreshToken);

        return Ok(new { AccessToken = accessToken });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var tokenHash = JwtService.HashToken(refreshToken);
            var storedToken = await _context.Set<RefreshToken>()
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

            if (storedToken is not null)
            {
                storedToken.RevokedAt = DateTime.UtcNow;
                storedToken.RevokedByIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _context.SaveChangesAsync();
            }
        }

        Response.Cookies.Delete("refreshToken");

        return Ok(new { Message = "Logged out." });
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is null)
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt,
            Roles = roles
        });
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(int.Parse(_configuration["Jwt:RefreshTokenDays"] ?? "7"))
        });
    }
}
