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

    [Fact]
    public async Task GetRoot_AnonymousRequest_BodyContainsSetupMarkupNotLoginPrompt()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.DoesNotContain("Account/Login", body, StringComparison.OrdinalIgnoreCase);
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
