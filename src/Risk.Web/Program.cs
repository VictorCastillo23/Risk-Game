using Risk.Domain.Dice;
using Risk.Engine;
using Risk.Web.Components;
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
