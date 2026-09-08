using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Risk.Web.Tests.Pages.Account;

/// <summary>
/// Cost-driven hardening: the Azure SQL Database backing this app is
/// serverless and bills per vCore-hour while active, auto-pausing only after
/// 15 minutes idle. Unlimited POSTs to <c>/Account/Register</c> or
/// <c>/Account/Login</c> from a single client can keep it awake indefinitely
/// even without a real credential-stuffing attempt (Identity's own
/// lockout policy protects one *existing* account from repeated password
/// guesses, but does nothing to stop many new registrations or logins with
/// different emails). These tests exercise the actual
/// <c>Microsoft.AspNetCore.RateLimiting</c> policy end-to-end, using a small
/// deterministic <c>PermitLimit</c> (via <see cref="RateLimitingTestFixture"/>)
/// so the threshold is crossed with a handful of fast requests inside one
/// fixed window — no real sleeping required.
/// </summary>
public sealed class RateLimitingTests
{
    private const int PermitLimit = 3;

    [Fact]
    public async Task Post_Register_UnderLimit_Succeeds_ButExceedingItReturns429()
    {
        using var factory = new RateLimitingTestFixture(PermitLimit);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        for (var i = 0; i < PermitLimit; i++)
        {
            var response = await PostRegisterAsync(client, $"user{i}-{Guid.NewGuid():N}@example.com");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var limited = await PostRegisterAsync(client, $"over-{Guid.NewGuid():N}@example.com");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task Post_Login_UnderLimit_Succeeds_ButExceedingItReturns429()
    {
        using var factory = new RateLimitingTestFixture(PermitLimit);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        for (var i = 0; i < PermitLimit; i++)
        {
            var response = await PostLoginAsync(client, $"nobody{i}@example.com");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var limited = await PostLoginAsync(client, "over@example.com");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task Get_Register_IsNeverRateLimited()
    {
        using var factory = new RateLimitingTestFixture(PermitLimit);
        var client = factory.CreateClient();

        for (var i = 0; i < PermitLimit + 5; i++)
        {
            var response = await client.GetAsync("/Account/Register");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Get_Login_IsNeverRateLimited()
    {
        using var factory = new RateLimitingTestFixture(PermitLimit);
        var client = factory.CreateClient();

        for (var i = 0; i < PermitLimit + 5; i++)
        {
            var response = await client.GetAsync("/Account/Login");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private static async Task<HttpResponseMessage> PostRegisterAsync(HttpClient client, string email)
    {
        var token = await AntiForgeryTestHelper.GetTokenAsync(client, "/Account/Register");

        return await client.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = "Str0ngPassw0rd!",
            ["Input.ConfirmPassword"] = "Str0ngPassw0rd!",
            ["__RequestVerificationToken"] = token,
        }));
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string email)
    {
        var token = await AntiForgeryTestHelper.GetTokenAsync(client, "/Account/Login");

        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = "definitely-wrong",
            ["__RequestVerificationToken"] = token,
        }));
    }
}
