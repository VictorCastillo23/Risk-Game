using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Risk.AI;
using Risk.Domain.Dice;
using Risk.Engine;
using Risk.Web.Components;
using Risk.Web.Data;
using Risk.Web.Persistence;
using Risk.Web.RateLimiting;
using Risk.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Login/Register/Logout land as Razor Pages (design D1), deliberately
// outside the Blazor router and MapRazorComponents<App>() below, so they
// stay plain HTTP endpoints free to issue real redirects + auth cookies
// without touching the app's single interactive circuit.
builder.Services.AddRazorPages();

// Composition root (design D1/D2/D3): engine and dice roller are stateless,
// so they are shared singletons; the session is scoped to one Blazor
// Server circuit, i.e. one hot-seat game per browser tab.
builder.Services.AddSingleton<IDiceRoller, RandomDiceRoller>();
builder.Services.AddSingleton<IGameEngine, GameEngine>();
builder.Services.AddSingleton<BotTurnRunner>();
builder.Services.AddScoped<IGameStore, EfGameStore>();
builder.Services.AddScoped<GameSessionService>();

// Accounts (design D1/D4): Identity gates only /saved and the future
// save/resume actions — anonymous hot-seat play through Setup.razor/
// Game.razor never touches any of this. Login/Register/Logout land as
// Razor Pages in PR2, kept deliberately outside the interactive Blazor
// router (D1) so App.razor's global InteractiveServer render mode never
// has to change.
// EnableRetryOnFailure (fix pass, BLOCKER finding): Azure SQL is prone to
// brief transient faults (throttling, failover) that a bare connection
// attempt has no chance to recover from on its own. This only helps with
// transient blips — GameSessionService's save/resume/delete-on-win methods
// still need their own try/catch around IGameStore for the non-transient
// case (DB fully unavailable), which is a separate, already-covered fix.
builder.Services.AddDbContext<RiskDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()));

builder.Services.AddIdentityCore<ApplicationUser>()
    .AddEntityFrameworkStores<RiskDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

// Hardening (review fix): the target host (Azure App Service) terminates
// TLS at its edge, so the app only ever sees plain HTTP internally unless
// forwarded headers are trusted (see UseForwardedHeaders below). Without
// SecurePolicy = Always, the auth cookie would still be marked non-Secure
// and could be replayed over an accidental HTTP connection.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Password/uniqueness policy pinned explicitly (review fix) rather than
// left to silent framework defaults — this is a hobby app, so no extra
// complexity rules beyond the framework's own reasonable defaults.
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
});

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Azure App Service forwards the original scheme via X-Forwarded-Proto
// (and the original client IP via X-Forwarded-For) because it terminates
// TLS at its edge. Without this, ASP.NET Core sees every request as plain
// HTTP, which would make UseHttpsRedirection loop and the Secure cookie
// above get dropped. Azure's own edge is the only proxy in front of this
// app, so trusting forwarded headers here is the minimal correct fix;
// residual risk (accepting forwarded headers from an untrusted proxy) does
// not apply as long as this app is only ever reachable through Azure App
// Service's front end.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;

    // Fix pass (BLOCKER): ForwardedHeadersOptions' *default* KnownNetworks/
    // KnownProxies only trust loopback, which is never the immediate peer in
    // front of Kestrel on Azure App Service (Azure's own edge is). Left at
    // the default, the middleware silently refuses to trust X-Forwarded-For,
    // so Connection.RemoteIpAddress never gets rewritten from the real
    // client IP — collapsing the rate limiter's per-IP partitioning above
    // into a single shared bucket for the whole site. Clearing both lists is
    // Microsoft's own documented pattern specifically for Azure App Service:
    // https://learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer
    // App Service's network isolates this app so only Azure's trusted
    // front-end can reach it directly, which makes "trust the immediate
    // peer's forwarded headers unconditionally" safe *in this specific
    // hosting model*. This would NOT be safe for an app directly exposed to
    // arbitrary internet traffic (e.g. self-hosted behind no proxy, or
    // behind an untrusted/public proxy) — do not copy this pattern there
    // without a real KnownProxies/KnownNetworks allowlist.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Hardening (auth-endpoints-rate-limiting): per-IP fixed-window rate limit
// applied to /Account/Register and /Account/Login. See
// RateLimitPolicies.AuthEndpoints for why this is wired at the page-class
// level and why the policy body itself branches on HTTP method, and
// AuthRateLimitOptions for the canonical business rationale (why this is a
// distinct layer from IdentityOptions.Lockout above). Partitioned by
// Connection.RemoteIpAddress, which UseForwardedHeaders (below, run before
// UseRateLimiter) rewrites to the real client IP behind Azure App Service's
// edge proxy — so this keys off the actual client, not Azure's own
// front-end IP.
builder.Services.Configure<AuthRateLimitOptions>(
    builder.Configuration.GetSection(AuthRateLimitOptions.SectionName));

builder.Services.AddRateLimiter(_ => { });
builder.Services.AddOptions<RateLimiterOptions>()
    .Configure<IOptions<AuthRateLimitOptions>>((rateLimiterOptions, authOptions) =>
    {
        var settings = authOptions.Value;

        rateLimiterOptions.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.ContentType = "text/plain";

            // Fix pass (CRITICAL): surface the fixed-window limiter's own
            // retry-after metadata rather than leaving the client to guess.
            // Full themed-error-page integration into Login/Register's
            // ModelState-driven UI is a larger change than this fix pass
            // covers — the header plus a message that states the actual
            // wait is the target bar here.
            string message;
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                var retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
                context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
                message = $"Too many requests. Please try again in about {retryAfterSeconds} seconds.";
            }
            else
            {
                message = "Too many requests. Please try again later.";
            }

            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Risk.Web.RateLimiting");
            logger.LogWarning(
                "Rate limit exceeded for {Path} from {PartitionKey}.",
                context.HttpContext.Request.Path,
                context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

            await context.HttpContext.Response.WriteAsync(message, cancellationToken);
        };

        rateLimiterOptions.AddPolicy(RateLimitPolicies.AuthEndpoints, httpContext =>
        {
            if (!HttpMethods.IsPost(httpContext.Request.Method))
            {
                return RateLimitPartition.GetNoLimiter("non-post-auth-request");
            }

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.PermitLimit,
                    Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                });
        });
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

// Must run before UseHttpsRedirection/UseAuthentication so the app
// correctly perceives HTTPS/client IP behind Azure's reverse proxy.
app.UseForwardedHeaders();
app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapRazorPages();

app.Run();

// Exposes the top-level-statement-generated Program class to
// WebApplicationFactory<Program> in tests/Risk.Web.Tests.
public partial class Program
{
}
