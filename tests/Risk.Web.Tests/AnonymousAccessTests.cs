using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Risk.Web.Tests;

/// <summary>
/// Proves — at the HTTP-pipeline level, not by manual claim — that the
/// Identity/EF Core wiring added for PR1 (Program.cs's
/// AddDbContext/AddIdentityCore/AddAuthentication/AddAuthorization plus
/// Routes.razor's AuthorizeRouteView) never gates anonymous hot-seat play.
/// Uses a real in-memory <see cref="WebApplicationFactory{TEntryPoint}"/>
/// host so the whole middleware pipeline (UseAuthentication/UseAuthorization
/// included) actually runs, unlike a unit test against isolated services.
///
/// No live database is required: <c>RiskDbContext</c> is only resolved
/// lazily per-scope (confirmed in PR1's manual verification), so a
/// syntactically valid but unreachable connection string is enough to build
/// the host without ever opening a socket.
/// </summary>
public sealed class AnonymousAccessTests : IClassFixture<AnonymousAccessTests.RiskWebApplicationFactory>
{
    private readonly RiskWebApplicationFactory _factory;

    public AnonymousAccessTests(RiskWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetRoot_AnonymousRequest_ReturnsOkWithoutAuthRedirect()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(
            response.Headers.Location is not null &&
            response.Headers.Location.ToString().Contains("Account/Login", StringComparison.OrdinalIgnoreCase),
            "Anonymous GET / must never be challenged into a login redirect.");
    }

    /// <summary>
    /// PR2 note: this originally asserted the body contained no
    /// "Account/Login" text at all. PR2's task 2.5 deliberately adds a
    /// global, always-visible login/register nav to
    /// <c>MainLayout.razor</c>'s anonymous branch, so that substring now
    /// legitimately appears on every page, including this one — that is
    /// the intended UX (auth is optional and reachable everywhere), not a
    /// login gate. The load-bearing assertion — that anonymous play is
    /// never challenged into a login redirect — is already covered by
    /// <see cref="GetRoot_AnonymousRequest_ReturnsOkWithoutAuthRedirect"/>
    /// above. This test now instead asserts the actual Setup markup still
    /// renders for an anonymous request, which is the real regression this
    /// test guards against.
    /// </summary>
    [Fact]
    public async Task GetRoot_AnonymousRequest_BodyContainsSetupMarkup()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Configurar partida", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Comenzar partida", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Task 2.5's global nav (<c>MainLayout.razor</c>'s <c>AuthorizeView</c>)
    /// must show the anonymous branch's login/register links on every route,
    /// including the anonymous Setup page.
    /// </summary>
    [Fact]
    public async Task GetRoot_AnonymousRequest_ShowsLoginAndRegisterNavLinks()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("href=\"/Account/Login\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Iniciar sesión", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/Account/Register\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Crear cuenta", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Overrides the connection string with a syntactically valid,
    /// unreachable placeholder so <c>UseSqlServer</c>/host build never needs
    /// a live database, per Fix 1's constraint of not adding a real DB
    /// dependency to the test suite.
    /// </summary>
    public sealed class RiskWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting(
                "ConnectionStrings:Default",
                "Server=tcp:placeholder.database.windows.net,1433;Database=placeholder;User Id=placeholder;Password=placeholder;Encrypt=True;");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] =
                        "Server=tcp:placeholder.database.windows.net,1433;Database=placeholder;User Id=placeholder;Password=placeholder;Encrypt=True;"
                });
            });
        }
    }
}
