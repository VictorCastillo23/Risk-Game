using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Risk.Web.Data;

namespace Risk.Web.Tests.Pages.Account;

/// <summary>
/// Covers spec `user-accounts` requirement "Logout ends the session". The
/// handler is POST-only by construction (no `OnGet` override signs anyone
/// out — see <c>LogoutModel</c>), so this also proves a plain GET never
/// tears down the session, closing the CSRF-via-link vector task 2.3 calls
/// out.
/// </summary>
public sealed class LogoutPageTests : IClassFixture<AccountPagesTestFixture>
{
    private const string Password = "Str0ngPassw0rd!";

    private readonly AccountPagesTestFixture _factory;

    public LogoutPageTests(AccountPagesTestFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Logout_Post_EndsSessionAndRedirectsHome()
    {
        var client = await SignedInClientAsync();

        var token = await AntiForgeryTestHelper.GetTokenAsync(client, "/Account/Logout");

        var response = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.ToString());
        Assert.True(
            response.Headers.TryGetValues("Set-Cookie", out var setCookies) &&
            setCookies.Any(c =>
                c.Contains(".AspNetCore.Identity.Application", StringComparison.OrdinalIgnoreCase) &&
                (c.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase) ||
                 c.Contains("expires=Thu, 01-Jan-1970", StringComparison.OrdinalIgnoreCase))),
            "Logout must clear the identity auth cookie via an expired Set-Cookie.");
    }

    [Fact]
    public async Task Logout_Get_DoesNotEndSession()
    {
        var client = await SignedInClientAsync();

        var response = await client.GetAsync("/Account/Logout");

        Assert.False(
            response.Headers.TryGetValues("Set-Cookie", out var setCookies) &&
            setCookies.Any(c => c.Contains(".AspNetCore.Identity.Application", StringComparison.OrdinalIgnoreCase)),
            "A plain GET must never sign the user out (CSRF-via-link protection).");
    }

    private async Task<HttpClient> SignedInClientAsync()
    {
        var email = $"logout-{Guid.NewGuid():N}@example.com";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var result = await userManager.CreateAsync(new ApplicationUser { UserName = email, Email = email }, Password);
            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await AntiForgeryTestHelper.GetTokenAsync(client, "/Account/Login");

        var loginResponse = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = Password,
            ["__RequestVerificationToken"] = token,
        }));

        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        return client;
    }
}
