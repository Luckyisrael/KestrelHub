using FluentAssertions;
using KestrelHub.Controller.Services;
using KestrelHub.Shared.Enums;
using Xunit;

namespace KestrelHub.Controller.Tests;

public class ProjectScannerTests : IDisposable
{
    private readonly string _testDir;

    public ProjectScannerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "kestrelhub-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task ScanAsync_IdentifiesSlnRepo_Correctly()
    {
        var slnDir = Path.Combine(_testDir, "MyApp");
        Directory.CreateDirectory(slnDir);
        File.WriteAllText(Path.Combine(slnDir, "MyApp.sln"), "dummy sln content");
        Directory.CreateDirectory(Path.Combine(slnDir, "src", "MyApp.Web"));
        File.WriteAllText(Path.Combine(slnDir, "src", "MyApp.Web", "MyApp.Web.csproj"),
            """<Project Sdk="Microsoft.NET.Sdk.Web"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>""");

        var scanner = new ProjectScanner();
        var result = await scanner.ScanAsync(slnDir);

        result.Should().NotBeNull();
        result!.ProjectType.Should().Be(ProjectType.Solution);
        result.PrimaryProjectPath.Should().EndWith(".sln");
        result.DotNetVersion.Should().Be("net10.0");
    }

    [Fact]
    public async Task ScanAsync_IdentifiesSingleCsprojRepo_Correctly()
    {
        var projDir = Path.Combine(_testDir, "SingleApp");
        Directory.CreateDirectory(projDir);
        File.WriteAllText(Path.Combine(projDir, "SingleApp.csproj"),
            """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>""");

        var scanner = new ProjectScanner();
        var result = await scanner.ScanAsync(projDir);

        result.Should().NotBeNull();
        result!.ProjectType.Should().Be(ProjectType.SingleProject);
        result.PrimaryProjectPath.Should().EndWith(".csproj");
        result.DotNetVersion.Should().Be("net10.0");
    }

    [Fact]
    public async Task ScanAsync_ReturnsUnknown_WhenNoNetFiles()
    {
        var emptyDir = Path.Combine(_testDir, "Empty");
        Directory.CreateDirectory(emptyDir);
        File.WriteAllText(Path.Combine(emptyDir, "README.md"), "# Not a .NET project");

        var scanner = new ProjectScanner();
        var result = await scanner.ScanAsync(emptyDir);

        result.Should().NotBeNull();
        result!.ProjectType.Should().Be(ProjectType.Unknown);
        result.PrimaryProjectPath.Should().BeEmpty();
        result.DotNetVersion.Should().Be("Unknown");
    }

    [Fact]
    public async Task ScanAsync_ReadsTargetFramework_FromSampleCsproj()
    {
        var projDir = Path.Combine(_testDir, "Net9App");
        Directory.CreateDirectory(projDir);
        File.WriteAllText(Path.Combine(projDir, "Net9App.csproj"),
            """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>""");

        var scanner = new ProjectScanner();
        var result = await scanner.ScanAsync(projDir);

        result!.DotNetVersion.Should().Be("net9.0");
    }

    [Fact]
    public async Task ScanAsync_HandlesBothSlnAndCsproj_PrefersSln()
    {
        var dir = Path.Combine(_testDir, "BothProject");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "BothProject.sln"), "sln content");
        File.WriteAllText(Path.Combine(dir, "BothProject.csproj"),
            """<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>""");

        var scanner = new ProjectScanner();
        var result = await scanner.ScanAsync(dir);

        result!.ProjectType.Should().Be(ProjectType.Solution);
    }

    [Fact]
    public async Task ScanAsync_HandlesMalformedCsproj_ReturnsUnknownVersion()
    {
        var projDir = Path.Combine(_testDir, "BadProject");
        Directory.CreateDirectory(projDir);
        File.WriteAllText(Path.Combine(projDir, "BadProject.csproj"), "not valid xml at all <<<");

        var scanner = new ProjectScanner();
        var result = await scanner.ScanAsync(projDir);

        result!.ProjectType.Should().Be(ProjectType.SingleProject);
        result.DotNetVersion.Should().Be("Unknown");
    }
}

public class GitServiceCleanupTests : IDisposable
{
    private readonly string _testDir;

    public GitServiceCleanupTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "kestrelhub-cleanup-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task CleanupAsync_RemovesDirectory_Completely()
    {
        var targetDir = Path.Combine(_testDir, "to-delete");
        Directory.CreateDirectory(targetDir);
        File.WriteAllText(Path.Combine(targetDir, "file.txt"), "content");
        Directory.CreateDirectory(Path.Combine(targetDir, "subdir"));
        File.WriteAllText(Path.Combine(targetDir, "subdir", "nested.txt"), "nested");

        var service = new GitService();
        await service.CleanupAsync(targetDir);

        Directory.Exists(targetDir).Should().BeFalse();
    }

    [Fact]
    public async Task CleanupAsync_NonExistentPath_DoesNotThrow()
    {
        var nonExistent = Path.Combine(_testDir, "does-not-exist");

        var service = new GitService();

        var act = () => service.CleanupAsync(nonExistent);

        await act.Should().NotThrowAsync();
    }
}
