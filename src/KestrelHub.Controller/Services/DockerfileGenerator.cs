using KestrelHub.Shared.Enums;
using KestrelHub.Shared.Models;

namespace KestrelHub.Controller.Services;

public interface IDockerfileGenerator
{
    string? Generate(ScanResult scan, string repoPath);
}

public class DockerfileGenerator : IDockerfileGenerator
{
    private const string Template = """
        FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
        WORKDIR /src
        COPY . .
        RUN dotnet restore "{PROJECT_PATH}"
        RUN dotnet publish "{PROJECT_PATH}" -c Release -o /app/publish

        FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
        WORKDIR /app
        COPY --from=build /app/publish .
        EXPOSE 8080
        ENV ASPNETCORE_URLS=http://+:8080
        ENTRYPOINT ["dotnet", "{ASSEMBLY_NAME}.dll"]
        """;

    public string? Generate(ScanResult scan, string repoPath)
    {
        if (scan.ProjectType is ProjectType.Unknown)
            return null;

        var existingDockerfile = Path.Combine(repoPath, "Dockerfile");
        if (File.Exists(existingDockerfile))
            return null;

        var projectPath = GetProjectPath(scan);
        var assemblyName = Path.GetFileNameWithoutExtension(projectPath);

        return Template
            .Replace("{PROJECT_PATH}", projectPath)
            .Replace("{ASSEMBLY_NAME}", assemblyName);
    }

    private static string GetProjectPath(ScanResult scan)
    {
        if (scan.ProjectType is ProjectType.Solution)
            return scan.PrimaryProjectPath;

        return scan.PrimaryProjectPath;
    }
}
