using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Risk.Domain.Dice;
using Risk.Engine;
using Risk.Web.Components;
using Risk.Web.Data;
using Risk.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Composition root (design D1/D2/D3): engine and dice roller are stateless,
// so they are shared singletons; the session is scoped to one Blazor
// Server circuit, i.e. one hot-seat game per browser tab.
builder.Services.AddSingleton<IDiceRoller, RandomDiceRoller>();
builder.Services.AddSingleton<IGameEngine, GameEngine>();
builder.Services.AddScoped<GameSessionService>();

// Accounts (design D1/D4): Identity gates only /saved and the future
// save/resume actions — anonymous hot-seat play through Setup.razor/
// Game.razor never touches any of this. Login/Register/Logout land as
// Razor Pages in PR2, kept deliberately outside the interactive Blazor
// router (D1) so App.razor's global InteractiveServer render mode never
// has to change.
builder.Services.AddDbContext<RiskDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

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
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Exposes the top-level-statement-generated Program class to
// WebApplicationFactory<Program> in tests/Risk.Web.Tests.
public partial class Program
{
}
