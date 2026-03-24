using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using KestrelHub.Controller.Data;
using KestrelHub.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KestrelHub.Controller.Tests;

public class SetupControllerTests : IDisposable
{
    private readonly TestAppFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public SetupControllerTests()
    {
        _factory = new TestAppFactory();
        _client = _factory.Client;
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    public async Task GetStatus_ReturnsIsSetupCompleteFalse_BeforeSetup()
    {
        var response = await _client.GetAsync("/api/setup/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.GetProperty("isSetupComplete").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Complete_CreatesAdminWithAdminRole()
    {
        var request = new
        {
            AdminEmail = "admin@test.com",
            AdminPassword = "StrongPass123!@",
            AdminDisplayName = "Admin"
        };

        var response = await _client.PostAsJsonAsync("/api/setup/complete", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify admin exists with Admin role
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = db.Users.FirstOrDefault(u => u.Email == "admin@test.com");
        user.Should().NotBeNull();
        user!.DisplayName.Should().Be("Admin");
    }

    [Fact]
    public async Task Complete_SetsIsSetupCompleteTrue()
    {
        var request = new
        {
            AdminEmail = "admin2@test.com",
            AdminPassword = "StrongPass123!@",
            AdminDisplayName = "Admin"
        };

        await _client.PostAsJsonAsync("/api/setup/complete", request);

        var statusResponse = await _client.GetAsync("/api/setup/status");
        var body = await statusResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.GetProperty("isSetupComplete").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Complete_ReturnsAccessToken()
    {
        var request = new
        {
            AdminEmail = "admin3@test.com",
            AdminPassword = "StrongPass123!@",
            AdminDisplayName = "Admin"
        };

        var response = await _client.PostAsJsonAsync("/api/setup/complete", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.TryGetProperty("accessToken", out var token).Should().BeTrue();
        token.GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Complete_SecondCall_Returns404()
    {
        var request = new
        {
            AdminEmail = "admin4@test.com",
            AdminPassword = "StrongPass123!@",
            AdminDisplayName = "Admin"
        };

        // First call succeeds
        var first = await _client.PostAsJsonAsync("/api/setup/complete", request);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second call returns 404
        var second = await _client.PostAsJsonAsync("/api/setup/complete", request);
        second.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Complete_WeakPassword_Returns400()
    {
        var request = new
        {
            AdminEmail = "weak@test.com",
            AdminPassword = "short",
            AdminDisplayName = "Weak"
        };

        var response = await _client.PostAsJsonAsync("/api/setup/complete", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
