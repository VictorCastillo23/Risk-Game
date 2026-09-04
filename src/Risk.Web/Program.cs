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

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
