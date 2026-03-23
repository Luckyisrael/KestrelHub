using Docker.DotNet;
using FluentAssertions;
using KestrelHub.Controller.Services;
using Xunit;

namespace KestrelHub.Controller.Tests;

public class DockerServiceTests
{
    [Fact]
    public async Task GetContainerStatusAsync_ReturnsMissing_ForNonExistentContainer()
    {
        var service = new DockerService();

        var status = await service.GetContainerStatusAsync("nonexistent-container-id-12345");

        status.Should().Be("missing");
    }
}
