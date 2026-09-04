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
/// Covers spec `user-accounts` requirement "Login establishes a session"
/// plus the `returnUrl` open-redirect guard from design's task 2.2, against
/// the real Razor Page over HTTP.
/// </summary>
public sealed class LoginPageTests : IClassFixture<AccountPagesTestFixture>
{
    private const string Password = "Str0ngPassw0rd!";

    private readonly AccountPagesTestFixture _factory;

    public LoginPageTests(AccountPagesTestFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WrongPassword_IsRejectedAndNoSessionIsEstablished()
    {
        var email = await SeedUserAsync();
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await PostLoginAsync(client, email, "definitely-wrong", returnUrl: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid login attempt", body, StringComparison.OrdinalIgnoreCase);
        Assert.False(AntiForgeryTestHelper.HasSetCookieContaining(response, ".AspNetCore.Identity.Application"));
    }

    [Fact]
    public async Task Login_CorrectPassword_SignsInAndRedirectsToDefault()
    {
        var email = await SeedUserAsync();
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await PostLoginAsync(client, email, Password, returnUrl: null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.ToString());
        Assert.True(AntiForgeryTestHelper.HasSetCookieContaining(response, ".AspNetCore.Identity.Application"));
    }

    [Fact]
    public async Task Login_CorrectPasswordWithLocalReturnUrl_RedirectsToReturnUrl()
    {
        var email = await SeedUserAsync();
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await PostLoginAsync(client, email, Password, returnUrl: "/game");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/game", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Login_CorrectPasswordWithExternalReturnUrl_FallsBackToDefault()
    {
        var email = await SeedUserAsync();
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await PostLoginAsync(client, email, Password, returnUrl: "https://evil.example.com/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.ToString());
    }

    private async Task<string> SeedUserAsync()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";

        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var result = await userManager.CreateAsync(new ApplicationUser { UserName = email, Email = email }, Password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));

        return email;
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(
        HttpClient client, string email, string password, string? returnUrl)
    {
        var loginPath = returnUrl is null
            ? "/Account/Login"
            : $"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}";

        var token = await AntiForgeryTestHelper.GetTokenAsync(client, loginPath);

        return await client.PostAsync(loginPath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["__RequestVerificationToken"] = token,
        }));
    }
}
