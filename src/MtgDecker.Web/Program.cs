using MediatR;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using MtgDecker.Application;
using MtgDecker.Application.DeckExport;
using MtgDecker.Infrastructure;
using MtgDecker.Infrastructure.Data;
using MtgDecker.Web.Components;
using MtgDecker.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddApplication();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
builder.Services.AddInfrastructure(connectionString);

// Game session manager
builder.Services.AddSingleton<MtgDecker.Engine.GameSessionManager>();
builder.Services.AddHostedService<GameSessionCleanupService>();

// In-memory log viewer
var logStore = new InMemoryLogStore();
builder.Services.AddSingleton(logStore);
builder.Logging.AddProvider(new InMemoryLogProvider(logStore, TimeProvider.System));

var app = builder.Build();

// Auto-migrate database on startup
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MtgDeckerDbContext>();
    db.Database.Migrate();
}

// Seed card data for preset decks (fetches from Scryfall API if missing)
{
    using var cardSeedScope = app.Services.CreateScope();
    var mediator = cardSeedScope.ServiceProvider.GetRequiredService<IMediator>();
    var cardSeedResult = await mediator.Send(new SeedPresetCardDataCommand());

    if (cardSeedResult.SeededCount > 0)
        Console.WriteLine($"[Seed] Fetched {cardSeedResult.SeededCount} cards from Scryfall.");
    foreach (var name in cardSeedResult.NotFoundOnScryfall)
        Console.WriteLine($"[Seed] Card not found on Scryfall: {name}");
}

// Seed preset decks for game testing
{
    using var seedScope = app.Services.CreateScope();
    var mediator = seedScope.ServiceProvider.GetRequiredService<IMediator>();
    var seedResult = await mediator.Send(new SeedPresetDecksCommand());

    foreach (var name in seedResult.Created)
        Console.WriteLine($"[Seed] {name} deck created.");
    foreach (var name in seedResult.Skipped)
        Console.WriteLine($"[Seed] {name} — already exists, skipped.");
    foreach (var (name, cards) in seedResult.Unresolved)
        Console.WriteLine($"[Seed] {name} — unresolved: {string.Join(", ", cards)}");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();

    // Only allow game pages and static assets in production
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value ?? "/";

        var isAllowed = path == "/" ||
                        path.StartsWith("/game", StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWith("/_content", StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase) ||
                        path == "/not-found";

        if (!isAllowed)
        {
            context.Response.StatusCode = 404;
            return;
        }

        await next();
    });
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
