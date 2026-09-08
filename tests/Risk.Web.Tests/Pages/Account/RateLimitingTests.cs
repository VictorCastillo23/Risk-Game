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
    public async Task Post_Register_DifferentForwardedForClients_AreRateLimitedIndependently()
    {
        // Validates the Fix 1 forwarded-headers fix: WebApplicationFactory's
        // in-process TestServer does not populate Connection.RemoteIpAddress
        // by default, so without a trusted ForwardedHeadersMiddleware
        // rewriting it from X-Forwarded-For, every request here would fall
        // back to the same "unknown" partition key regardless of which
        // simulated client sent it — exactly the site-wide-lockout failure
        // mode Fix 1 addresses. This test sends two distinct
        // X-Forwarded-For values (simulating two real clients behind Azure's
        // edge) and asserts they get independent quotas.
        using var factory = new RateLimitingTestFixture(PermitLimit);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        const string clientAIp = "203.0.113.1";
        const string clientBIp = "203.0.113.2";

        for (var i = 0; i < PermitLimit; i++)
        {
            var response = await PostRegisterAsync(client, $"a{i}-{Guid.NewGuid():N}@example.com", clientAIp);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        var clientARejected = await PostRegisterAsync(client, $"a-over-{Guid.NewGuid():N}@example.com", clientAIp);
        Assert.Equal(HttpStatusCode.TooManyRequests, clientARejected.StatusCode);

        // Client B has never sent a request before — if it shared client A's
        // partition (the pre-fix bug), this would also be 429.
        var clientBResponse = await PostRegisterAsync(client, $"b-{Guid.NewGuid():N}@example.com", clientBIp);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, clientBResponse.StatusCode);
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

    private static async Task<HttpResponseMessage> PostRegisterAsync(
        HttpClient client, string email, string? forwardedFor = null)
    {
        var token = await AntiForgeryTestHelper.GetTokenAsync(client, "/Account/Register");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Account/Register")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = "Str0ngPassw0rd!",
                ["Input.ConfirmPassword"] = "Str0ngPassw0rd!",
                ["__RequestVerificationToken"] = token,
            }),
        };

        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return await client.SendAsync(request);
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
