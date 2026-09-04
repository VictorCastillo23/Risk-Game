using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Risk.Web.Tests.Pages.Account;

/// <summary>
/// Covers the authenticated branch of task 2.5's global nav
/// (<c>MainLayout.razor</c>'s <c>AuthorizeView</c>) — the counterpart to
/// <see cref="Risk.Web.Tests.AnonymousAccessTests.GetRoot_AnonymousRequest_ShowsLoginAndRegisterNavLinks"/>,
/// which covers the anonymous branch. Reuses <see cref="AccountPagesTestFixture"/>
/// and <see cref="AntiForgeryTestHelper"/> rather than building new fixtures:
/// registering already signs the user in (see <c>RegisterModel.OnPostAsync</c>),
/// so a single register call is enough to obtain an authenticated
/// <see cref="HttpClient"/> whose cookie container carries the identity
/// cookie into the subsequent GET, exactly as a browser would after
/// register-then-login.
/// </summary>
public sealed class AuthenticatedNavTests : IClassFixture<AccountPagesTestFixture>
{
    private readonly AccountPagesTestFixture _factory;

    public AuthenticatedNavTests(AccountPagesTestFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetRoot_AuthenticatedRequest_ShowsEmailAndLogoutForm()
    {
        var email = $"nav-{Guid.NewGuid():N}@example.com";

        // BaseAddress must be https: Program.cs pins the identity cookie's
        // SecurePolicy to Always (correct hardening for Azure's edge-terminated
        // TLS in production). System.Net.CookieContainer silently drops any
        // Secure-flagged cookie received over an http:// URI when parsing
        // Set-Cookie, so an http:// test client would receive the cookie on
        // the register response but never resend it on the next request —
        // not a bug in the app, just a test-client requirement.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        var registerResponse = await RegisterAndSignInAsync(client, email, "Str0ngPassw0rd!");
        Assert.Equal(HttpStatusCode.Redirect, registerResponse.StatusCode);
        Assert.True(AntiForgeryTestHelper.HasSetCookieContaining(registerResponse, ".AspNetCore.Identity.Application"));

        var response = await client.GetAsync("/");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains(email, body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("action=\"/Account/Logout\"", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cerrar sesión", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("__RequestVerificationToken", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Iniciar sesión", body, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpResponseMessage> RegisterAndSignInAsync(HttpClient client, string email, string password)
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
}
