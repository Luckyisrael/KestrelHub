using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using KestrelHub.Controller.Data;
using KestrelHub.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KestrelHub.Controller.Tests;

public class AuthControllerTests : IDisposable
{
    private readonly TestAppFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AuthControllerTests()
    {
        _factory = new TestAppFactory();
        _client = _factory.Client;
    }

    public void Dispose()
    {
        _factory.Dispose();
    }

    private async Task<string> CompleteSetupAndGetTokenAsync()
    {
        await _client.PostAsJsonAsync("/api/setup/complete", new
        {
            AdminEmail = "authtest@test.com",
            AdminPassword = "StrongPass123!@",
            AdminDisplayName = "Auth Tester"
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "authtest@test.com",
            Password = "StrongPass123!@"
        });

        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        return body.GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task Login_CorrectCredentials_ReturnsAccessTokenAndCookie()
    {
        await _client.PostAsJsonAsync("/api/setup/complete", new
        {
            AdminEmail = "login-ok@test.com",
            AdminPassword = "StrongPass123!@",
            AdminDisplayName = "Login OK"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "login-ok@test.com",
            Password = "StrongPass123!@"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();

        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies!.Should().Contain(c => c.Contains("refreshToken") && c.Contains("httponly"));
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401GenericMessage()
    {
        await _client.PostAsJsonAsync("/api/setup/complete", new
        {
            AdminEmail = "wrongpw@test.com",
            AdminPassword = "StrongPass123!@",
            AdminDisplayName = "Wrong PW"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "wrongpw@test.com",
            Password = "WrongPassword123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task Login_WrongEmail_ReturnsSameGenericMessage()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "nonexistent@test.com",
            Password = "AnyPassword123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task Me_ValidToken_ReturnsUserInfo()
    {
        var token = await CompleteSetupAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        body.GetProperty("email").GetString().Should().Be("authtest@test.com");
        body.TryGetProperty("roles", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Me_NoToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesToken()
    {
        await _client.PostAsJsonAsync("/api/setup/complete", new
        {
            AdminEmail = "logout@test.com",
            AdminPassword = "StrongPass123!@",
            AdminDisplayName = "Logout"
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "logout@test.com",
            Password = "StrongPass123!@"
        });

        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>(_jsonOptions);
        var token = body.GetProperty("accessToken").GetString();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var logoutResponse = await _client.PostAsync("/api/auth/logout", null);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_MissingCookie_Returns401()
    {
        var response = await _client.PostAsync("/api/auth/refresh", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
