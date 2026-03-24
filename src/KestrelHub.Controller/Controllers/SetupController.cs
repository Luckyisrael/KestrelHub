using KestrelHub.Controller.Data;
using KestrelHub.Controller.DTOs;
using KestrelHub.Controller.Services;
using KestrelHub.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KestrelHub.Controller.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SetupController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<KestrelHubUser> _userManager;
    private readonly RoleManager<KestrelHubRole> _roleManager;
    private readonly IJwtService _jwtService;

    public SetupController(
        ApplicationDbContext context,
        UserManager<KestrelHubUser> userManager,
        RoleManager<KestrelHubRole> roleManager,
        IJwtService jwtService)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtService = jwtService;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var settings = await _context.SystemSettings.FirstOrDefaultAsync();
        return Ok(new { IsSetupComplete = settings?.IsSetupComplete ?? false });
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete([FromBody] CompleteSetupRequest request)
    {
        // Can only run once
        var settings = await _context.SystemSettings.FirstOrDefaultAsync();
        if (settings is null || settings.IsSetupComplete)
            return NotFound();

        // Validate password strength (same rules as Identity config)
        if (string.IsNullOrWhiteSpace(request.AdminPassword) || request.AdminPassword.Length < 12)
            return BadRequest(new { Error = "Password must be at least 12 characters." });

        // Create admin user
        var admin = new KestrelHubUser
        {
            UserName = request.AdminEmail,
            Email = request.AdminEmail,
            DisplayName = request.AdminDisplayName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(admin, request.AdminPassword);
        if (!result.Succeeded)
            return BadRequest(new { Errors = result.Errors.Select(e => e.Description) });

        // Assign Admin role
        await _userManager.AddToRoleAsync(admin, "Admin");

        // Mark setup complete atomically
        settings.IsSetupComplete = true;
        await _context.SaveChangesAsync();

        // Log admin in immediately
        var roles = await _userManager.GetRolesAsync(admin);
        var accessToken = _jwtService.GenerateAccessToken(admin, roles);

        return Ok(new { AccessToken = accessToken });
    }
}
