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

            using var fs = new FileStream(tarPath, FileMode.Create);

            void WriteTarEntry(string relativePath, byte[] content)
            {
                var header = new byte[512];
                var nameBytes = System.Text.Encoding.ASCII.GetBytes(relativePath.Replace('\\', '/'));
                Array.Copy(nameBytes, 0, header, 0, Math.Min(nameBytes.Length, 100));

                // file mode: 0644 (octal)
                WriteOctalField(header, 100, 8, 0644);

                // uid: 0
                WriteOctalField(header, 108, 8, 0);

                // gid: 0
                WriteOctalField(header, 116, 8, 0);

                // size
                WriteOctalField(header, 124, 12, content.Length);

                // mtime
                var epoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                WriteOctalField(header, 136, 12, epoch);

                // typeflag: '0' = regular file
                header[156] = (byte)'0';

                // magic: "ustar\0"
                var magic = System.Text.Encoding.ASCII.GetBytes("ustar\0");
                Array.Copy(magic, 0, header, 257, 6);

                // version: "00"
                header[263] = (byte)'0';
                header[264] = (byte)'0';

                // checksum: calculate with checksum field as spaces
                for (int i = 148; i < 156; i++) header[i] = (byte)' ';
                long checksum = 0;
                for (int i = 0; i < 512; i++) checksum += header[i];
                WriteOctalField(header, 148, 7, checksum);
                header[155] = (byte)' ';

                fs.Write(header, 0, 512);
                fs.Write(content, 0, content.Length);

                // pad to 512-byte boundary
                var remainder = content.Length % 512;
                if (remainder > 0)
                    fs.Write(new byte[512 - remainder], 0, 512 - remainder);
            }

            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(directory, file);
                var content = File.ReadAllBytes(file);
                WriteTarEntry(relativePath, content);
            }

            // two empty 512-byte blocks to signal end of archive
            fs.Write(new byte[1024], 0, 1024);

            return tarPath;
        });
    }

    private static void WriteOctalField(byte[] header, int offset, int length, long value)
    {
        var octal = Convert.ToString(value, 8).PadLeft(length - 1, '0');
        var bytes = System.Text.Encoding.ASCII.GetBytes(octal);
        Array.Copy(bytes, 0, header, offset, Math.Min(bytes.Length, length - 1));
        header[offset + length - 1] = 0;
    }
}
