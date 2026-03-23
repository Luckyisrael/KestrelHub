using KestrelHub.Controller.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace KestrelHub.Integration.Tests;

public class PostgreSqlFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("kestrelhub_test")
        .WithUsername("kh")
        .WithPassword("kh_test_password")
        .Build();

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(Container.GetConnectionString())
            .Options;

        return new ApplicationDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await Container.StartAsync();
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await Container.DisposeAsync();
    }
}
