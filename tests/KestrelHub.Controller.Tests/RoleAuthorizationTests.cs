using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using KestrelHub.Controller.Data;
using KestrelHub.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KestrelHub.Controller.Tests;

public class RoleAuthorizationTests : IDisposable
{
    private readonly TestAppFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RoleAuthorizationTests()
    {
        _factory = new TestAppFactory();
        _client = _factory.Client;
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private async Task CreateUserAndGetTokenAsync(string email, string password, string displayName, string role)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<KestrelHubUser>>();
        var user = new KestrelHubUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await userManager.CreateAsync(user, password);
        await userManager.AddToRoleAsync(user, role);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var token = body.GetProperty("accessToken").GetString();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private async Task SetupAdminAsync()
    {
        await _client.PostAsJsonAsync("/api/setup/complete", new
        {
            AdminEmail = "setupadmin@test.com",
            AdminPassword = "StrongPass123!@",
            AdminDisplayName = "Setup Admin"
        });
    }

    [Fact]
    public async Task Viewer_CannotPOST_Deployments_Returns403()
    {
        await SetupAdminAsync();
        await CreateUserAndGetTokenAsync("viewer@test.com", "StrongPass123!@", "Viewer", "Viewer");

        var response = await _client.PostAsJsonAsync("/api/deployments", new
        {
            GitUrl = "https://github.com/test/repo.git",
            Branch = "main"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Viewer_CanGET_Deployments_Returns200()
    {
        await SetupAdminAsync();
        await CreateUserAndGetTokenAsync("viewer2@test.com", "StrongPass123!@", "Viewer2", "Viewer");

        var response = await _client.GetAsync("/api/deployments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Developer_CanPOST_Deployments_Returns202()
    {
        await SetupAdminAsync();
        await CreateUserAndGetTokenAsync("dev@test.com", "StrongPass123!@", "Dev", "Developer");

        var response = await _client.PostAsJsonAsync("/api/deployments", new
        {
            GitUrl = "https://github.com/test/repo.git",
            Branch = "main",
            Name = "dev-app"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Developer_CannotDELETE_Deployments_Returns403()
    {
        await SetupAdminAsync();

        // Admin creates deployment
        var adminLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "setupadmin@test.com",
            Password = "StrongPass123!@"
        });
        var adminBody = await adminLogin.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var adminToken = adminBody.GetProperty("accessToken").GetString();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        var createResponse = await _client.PostAsJsonAsync("/api/deployments", new
        {
            GitUrl = "https://github.com/test/repo.git",
            Branch = "main",
            Name = "cant-delete"
        });
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var id = createBody.GetProperty("id").GetGuid();

        // Developer tries to delete
        await CreateUserAndGetTokenAsync("devdel@test.com", "StrongPass123!@", "DevDel", "Developer");
        var deleteResponse = await _client.DeleteAsync($"/api/deployments/{id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_CanDELETE_Deployments_Returns204()
    {
        await SetupAdminAsync();

        var adminLogin = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "setupadmin@test.com",
            Password = "StrongPass123!@"
        });
        var adminBody = await adminLogin.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var adminToken = adminBody.GetProperty("accessToken").GetString();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        var createResponse = await _client.PostAsJsonAsync("/api/deployments", new
        {
            GitUrl = "https://github.com/test/repo.git",
            Branch = "main",
            Name = "admin-delete"
        });
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var id = createBody.GetProperty("id").GetGuid();

        var deleteResponse = await _client.DeleteAsync($"/api/deployments/{id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
