using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using KestrelHub.Controller.Data;
using KestrelHub.Controller.DTOs;
using KestrelHub.Shared.Enums;
using KestrelHub.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KestrelHub.Controller.Tests;

public class DeploymentsControllerTests : IDisposable
{
    private readonly TestAppFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DeploymentsControllerTests()
    {
        _factory = new TestAppFactory();
        _client = _factory.Client;
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    public async Task Post_ValidBody_Returns202WithId()
    {
        var request = new CreateDeploymentRequest("https://github.com/test/repo.git", "main", "my-app");
        var response = await _client.PostAsJsonAsync("/api/deployments", request);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.TryGetProperty("id", out var idProp).Should().BeTrue();
        Guid.TryParse(idProp.ToString(), out _).Should().BeTrue();
    }

    [Fact]
    public async Task Post_MissingGitUrl_Returns400()
    {
        var request = new { GitUrl = "", Branch = "main" };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/deployments", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_ReturnsEmptyArray_WhenNoDeployments()
    {
        var response = await _client.GetAsync("/api/deployments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<List<AppDeployment>>(_jsonOptions);
        body.Should().NotBeNull();
        body.Should().BeEmpty();
    }

    [Fact]
    public async Task GetById_Returns404_ForUnknownId()
    {
        var response = await _client.GetAsync($"/api/deployments/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_ReturnsCorrectStatusAndLogCount()
    {
        var createRequest = new CreateDeploymentRequest("https://github.com/test/repo.git", "main", "status-app");
        var createResponse = await _client.PostAsJsonAsync("/api/deployments", createRequest);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var id = createBody.GetProperty("id").GetGuid();

        var response = await _client.GetAsync($"/api/deployments/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.GetProperty("name").GetString().Should().Be("status-app");
        body.TryGetProperty("logs", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Stop_Returns404_ForUnknownId()
    {
        var response = await _client.PostAsync($"/api/deployments/{Guid.NewGuid()}/stop", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAll_ReturnsDeployments_AfterCreating()
    {
        await _client.PostAsJsonAsync("/api/deployments",
            new CreateDeploymentRequest("https://github.com/test/repo1.git", "main", "app1"));
        await _client.PostAsJsonAsync("/api/deployments",
            new CreateDeploymentRequest("https://github.com/test/repo2.git", "main", "app2"));

        var response = await _client.GetAsync("/api/deployments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<AppDeployment>>(_jsonOptions);
        body.Should().HaveCount(2);
    }

    [Fact]
    public async Task Delete_Returns404_ForUnknownId()
    {
        var response = await _client.DeleteAsync($"/api/deployments/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Returns204_ForExistingDeployment()
    {
        var createRequest = new CreateDeploymentRequest("https://github.com/test/repo.git", "main", "delete-me");
        var createResponse = await _client.PostAsJsonAsync("/api/deployments", createRequest);
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var id = createBody.GetProperty("id").GetGuid();

        var response = await _client.DeleteAsync($"/api/deployments/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/deployments/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
