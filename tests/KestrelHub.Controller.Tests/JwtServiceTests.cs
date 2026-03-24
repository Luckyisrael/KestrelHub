using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using KestrelHub.Controller.Services;
using KestrelHub.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace KestrelHub.Controller.Tests;

public class JwtServiceTests
{
    private readonly JwtService _service;

    public JwtServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "TestSecretKey-Min32CharactersLong!",
                ["Jwt:Issuer"] = "KestrelHub",
                ["Jwt:Audience"] = "KestrelHub",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7"
            })
            .Build();

        _service = new JwtService(config);
    }

    [Fact]
    public void GenerateAccessToken_ExpiresIn15Minutes()
    {
        var user = new KestrelHubUser { Id = Guid.NewGuid().ToString(), Email = "test@test.com", DisplayName = "Test" };

        var token = _service.GenerateAccessToken(user, ["Admin"]);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var expiry = jwt.ValidTo;
        var expectedExpiry = DateTime.UtcNow.AddMinutes(15);

        expiry.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void GenerateAccessToken_IncludesCorrectRoleClaims()
    {
        var user = new KestrelHubUser { Id = "user1", Email = "test@test.com", DisplayName = "Test" };

        var token = _service.GenerateAccessToken(user, ["Admin", "Developer"]);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();

        roles.Should().Contain("Admin");
        roles.Should().Contain("Developer");
    }

    [Fact]
    public void GenerateAccessToken_IncludesUserIdAndEmail()
    {
        var user = new KestrelHubUser { Id = "user-123", Email = "me@test.com", DisplayName = "Me" };

        var token = _service.GenerateAccessToken(user, ["Viewer"]);

        var principal = _service.ValidateAccessToken(token);
        principal.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be("user-123");
        principal.FindFirst(ClaimTypes.Email)!.Value.Should().Be("me@test.com");
    }

    [Fact]
    public void GenerateRefreshToken_Produces64ByteBase64()
    {
        var token = _service.GenerateRefreshToken();

        var bytes = Convert.FromBase64String(token);
        bytes.Length.Should().Be(64);
    }

    [Fact]
    public void GenerateRefreshToken_NonSequential()
    {
        var token1 = _service.GenerateRefreshToken();
        var token2 = _service.GenerateRefreshToken();

        token1.Should().NotBe(token2);
    }

    [Fact]
    public void ValidateAccessToken_ThrowsOnExpiredToken()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "TestSecretKey-Min32CharactersLong!",
                ["Jwt:Issuer"] = "KestrelHub",
                ["Jwt:Audience"] = "KestrelHub",
                ["Jwt:AccessTokenMinutes"] = "0"  // expires immediately
            })
            .Build();

        var shortLivedService = new JwtService(config);
        var user = new KestrelHubUser { Id = "1", Email = "a@b.com", DisplayName = "A" };

        var token = shortLivedService.GenerateAccessToken(user, ["Admin"]);

        var act = () => _service.ValidateAccessToken(token);
        act.Should().Throw<SecurityTokenExpiredException>();
    }

    [Fact]
    public void ValidateAccessToken_ThrowsOnTamperedToken()
    {
        var user = new KestrelHubUser { Id = "1", Email = "a@b.com", DisplayName = "A" };
        var token = _service.GenerateAccessToken(user, ["Admin"]);

        var tampered = token[..^5] + "XXXXX";

        var act = () => _service.ValidateAccessToken(tampered);
        act.Should().Throw<SecurityTokenException>();
    }

    [Fact]
    public void HashToken_StoresAsSHA256_NotRaw()
    {
        var rawToken = "my-raw-refresh-token-value";
        var hash = JwtService.HashToken(rawToken);

        hash.Should().NotBe(rawToken);
        hash.Length.Should().BeGreaterThan(20); // SHA-256 base64 is 44 chars
    }

    [Fact]
    public void HashToken_SameInputProducesSameHash()
    {
        var hash1 = JwtService.HashToken("same-token");
        var hash2 = JwtService.HashToken("same-token");

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void Constructor_ThrowsOnShortSecret()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "short"
            })
            .Build();

        var act = () => new JwtService(config);
        act.Should().Throw<InvalidOperationException>();
    }
}
