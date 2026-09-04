using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Risk.Web.Data;

namespace Risk.Web.Tests.Pages.Account;

/// <summary>
/// Covers spec `user-accounts` requirement "Registration without email
/// confirmation" (both scenarios) against the real Razor Page over HTTP,
/// per <see cref="AccountPagesTestFixture"/>'s rationale.
/// </summary>
public sealed class RegisterPageTests : IClassFixture<AccountPagesTestFixture>
{
    private readonly AccountPagesTestFixture _factory;

    public RegisterPageTests(AccountPagesTestFixture factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_NewEmail_CreatesAccountAndSignsInImmediately()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var email = $"new-{Guid.NewGuid():N}@example.com";

        var response = await PostRegisterAsync(client, email, "Str0ngPassw0rd!");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(HasSetCookieContaining(response, ".AspNetCore.Identity.Application"),
            "A successful registration must sign the user in immediately (no email confirmation step).");
    }

    [Fact]
    public async Task Register_DuplicateEmail_IsRejectedAndNoAccountIsCreated()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var email = $"dup-{Guid.NewGuid():N}@example.com";

        var first = await PostRegisterAsync(client, email, "Str0ngPassw0rd!");
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);

        var second = await PostRegisterAsync(client, email, "AnotherStr0ngPassw0rd!");

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var body = await second.Content.ReadAsStringAsync();
        Assert.Contains("already taken", body, StringComparison.OrdinalIgnoreCase);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RiskDbContext>();
        var matchingUsers = await db.Users.CountAsync(u => u.Email == email);
        Assert.Equal(1, matchingUsers);
    }

    private static async Task<HttpResponseMessage> PostRegisterAsync(HttpClient client, string email, string password)
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

    private static bool HasSetCookieContaining(HttpResponseMessage response, string needle) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies) &&
        cookies.Any(c => c.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
