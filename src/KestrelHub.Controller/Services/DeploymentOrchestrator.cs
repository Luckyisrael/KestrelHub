using KestrelHub.Controller.Data;
using KestrelHub.Shared.Enums;
using KestrelHub.Shared.Models;

namespace KestrelHub.Controller.Services;

public interface IDeploymentOrchestrator
{
    Task RunDeploymentAsync(Guid deploymentId);
}

public class DeploymentOrchestrator : IDeploymentOrchestrator
{
    private readonly IDeploymentRepository _repository;
    private readonly IGitService _gitService;
    private readonly IProjectScanner _projectScanner;
    private readonly IDockerfileGenerator _dockerfileGenerator;
    private readonly IDockerService _dockerService;
    private readonly IPortAllocator _portAllocator;
    private readonly ApplicationDbContext _context;

    private const string BuildsBasePath = "/tmp/kestrelhub-builds";

    public DeploymentOrchestrator(
        IDeploymentRepository repository,
        IGitService gitService,
        IProjectScanner projectScanner,
        IDockerfileGenerator dockerfileGenerator,
        IDockerService dockerService,
        IPortAllocator portAllocator,
        ApplicationDbContext context)
    {
        _repository = repository;
        _gitService = gitService;
        _projectScanner = projectScanner;
        _dockerfileGenerator = dockerfileGenerator;
        _dockerService = dockerService;
        _portAllocator = portAllocator;
        _context = context;
    }

    public async Task RunDeploymentAsync(Guid deploymentId)
    {
        var deployment = await _repository.GetByIdAsync(deploymentId);
        if (deployment is null)
            return;

        var buildPath = Path.Combine(BuildsBasePath, deploymentId.ToString());

        try
        {
            // Step 1: Clone repo → Building
            await LogAsync(deploymentId, "Cloning repository...");
            await _gitService.CloneAsync(deployment.GitUrl, deployment.Branch, buildPath);
            await LogAsync(deploymentId, "Repository cloned successfully.");

            // Step 2: Scan project
            await LogAsync(deploymentId, "Scanning project...");
            var scanResult = await _projectScanner.ScanAsync(buildPath);
            if (scanResult is null || scanResult.ProjectType is ProjectType.Unknown)
            {
                await FailDeploymentAsync(deploymentId, "No .NET project found in repository.");
                return;
            }
            await LogAsync(deploymentId, $"Project type: {scanResult.ProjectType}, TFM: {scanResult.DotNetVersion}");

            // Step 3: Generate Dockerfile if needed
            var dockerfile = _dockerfileGenerator.Generate(scanResult, buildPath);
            if (dockerfile is not null)
            {
                await LogAsync(deploymentId, "Generated Dockerfile.");
                var dockerfilePath = Path.Combine(buildPath, "Dockerfile");
                await File.WriteAllTextAsync(dockerfilePath, dockerfile);
            }
            else
            {
                await LogAsync(deploymentId, "Using existing Dockerfile from repository.");
            }

            // Step 4: Build image
            await _repository.UpdateStatusAsync(deploymentId, DeploymentStatus.Building);
            var imageTag = $"kestrelhub/{deployment.Name}:{deploymentId.ToString()[..8]}".ToLowerInvariant();
            var dockerfilePathFull = Path.Combine(buildPath, "Dockerfile");

            await LogAsync(deploymentId, $"Building Docker image: {imageTag}");
            var buildLogs = new List<string>();
            var progress = new Progress<string>(msg => buildLogs.Add(msg));
            await _dockerService.BuildImageAsync(buildPath, dockerfilePathFull, imageTag, progress);
            foreach (var msg in buildLogs)
                await LogAsync(deploymentId, msg);
            await LogAsync(deploymentId, "Docker image built successfully.");

            // Step 5: Run container
            var hostPort = await _portAllocator.AllocateNextPortAsync();
            await LogAsync(deploymentId, $"Allocated port: {hostPort}");

            var containerInfo = await _dockerService.RunContainerAsync(
                imageTag,
                hostPort,
                containerPort: 8080,
                new Dictionary<string, string>());

            containerInfo.AppDeploymentId = deploymentId;
            _context.ContainerInfos.Add(containerInfo);
            await _context.SaveChangesAsync();

            // Step 6: Update status → Running
            await _repository.UpdateStatusAsync(deploymentId, DeploymentStatus.Running);
            await LogAsync(deploymentId, $"Deployment running on port {hostPort}.");
        }
        catch (Exception ex)
        {
            await FailDeploymentAsync(deploymentId, $"Deployment failed: {ex.Message}");
        }
        finally
        {
            // Step 8: Always cleanup
            try
            {
                await _gitService.CleanupAsync(buildPath);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    private async Task FailDeploymentAsync(Guid deploymentId, string message)
    {
        await LogAsync(deploymentId, message, isError: true);
        await _repository.UpdateStatusAsync(deploymentId, DeploymentStatus.Failed);
    }

    private async Task LogAsync(Guid deploymentId, string message, bool isError = false)
    {
        var log = new DeploymentLog
        {
            Id = Guid.NewGuid(),
            DeploymentId = deploymentId,
            Timestamp = DateTime.UtcNow,
            Message = message,
            IsError = isError
        };

        _context.DeploymentLogs.Add(log);
        await _context.SaveChangesAsync();
    }

}
