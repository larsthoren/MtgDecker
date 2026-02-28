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

// Auto-migrate and seed in background so the app starts accepting traffic immediately
_ = Task.Run(async () =>
{
    try
    {
        // Auto-migrate database
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MtgDeckerDbContext>();
            db.Database.Migrate();
            Console.WriteLine("[Startup] Database migration complete.");
        }

        // Seed card data for preset decks (fetches from Scryfall API if missing)
        using (var cardSeedScope = app.Services.CreateScope())
        {
            var mediator = cardSeedScope.ServiceProvider.GetRequiredService<IMediator>();
            var cardSeedResult = await mediator.Send(new SeedPresetCardDataCommand());

            if (cardSeedResult.SeededCount > 0)
                Console.WriteLine($"[Seed] Fetched {cardSeedResult.SeededCount} cards from Scryfall.");
            foreach (var name in cardSeedResult.NotFoundOnScryfall)
                Console.WriteLine($"[Seed] Card not found on Scryfall: {name}");
        }

        // Seed preset decks for game testing
        using (var seedScope = app.Services.CreateScope())
        {
            var mediator = seedScope.ServiceProvider.GetRequiredService<IMediator>();
            var seedResult = await mediator.Send(new SeedPresetDecksCommand());

            foreach (var name in seedResult.Created)
                Console.WriteLine($"[Seed] {name} deck created.");
            foreach (var name in seedResult.Skipped)
                Console.WriteLine($"[Seed] {name} — already exists, skipped.");
            foreach (var (name, cards) in seedResult.Unresolved)
                Console.WriteLine($"[Seed] {name} — unresolved: {string.Join(", ", cards)}");
        }

        Console.WriteLine("[Startup] Seeding complete.");

        // Readiness check — verify DB has the data the app needs
        using (var checkScope = app.Services.CreateScope())
        {
            var checkDb = checkScope.ServiceProvider.GetRequiredService<MtgDeckerDbContext>();
            var readiness = await GetReadinessAsync(checkDb);
            Console.WriteLine($"[Startup] Readiness: {readiness.CardCount} cards, {readiness.SystemDeckCount} system decks, {readiness.FormatSummary}");
            if (readiness.CardCount == 0)
                Console.WriteLine("[Startup] WARNING: No cards in database — game lobby will have no decks to choose.");
            if (readiness.SystemDeckCount == 0)
                Console.WriteLine("[Startup] WARNING: No system decks — game lobby will be empty.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Startup] Migration/seeding failed: {ex.Message}");
    }
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
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
                        path.StartsWith("/Components", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
                        path == "/not-found" ||
                        path.StartsWith("/health", StringComparison.OrdinalIgnoreCase);

        if (!isAllowed)
        {
            context.Response.StatusCode = 404;
            return;
        }

        await next();
    });
}
app.UseStatusCodePagesWithReExecute("/not-found");

// Skip HTTPS redirection — Azure Container Apps handles TLS termination
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok("healthy"));
app.MapGet("/health/ready", async (MtgDeckerDbContext db) =>
{
    var readiness = await GetReadinessAsync(db);
    return Results.Ok(readiness);
});
app.UseStaticFiles();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static async Task<ReadinessResult> GetReadinessAsync(MtgDeckerDbContext db)
{
    var cardCount = await db.Cards.CountAsync();
    var systemDecks = await db.Decks
        .Where(d => d.UserId == null)
        .Select(d => new { d.Name, d.Format })
        .ToListAsync();
    var userDeckCount = await db.Decks.CountAsync(d => d.UserId != null);

    var byFormat = systemDecks
        .GroupBy(d => d.Format)
        .ToDictionary(g => g.Key.ToString(), g => g.Count());

    var formatSummary = byFormat.Count > 0
        ? string.Join(", ", byFormat.Select(kv => $"{kv.Key}: {kv.Value}"))
        : "none";

    return new ReadinessResult(
        cardCount,
        systemDecks.Count,
        userDeckCount,
        byFormat,
        systemDecks.Select(d => d.Name).Order().ToList(),
        formatSummary);
}

record ReadinessResult(
    int CardCount,
    int SystemDeckCount,
    int UserDeckCount,
    Dictionary<string, int> DecksByFormat,
    List<string> SystemDeckNames,
    string FormatSummary);
