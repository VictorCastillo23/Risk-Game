using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Risk.Web.Tests.Pages.Account;

/// <summary>
/// Same Sqlite-backed host as <see cref="AccountPagesTestFixture"/> (inherits
/// its connection lifecycle and DbContext-swap logic verbatim), but each
/// test creates and disposes its own instance (rather than sharing one via
/// <c>IClassFixture</c>) so every test gets a fresh, empty rate-limiter
/// partition table — no cross-test interference from shared state. The
/// <c>RateLimiting:AuthEndpoints:PermitLimit</c> override lets tests use a
/// small, deterministic threshold instead of the production default, so a
/// test can cross it with a handful of fast, real HTTP requests and never
/// needs to sleep for a window to elapse.
/// </summary>
public sealed class RateLimitingTestFixture : AccountPagesTestFixture
{
    private readonly int _permitLimit;

    public RateLimitingTestFixture(int permitLimit)
    {
        _permitLimit = permitLimit;
    }

    protected override IDictionary<string, string?> GetConfigOverrides() => new Dictionary<string, string?>
    {
        ["RateLimiting:AuthEndpoints:PermitLimit"] = _permitLimit.ToString(),
        ["RateLimiting:AuthEndpoints:WindowSeconds"] = "60",
    };

    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        // WebApplicationFactory's in-process TestServer transport leaves
        // Connection.RemoteIpAddress null, which is NOT representative
        // of production (Azure App Service's edge is always a real,
        // non-loopback peer). An IStartupFilter runs outermost — before
        // Program.cs's own pipeline, including UseForwardedHeaders — so
        // this stamps a fake non-loopback "edge" IP onto every request
        // first, faithfully reproducing the real network topology this
        // app runs behind. Without this, tests could not tell the
        // difference between ForwardedHeadersOptions trusting X-Forwarded-For
        // correctly and defaulting to loopback-only trust (the Fix 1 bug),
        // because a null RemoteIpAddress does not exercise the same trust
        // check path as a real one.
        services.AddTransient<IStartupFilter, SimulateEdgeProxyStartupFilter>();
    }

    private sealed class SimulateEdgeProxyStartupFilter : IStartupFilter
    {
        // A fictitious, definitely-non-loopback address standing in for
        // Azure App Service's real edge IP.
        private static readonly IPAddress EdgeProxyIp = IPAddress.Parse("40.112.72.205");

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = EdgeProxyIp;
                return nextMiddleware();
            });

            next(app);
        };
    }
}
