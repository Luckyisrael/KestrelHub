using KestrelHub.Controller.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KestrelHub.Controller.Controllers;

[ApiController]
[Route("api/deployments/{deploymentId:guid}/settings")]
[Authorize(Policy = "AnyAuthenticatedUser")]
public class AppSettingsController : ControllerBase
{
    private readonly IAppSettingsService _appSettings;

    public AppSettingsController(IAppSettingsService appSettings)
    {
        _appSettings = appSettings;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid deploymentId)
    {
        var settings = await _appSettings.GetSettingsAsync(deploymentId);
        return Ok(settings);
    }

    [HttpPut]
    [Authorize(Policy = "DeveloperOrAbove")]
    public async Task<IActionResult> Set(Guid deploymentId, [FromBody] SetSettingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
            return BadRequest("Key is required.");

        var setting = await _appSettings.SetSettingAsync(deploymentId, request.Key, request.Value ?? "");
        return Ok(setting);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "DeveloperOrAbove")]
    public async Task<IActionResult> Delete(Guid deploymentId, Guid id)
    {
        var deleted = await _appSettings.DeleteSettingAsync(id);
        if (!deleted)
            return NotFound();

        return NoContent();
    }
}

public record SetSettingRequest(string Key, string? Value);
