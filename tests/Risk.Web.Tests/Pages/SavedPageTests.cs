using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Risk.Web.Data;
using Risk.Web.Persistence;
using Risk.Web.Tests.Pages.Account;

namespace Risk.Web.Tests.Pages;

/// <summary>
/// Task 6.1: <c>/saved</c> is gated by <c>[Authorize]</c> (via
/// <c>Routes.razor</c>'s existing <c>AuthorizeRouteView</c>/<c>RedirectToLogin</c>
/// wiring, task 2.1/2.5), and renders a single "resume your game" card — this
/// repo supports at most ONE saved game per account (<c>SavedGameSummary</c>'s
/// own shape has no list/collection), not a list. Reuses
/// <see cref="AccountPagesTestFixture"/> (Sqlite-backed <c>RiskDbContext</c>)
/// and <see cref="AntiForgeryTestHelper"/>'s register-then-authenticated-client
/// pattern from <see cref="AuthenticatedNavTests"/>.
/// </summary>
public sealed class SavedPageTests : IClassFixture<AccountPagesTestFixture>
{
    private readonly AccountPagesTestFixture _factory;

    public SavedPageTests(AccountPagesTestFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetSaved_AnonymousRequest_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/saved");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Account/Login", response.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSaved_AuthenticatedWithNoSave_ShowsEmptyState()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        var email = $"saved-empty-{Guid.NewGuid():N}@example.com";
        await RegisterAsync(client, email, "Str0ngPassw0rd!");

        var response = await client.GetAsync("/saved");
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("No ten", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSaved_AuthenticatedWithExistingSave_ShowsResumeCard()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        var email = $"saved-existing-{Guid.NewGuid():N}@example.com";
        await RegisterAsync(client, email, "Str0ngPassw0rd!");
        var userId = await SeedSaveForUserAsync(email);

        var response = await client.GetAsync("/saved");
        var body = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Reanudar", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No ten", body, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, string password)
    {
        var token = await AntiForgeryTestHelper.GetTokenAsync(client, "/Account/Register");

        return await client.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.ConfirmPassword"] = password,
            ["__RequestVerificationToken"] = token,
        }));
    }

    /// <summary>
    /// Writes a minimal <c>SavedGame</c> row directly via <see cref="RiskDbContext"/>
    /// (bypassing <see cref="EfGameStore"/>/<see cref="GameSnapshotSerializer"/>
    /// entirely) — this test only needs the denormalized summary columns
    /// <c>Saved.razor</c> actually renders, not a real deserializable
    /// <c>GameState</c>/<c>PlayerConfig</c> payload.
    /// </summary>
    private async Task<string> SeedSaveForUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RiskDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);

        db.SavedGames.Add(new SavedGame
        {
            OwnerId = user.Id,
            StateJson = "{}",
            PlayersJson = "[]",
            Mode = "Classic",
            PlayerCount = 2,
            Phase = "Reinforce",
            SavedAtUtc = DateTime.UtcNow,
            SchemaVersion = GameSnapshot.CurrentSchemaVersion,
        });
        await db.SaveChangesAsync();

        return user.Id;
    }
}
