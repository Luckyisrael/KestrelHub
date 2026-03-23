using FluentAssertions;
using KestrelHub.Controller.Services;
using KestrelHub.Shared.Enums;
using KestrelHub.Shared.Models;
using Xunit;

namespace KestrelHub.Controller.Tests;

public class DockerfileGeneratorTests : IDisposable
{
    private readonly string _testDir;

    public DockerfileGeneratorTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "kestrelhub-df-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Generate_ContainsSdkBaseImage()
    {
        var scan = new ScanResult
        {
            ProjectType = ProjectType.SingleProject,
            PrimaryProjectPath = "MyApp/MyApp.csproj",
            DotNetVersion = "net10.0"
        };

        var generator = new DockerfileGenerator();
        var result = generator.Generate(scan, _testDir);

        result.Should().Contain("FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build");
    }

    [Fact]
    public void Generate_ContainsAspnetRuntimeImage()
    {
        var scan = new ScanResult
        {
            ProjectType = ProjectType.SingleProject,
            PrimaryProjectPath = "MyApp/MyApp.csproj",
            DotNetVersion = "net10.0"
        };

        var generator = new DockerfileGenerator();
        var result = generator.Generate(scan, _testDir);

        result.Should().Contain("FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime");
    }

    [Fact]
    public void Generate_SubstitutesProjectPathCorrectly()
    {
        var scan = new ScanResult
        {
            ProjectType = ProjectType.Solution,
            PrimaryProjectPath = "src/MyWebApp/MyWebApp.csproj",
            DotNetVersion = "net10.0"
        };

        var generator = new DockerfileGenerator();
        var result = generator.Generate(scan, _testDir);

        result.Should().Contain("dotnet restore \"src/MyWebApp/MyWebApp.csproj\"");
        result.Should().Contain("dotnet publish \"src/MyWebApp/MyWebApp.csproj\" -c Release");
    }

    [Fact]
    public void Generate_SubstitutesAssemblyNameCorrectly()
    {
        var scan = new ScanResult
        {
            ProjectType = ProjectType.SingleProject,
            PrimaryProjectPath = "MyService/MyService.csproj",
            DotNetVersion = "net10.0"
        };

        var generator = new DockerfileGenerator();
        var result = generator.Generate(scan, _testDir);

        result.Should().Contain("ENTRYPOINT [\"dotnet\", \"MyService.dll\"]");
    }

    [Fact]
    public void Generate_ReturnsNull_WhenDockerfileAlreadyExists()
    {
        File.WriteAllText(Path.Combine(_testDir, "Dockerfile"), "FROM scratch");

        var scan = new ScanResult
        {
            ProjectType = ProjectType.SingleProject,
            PrimaryProjectPath = "MyApp/MyApp.csproj",
            DotNetVersion = "net10.0"
        };

        var generator = new DockerfileGenerator();
        var result = generator.Generate(scan, _testDir);

        result.Should().BeNull();
    }

    [Fact]
    public void Generate_ReturnsNull_WhenProjectTypeUnknown()
    {
        var scan = new ScanResult
        {
            ProjectType = ProjectType.Unknown,
            PrimaryProjectPath = "",
            DotNetVersion = "Unknown"
        };

        var generator = new DockerfileGenerator();
        var result = generator.Generate(scan, _testDir);

        result.Should().BeNull();
    }

    [Fact]
    public void Generate_ContainsRequiredDirectives()
    {
        var scan = new ScanResult
        {
            ProjectType = ProjectType.SingleProject,
            PrimaryProjectPath = "Test/Test.csproj",
            DotNetVersion = "net10.0"
        };

        var generator = new DockerfileGenerator();
        var result = generator.Generate(scan, _testDir);

        result.Should().Contain("WORKDIR /src");
        result.Should().Contain("COPY . .");
        result.Should().Contain("WORKDIR /app");
        result.Should().Contain("COPY --from=build /app/publish .");
        result.Should().Contain("EXPOSE 8080");
        result.Should().Contain("ENV ASPNETCORE_URLS=http://+:8080");
    }

    [Fact]
    public void Generate_HandlesSlnPath_Correctly()
    {
        var scan = new ScanResult
        {
            ProjectType = ProjectType.Solution,
            PrimaryProjectPath = "MySln.sln",
            DotNetVersion = "net10.0"
        };

        var generator = new DockerfileGenerator();
        var result = generator.Generate(scan, _testDir);

        result.Should().Contain("dotnet restore \"MySln.sln\"");
        result.Should().Contain("ENTRYPOINT [\"dotnet\", \"MySln.dll\"]");
    }
}
