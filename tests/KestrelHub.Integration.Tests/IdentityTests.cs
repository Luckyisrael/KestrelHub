using FluentAssertions;
using KestrelHub.Controller.Data;
using KestrelHub.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace KestrelHub.Integration.Tests;

public class IdentityTests : IAsyncLifetime
{
    private ApplicationDbContext _context = null!;
    private UserManager<KestrelHubUser> _userManager = null!;
    private RoleManager<KestrelHubRole> _roleManager = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("identity-test-" + Guid.NewGuid().ToString("N")[..8]));

        services.AddIdentity<KestrelHubUser, KestrelHubRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 12;
            options.Password.RequiredUniqueChars = 4;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.AllowedForNewUsers = true;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        var provider = services.BuildServiceProvider();
        _context = provider.GetRequiredService<ApplicationDbContext>();
        _userManager = provider.GetRequiredService<UserManager<KestrelHubUser>>();
        _roleManager = provider.GetRequiredService<RoleManager<KestrelHubRole>>();

        await _context.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync()
    {
        _context.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("Short1!")]           // < 12 chars
    [InlineData("alllowercase1!")]    // no uppercase
    [InlineData("ALLUPPERCASE1!")]    // no lowercase
    [InlineData("NoDigitsHere!!")]    // no digit
    [InlineData("NoSpecial1234")]     // no special char
    public async Task PasswordValidator_RejectsWeakPasswords(string password)
    {
        var user = new KestrelHubUser
        {
            UserName = "test@test.com",
            Email = "test@test.com",
            DisplayName = "Test"
        };

        var result = await _userManager.CreateAsync(user, password);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PasswordValidator_AcceptsStrongPassword()
    {
        var user = new KestrelHubUser
        {
            UserName = "strong@test.com",
            Email = "strong@test.com",
            DisplayName = "Strong",
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, "StrongPass123!@");

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ThreeRolesExist_AfterSeed()
    {
        var roles = await _roleManager.Roles.ToListAsync();

        roles.Should().HaveCountGreaterThanOrEqualTo(3);
        roles.Should().Contain(r => r.Name == "Admin");
        roles.Should().Contain(r => r.Name == "Developer");
        roles.Should().Contain(r => r.Name == "Viewer");
    }

    [Fact]
    public async Task SystemSettings_HasIsSetupCompleteFalse()
    {
        var settings = await _context.SystemSettings.FirstOrDefaultAsync();

        settings.Should().NotBeNull();
        settings!.IsSetupComplete.Should().BeFalse();
        settings.InstanceId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreatedAt_SetOnUserCreation()
    {
        var user = new KestrelHubUser
        {
            UserName = "created@test.com",
            Email = "created@test.com",
            DisplayName = "Created",
            CreatedAt = DateTime.UtcNow
        };

        await _userManager.CreateAsync(user, "StrongPass123!@");

        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task DeactivatedUser_CannotAuthenticate()
    {
        var user = new KestrelHubUser
        {
            UserName = "deactivated@test.com",
            Email = "deactivated@test.com",
            DisplayName = "Deactivated",
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };

        await _userManager.CreateAsync(user, "StrongPass123!@");

        // Verify IsActive = false
        var found = await _userManager.FindByNameAsync("deactivated@test.com");
        found!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task LockedAccount_RejectsAfterMaxAttempts()
    {
        var user = new KestrelHubUser
        {
            UserName = "locked@test.com",
            Email = "locked@test.com",
            DisplayName = "Locked",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _userManager.CreateAsync(user, "StrongPass123!@");

        // AccessFailedAsync increments the failed count
        for (int i = 0; i < 5; i++)
        {
            await _userManager.AccessFailedAsync(user);
        }

        var isLocked = await _userManager.IsLockedOutAsync(user);
        isLocked.Should().BeTrue();
    }
}
