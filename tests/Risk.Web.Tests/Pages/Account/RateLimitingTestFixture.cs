using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Risk.Web.Data;

namespace Risk.Web.Tests.Pages.Account;

/// <summary>
/// Same Sqlite-backed host as <see cref="AccountPagesTestFixture"/>, but each
/// test creates and disposes its own instance (rather than sharing one via
/// <c>IClassFixture</c>) so every test gets a fresh, empty rate-limiter
/// partition table — no cross-test interference from shared state. The
/// <c>RateLimiting:AuthEndpoints:PermitLimit</c> override lets tests use a
/// small, deterministic threshold instead of the production default, so a
/// test can cross it with a handful of fast, real HTTP requests and never
/// needs to sleep for a window to elapse.
/// </summary>
public sealed class RateLimitingTestFixture : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly int _permitLimit;

    public RateLimitingTestFixture(int permitLimit)
    {
        _permitLimit = permitLimit;
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:AuthEndpoints:PermitLimit"] = _permitLimit.ToString(),
                ["RateLimiting:AuthEndpoints:WindowSeconds"] = "60",
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<RiskDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<RiskDbContext>(options => options.UseSqlite(_connection));

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RiskDbContext>();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
