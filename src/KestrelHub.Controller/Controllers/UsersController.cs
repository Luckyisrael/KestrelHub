using System.Security.Cryptography;
using KestrelHub.Controller.Data;
using KestrelHub.Controller.Services;
using KestrelHub.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KestrelHub.Controller.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class UsersController : ControllerBase
{
    private readonly UserManager<KestrelHubUser> _userManager;
    private readonly ApplicationDbContext _context;

    public UsersController(UserManager<KestrelHubUser> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _userManager.Users.ToListAsync();
        var result = new List<object>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new
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

        return Ok(result);
    }

    [HttpPost("invite")]
    public async Task<IActionResult> Invite([FromBody] InviteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("Email is required.");

        // Check if user already exists
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            return Conflict("User with this email already exists.");

        // Generate invite token
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var tokenHash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));

        var invite = new UserInvite
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            TokenHash = Convert.ToBase64String(tokenHash),
            Role = request.Role ?? "Viewer",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(48)
        };

        _context.UserInvites.Add(invite);
        await _context.SaveChangesAsync();

        return Ok(new { InviteToken = rawToken, invite.ExpiresAt });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        // Cannot deactivate yourself
        if (user.Id == User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value)
        {
            if (request.IsActive == false)
                return BadRequest("Cannot deactivate your own account.");
        }

        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
            user.DisplayName = request.DisplayName;

        await _userManager.UpdateAsync(user);

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, request.Role);
        }

        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        // Cannot delete yourself
        if (user.Id == User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value)
            return BadRequest("Cannot delete your own account.");

        await _userManager.DeleteAsync(user);
        return NoContent();
    }
}

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class InviteController : ControllerBase
{
    private readonly UserManager<KestrelHubUser> _userManager;
    private readonly ApplicationDbContext _context;

    public InviteController(UserManager<KestrelHubUser> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    [HttpGet("validate")]
    public async Task<IActionResult> Validate([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest("Token is required.");

        var tokenHash = Convert.ToBase64String(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

        var invite = await _context.UserInvites
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash);

        if (invite is null || !invite.IsValid)
            return NotFound(new { Error = "Invalid or expired invite." });

        return Ok(new { invite.Email, invite.Role });
    }

    [HttpPost("accept")]
    public async Task<IActionResult> Accept([FromBody] AcceptInviteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Token and password are required.");

        var tokenHash = Convert.ToBase64String(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(request.Token)));

        var invite = await _context.UserInvites
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash);

        if (invite is null || !invite.IsValid)
            return NotFound(new { Error = "Invalid or expired invite." });

        var user = new KestrelHubUser
        {
            UserName = invite.Email,
            Email = invite.Email,
            DisplayName = request.DisplayName ?? invite.Email.Split('@')[0],
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });

        await _userManager.AddToRoleAsync(user, invite.Role);

        invite.UsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Account created successfully." });
    }
}

public record InviteRequest(string Email, string? Role);
public record UpdateUserRequest(string? DisplayName, bool? IsActive, string? Role);
public record AcceptInviteRequest(string Token, string Password, string? DisplayName);
