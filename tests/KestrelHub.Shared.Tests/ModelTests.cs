using System.Text.Json;
using FluentAssertions;
using KestrelHub.Shared.Enums;
using KestrelHub.Shared.Models;

namespace KestrelHub.Shared.Tests;

public class DeploymentStatusTests
{
    [Theory]
    [InlineData(DeploymentStatus.Pending, 0)]
    [InlineData(DeploymentStatus.Building, 1)]
    [InlineData(DeploymentStatus.Running, 2)]
    [InlineData(DeploymentStatus.Failed, 3)]
    [InlineData(DeploymentStatus.Stopped, 4)]
    public void EnumValues_AreCorrect(DeploymentStatus status, int expectedValue)
    {
        ((int)status).Should().Be(expectedValue);
    }

    [Fact]
    public void Enum_HasExactlyFiveValues()
    {
        Enum.GetValues<DeploymentStatus>().Should().HaveCount(5);
    }
}

public class AppDeploymentTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void CanInstantiate_WithDefaults()
    {
        var deployment = new AppDeployment();

        deployment.Id.Should().Be(Guid.Empty);
        deployment.Name.Should().Be(string.Empty);
        deployment.GitUrl.Should().Be(string.Empty);
        deployment.Branch.Should().Be("main");
        deployment.Status.Should().Be(DeploymentStatus.Pending);
    }

    [Fact]
    public void CanSet_AllProperties()
    {
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var deployment = new AppDeployment
        {
            Id = id,
            Name = "my-app",
            GitUrl = "https://github.com/user/repo.git",
            Branch = "develop",
            Status = DeploymentStatus.Running,
            CreatedAt = now,
            UpdatedAt = now.AddMinutes(5)
        };

        deployment.Id.Should().Be(id);
        deployment.Name.Should().Be("my-app");
        deployment.GitUrl.Should().Be("https://github.com/user/repo.git");
        deployment.Branch.Should().Be("develop");
        deployment.Status.Should().Be(DeploymentStatus.Running);
        deployment.CreatedAt.Should().Be(now);
        deployment.UpdatedAt.Should().Be(now.AddMinutes(5));
    }

    [Fact]
    public void JsonSerialization_Roundtrip_PreservesAllFields()
    {
        var original = new AppDeployment
        {
            Id = Guid.NewGuid(),
            Name = "test-app",
            GitUrl = "https://github.com/test/repo.git",
            Branch = "main",
            Status = DeploymentStatus.Building,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow.AddSeconds(30)
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<AppDeployment>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be(original.Id);
        deserialized.Name.Should().Be(original.Name);
        deserialized.GitUrl.Should().Be(original.GitUrl);
        deserialized.Branch.Should().Be(original.Branch);
        deserialized.Status.Should().Be(original.Status);
        deserialized.CreatedAt.Should().Be(original.CreatedAt);
        deserialized.UpdatedAt.Should().Be(original.UpdatedAt);
    }

    [Fact]
    public void UpdatedAt_IsGreaterThanOrEqualTo_CreatedAt()
    {
        var createdAt = DateTime.UtcNow;
        var updatedAt = createdAt.AddMinutes(1);

        updatedAt.Should().BeOnOrAfter(createdAt);
    }
}

public class ContainerInfoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void CanInstantiate_WithDefaults()
    {
        var info = new ContainerInfo();

        info.ContainerId.Should().Be(string.Empty);
        info.AppDeploymentId.Should().Be(Guid.Empty);
        info.ImageTag.Should().Be(string.Empty);
        info.Port.Should().Be(0);
        info.Status.Should().Be(string.Empty);
    }

    [Fact]
    public void CanSet_AllProperties()
    {
        var deploymentId = Guid.NewGuid();

        var info = new ContainerInfo
        {
            ContainerId = "abc123",
            AppDeploymentId = deploymentId,
            ImageTag = "my-app:latest",
            Port = 8100,
            Status = "running"
        };

        info.ContainerId.Should().Be("abc123");
        info.AppDeploymentId.Should().Be(deploymentId);
        info.ImageTag.Should().Be("my-app:latest");
        info.Port.Should().Be(8100);
        info.Status.Should().Be("running");
    }

    [Fact]
    public void JsonSerialization_Roundtrip_PreservesAllFields()
    {
        var original = new ContainerInfo
        {
            ContainerId = "container-xyz",
            AppDeploymentId = Guid.NewGuid(),
            ImageTag = "app:v1.0.0",
            Port = 8105,
            Status = "running"
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ContainerInfo>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.ContainerId.Should().Be(original.ContainerId);
        deserialized.AppDeploymentId.Should().Be(original.AppDeploymentId);
        deserialized.ImageTag.Should().Be(original.ImageTag);
        deserialized.Port.Should().Be(original.Port);
        deserialized.Status.Should().Be(original.Status);
    }
}

public class AgentHeartbeatTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void CanInstantiate_WithDefaults()
    {
        var heartbeat = new AgentHeartbeat();

        heartbeat.AgentId.Should().Be(Guid.Empty);
        heartbeat.Timestamp.Should().Be(default);
        heartbeat.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public void CanSet_AllProperties()
    {
        var agentId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        var heartbeat = new AgentHeartbeat
        {
            AgentId = agentId,
            Timestamp = timestamp,
            IsHealthy = true
        };

        heartbeat.AgentId.Should().Be(agentId);
        heartbeat.Timestamp.Should().Be(timestamp);
        heartbeat.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void JsonSerialization_Roundtrip_PreservesAllFields()
    {
        var original = new AgentHeartbeat
        {
            AgentId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            IsHealthy = true
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<AgentHeartbeat>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.AgentId.Should().Be(original.AgentId);
        deserialized.Timestamp.Should().Be(original.Timestamp);
        deserialized.IsHealthy.Should().Be(original.IsHealthy);
    }
}
