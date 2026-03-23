using System.Xml.Linq;
using KestrelHub.Shared.Enums;
using KestrelHub.Shared.Models;

namespace KestrelHub.Controller.Services;

public interface IProjectScanner
{
    Task<ScanResult?> ScanAsync(string repoPath);
}

public class ProjectScanner : IProjectScanner
{
    public Task<ScanResult?> ScanAsync(string repoPath)
    {
        return Task.Run<ScanResult?>(() =>
        {
            var slnFiles = Directory.GetFiles(repoPath, "*.sln", SearchOption.AllDirectories);
            var csprojFiles = Directory.GetFiles(repoPath, "*.csproj", SearchOption.AllDirectories);

            if (slnFiles.Length > 0)
            {
                var primaryCsproj = csprojFiles.Length > 0 ? csprojFiles[0] : null;
                return new ScanResult
                {
                    ProjectType = ProjectType.Solution,
                    PrimaryProjectPath = slnFiles[0],
                    DotNetVersion = primaryCsproj is not null ? ReadTargetFramework(primaryCsproj) : "Unknown"
                };
            }

            if (csprojFiles.Length > 0)
            {
                return new ScanResult
                {
                    ProjectType = ProjectType.SingleProject,
                    PrimaryProjectPath = csprojFiles[0],
                    DotNetVersion = ReadTargetFramework(csprojFiles[0])
                };
            }

            return new ScanResult
            {
                ProjectType = ProjectType.Unknown,
                PrimaryProjectPath = string.Empty,
                DotNetVersion = "Unknown"
            };
        });
    }

    private static string ReadTargetFramework(string csprojPath)
    {
        try
        {
            var doc = XDocument.Load(csprojPath);
            var tfm = doc.Descendants("TargetFramework").FirstOrDefault()?.Value;
            return tfm ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
}
