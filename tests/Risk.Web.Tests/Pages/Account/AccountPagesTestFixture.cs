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
/// Boots the real <c>Program</c> host (same pipeline as
/// <see cref="Risk.Web.Tests.AnonymousAccessTests"/>) but swaps
/// <see cref="RiskDbContext"/> onto an open, in-memory Sqlite connection
/// instead of the production SQL Server provider.
///
/// Production migrations (<c>InitialIdentity</c>) target SQL Server and
/// cannot run against Sqlite, so this fixture calls
/// <c>Database.EnsureCreated()</c> to derive the schema directly from the
/// same EF model instead — sufficient to exercise real
/// <c>UserManager</c>/<c>SignInManager</c> behavior end-to-end over actual
/// HTTP requests (Register/Login/Logout Razor Pages), including antiforgery
/// and cookie issuance, with no live Azure SQL dependency.
/// </summary>
public sealed class AccountPagesTestFixture : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public AccountPagesTestFixture()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Hardening (auth-endpoints-rate-limiting): this fixture is shared
        // per test *class* (IClassFixture), so its rate-limiter state
        // persists across every test method in that class — some classes
        // legitimately fire more sequential POSTs than the production
        // default in one run (e.g. LoginPageTests' lockout test alone sends
        // 6). These tests exercise Register/Login/Logout business logic,
        // not the limiter itself (see RateLimitingTests for that, with its
        // own isolated fixture per test and a small deterministic
        // PermitLimit), so the limit is raised generously here to avoid
        // false-positive 429s on otherwise-passing test traffic.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:AuthEndpoints:PermitLimit"] = "1000",
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
