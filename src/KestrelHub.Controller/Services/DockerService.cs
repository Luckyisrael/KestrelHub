using Docker.DotNet;
using Docker.DotNet.Models;
using KestrelHub.Shared.Models;
using JSONMessage = Docker.DotNet.Models.JSONMessage;

namespace KestrelHub.Controller.Services;

public interface IDockerService
{
    Task BuildImageAsync(string contextPath, string dockerfilePath, string imageTag, IProgress<string> progress);
    Task<ContainerInfo> RunContainerAsync(string imageTag, int hostPort, int containerPort, Dictionary<string, string> envVars);
    Task StopContainerAsync(string containerId);
    Task<string> GetContainerStatusAsync(string containerId);
}

public class DockerService : IDockerService
{
    private readonly DockerClient _client;

    public DockerService()
    {
        _client = new DockerClientConfiguration()
            .CreateClient();
    }

    public async Task BuildImageAsync(string contextPath, string dockerfilePath, string imageTag, IProgress<string> progress)
    {
        var tarPath = await CreateTarballAsync(contextPath);
        try
        {
            using var stream = File.OpenRead(tarPath);
            var parameters = new ImageBuildParameters
            {
                Tags = [imageTag],
                Dockerfile = dockerfilePath.Replace(contextPath, "").TrimStart('\\').TrimStart('/'),
            };

            var jsonProgress = new Progress<JSONMessage>(msg => progress.Report(msg.Stream ?? ""));

            await _client.Images.BuildImageFromDockerfileAsync(
                parameters,
                stream,
                null,
                null,
                jsonProgress,
                CancellationToken.None);
        }
        finally
        {
            if (File.Exists(tarPath))
                File.Delete(tarPath);
        }
    }

    public async Task<ContainerInfo> RunContainerAsync(string imageTag, int hostPort, int containerPort, Dictionary<string, string> envVars)
    {
        var envList = envVars.Select(kv => $"{kv.Key}={kv.Value}").ToList();

        var createParams = new CreateContainerParameters
        {
            Image = imageTag,
            Env = envList,
            HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    [$"{containerPort}/tcp"] = new List<PortBinding>
                    {
                        new() { HostPort = hostPort.ToString() }
                    }
                }
            },
            ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                [$"{containerPort}/tcp"] = default
            }
        };

        var createResponse = await _client.Containers.CreateContainerAsync(createParams);
        await _client.Containers.StartContainerAsync(createResponse.ID, new ContainerStartParameters());

        var inspect = await _client.Containers.InspectContainerAsync(createResponse.ID);

        return new ContainerInfo
        {
            ContainerId = createResponse.ID,
            ImageTag = imageTag,
            Port = hostPort,
            Status = inspect.State?.Status ?? "unknown"
        };
    }

    public async Task StopContainerAsync(string containerId)
    {
        try
        {
            await _client.Containers.StopContainerAsync(containerId, new ContainerStopParameters());
            await _client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true });
        }
        catch (DockerContainerNotFoundException)
        {
            // Container already gone
        }
    }

    public async Task<string> GetContainerStatusAsync(string containerId)
    {
        try
        {
            var inspect = await _client.Containers.InspectContainerAsync(containerId);
            return inspect.State?.Status ?? "unknown";
        }
        catch (DockerContainerNotFoundException)
        {
            return "missing";
        }
    }

    private static Task<string> CreateTarballAsync(string directory)
    {
        return Task.Run(() =>
        {
            var tarPath = Path.Combine(Path.GetTempPath(), $"kestrelhub-{Guid.NewGuid():N}.tar");
            var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);

            using var fs = new FileStream(tarPath, FileMode.Create);
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(directory, file);
                var header = new byte[512];
                var nameBytes = System.Text.Encoding.ASCII.GetBytes(relativePath.Replace('\\', '/'));
                Array.Copy(nameBytes, 0, header, 0, Math.Min(nameBytes.Length, 100));

                // file mode (octal 0100644)
                header[100] = (byte)'0';
                header[101] = (byte)'1';
                header[102] = (byte)'0';
                header[103] = (byte)'0';
                header[104] = (byte)'6';
                header[105] = (byte)'4';
                header[106] = (byte)'4';

                // size
                var size = new FileInfo(file).Length;
                var sizeOctal = Convert.ToString(size, 8).PadLeft(11, '0');
                var sizeBytes = System.Text.Encoding.ASCII.GetBytes(sizeOctal);
                Array.Copy(sizeBytes, 0, header, 124, 11);

                // checksum placeholder
                header[148] = (byte)' ';

                // calculate checksum
                long checksum = 0;
                for (int i = 0; i < 512; i++) checksum += header[i];
                var checksumOctal = Convert.ToString(checksum, 8).PadLeft(7, '0');
                var checksumBytes = System.Text.Encoding.ASCII.GetBytes(checksumOctal);
                Array.Copy(checksumBytes, 0, header, 148, 7);

                fs.Write(header, 0, 512);

                // file content
                var content = File.ReadAllBytes(file);
                fs.Write(content, 0, content.Length);

                // pad to 512-byte boundary
                var remainder = content.Length % 512;
                if (remainder > 0)
                    fs.Write(new byte[512 - remainder], 0, 512 - remainder);
            }

            // two empty 512-byte blocks to signal end of archive
            fs.Write(new byte[1024], 0, 1024);

            return tarPath;
        });
    }
}
