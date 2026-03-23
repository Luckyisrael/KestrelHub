using System.Text.Json.Serialization;

namespace KestrelHub.Controller.DTOs;

public record CreateDeploymentRequest(
    [property: JsonPropertyName("gitUrl")] string GitUrl,
    [property: JsonPropertyName("branch")] string Branch = "main",
    [property: JsonPropertyName("name")] string? Name = null);
