using KestrelHub.Controller.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KestrelHub.Controller.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AnyAuthenticatedUser")]
public class SecretsController : ControllerBase
{
    private readonly ISecretVaultService _secretVault;

    public SecretsController(ISecretVaultService secretVault)
    {
        _secretVault = secretVault;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? deploymentId)
    {
        var secrets = await _secretVault.GetAllSecretsAsync(deploymentId);
        return Ok(secrets);
    }

    [HttpPost]
    [Authorize(Policy = "DeveloperOrAbove")]
    public async Task<IActionResult> Create([FromBody] CreateSecretRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
            return BadRequest("Key is required.");

        var secret = await _secretVault.SetSecretAsync(
            request.DeploymentId, request.Key, request.Value, request.Environment ?? "Production");

        return Created($"/api/secrets/{secret.Id}", new { secret.Id, secret.Key, secret.Environment });
    }

    [HttpGet("{id:guid}/value")]
    public async Task<IActionResult> GetValue(Guid id)
    {
        var value = await _secretVault.GetSecretAsync(id);
        if (value is null)
            return NotFound();

        return Ok(new { Value = value });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "DeveloperOrAbove")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _secretVault.DeleteSecretAsync(id);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    [HttpGet("{id:guid}/audit")]
    public async Task<IActionResult> GetAuditLogs(Guid id)
    {
        var logs = await _secretVault.GetAuditLogsAsync(id);
        return Ok(logs);
    }
}

public record CreateSecretRequest(Guid? DeploymentId, string Key, string Value, string? Environment);
