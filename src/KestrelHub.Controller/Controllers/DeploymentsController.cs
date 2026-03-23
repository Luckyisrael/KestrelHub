using KestrelHub.Controller.Data;
using KestrelHub.Controller.DTOs;
using KestrelHub.Controller.Services;
using KestrelHub.Shared.Enums;
using KestrelHub.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace KestrelHub.Controller.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeploymentsController : ControllerBase
{
    private readonly IDeploymentRepository _repository;
    private readonly IDeploymentQueue _queue;
    private readonly IDockerService _dockerService;
    private readonly ApplicationDbContext _context;

    public DeploymentsController(
        IDeploymentRepository repository,
        IDeploymentQueue queue,
        IDockerService dockerService,
        ApplicationDbContext context)
    {
        _repository = repository;
        _queue = queue;
        _dockerService = dockerService;
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDeploymentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.GitUrl))
            return ValidationProblem("GitUrl is required.");

        var deployment = await _repository.CreateAsync(new AppDeployment
        {
            Name = request.Name ?? ExtractRepoName(request.GitUrl),
            GitUrl = request.GitUrl,
            Branch = request.Branch
        });

        await _queue.EnqueueAsync(deployment.Id);

        return Accepted(new { deployment.Id });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var deployments = await _repository.GetAllAsync();
        return Ok(deployments);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var deployment = await _repository.GetByIdAsync(id);
        if (deployment is null)
            return NotFound();

        var logs = await _repository.GetLogsAsync(id);

        return Ok(new
        {
            deployment.Id,
            deployment.Name,
            deployment.GitUrl,
            deployment.Branch,
            deployment.Status,
            deployment.CreatedAt,
            deployment.UpdatedAt,
            Logs = logs
        });
    }

    [HttpPost("{id:guid}/stop")]
    public async Task<IActionResult> Stop(Guid id)
    {
        var deployment = await _repository.GetByIdAsync(id);
        if (deployment is null)
            return NotFound();

        if (deployment.Status is DeploymentStatus.Running)
        {
            var containers = _context.ContainerInfos
                .Where(c => c.AppDeploymentId == id)
                .ToList();

            foreach (var container in containers)
            {
                await _dockerService.StopContainerAsync(container.ContainerId);
                container.Status = "stopped";
            }
            await _context.SaveChangesAsync();
        }

        await _repository.UpdateStatusAsync(id, DeploymentStatus.Stopped);
        return Ok(new { Id = id, Status = "Stopped" });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deployment = await _repository.GetByIdAsync(id);
        if (deployment is null)
            return NotFound();

        if (deployment.Status is DeploymentStatus.Running)
        {
            var containers = _context.ContainerInfos
                .Where(c => c.AppDeploymentId == id)
                .ToList();

            foreach (var container in containers)
            {
                await _dockerService.StopContainerAsync(container.ContainerId);
                _context.ContainerInfos.Remove(container);
            }
            await _context.SaveChangesAsync();
        }

        _context.AppDeployments.Remove(deployment);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static string ExtractRepoName(string gitUrl)
    {
        var lastSegment = gitUrl.TrimEnd('/').Split('/').Last();
        return lastSegment.EndsWith(".git")
            ? lastSegment[..^4]
            : lastSegment;
    }
}
