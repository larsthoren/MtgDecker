using MediatR;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using MtgDecker.Application;
using MtgDecker.Application.DeckExport;
using MtgDecker.Domain.Enums;
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

// In-memory log viewer
var logStore = new InMemoryLogStore();
builder.Services.AddSingleton(logStore);
builder.Logging.AddProvider(new InMemoryLogProvider(logStore, TimeProvider.System));

var app = builder.Build();

// Auto-migrate database on startup (dev only; use explicit migrations in production)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MtgDeckerDbContext>();
    db.Database.Migrate();
}

// Seed sample decks for game testing
if (app.Environment.IsDevelopment())
{
    using var seedScope = app.Services.CreateScope();
    var mediator = seedScope.ServiceProvider.GetRequiredService<IMediator>();
    var seedUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    var existingDecks = await mediator.Send(new MtgDecker.Application.Decks.ListDecksQuery(seedUserId));

    if (!existingDecks.Any(d => d.Name == "Legacy Goblins"))
    {
        var goblinsDeck = """
            4 Goblin Lackey
            4 Goblin Matron
            4 Goblin Piledriver
            4 Goblin Ringleader
            4 Goblin Warchief
            4 Mogg Fanatic
            3 Gempalm Incinerator
            3 Siege-Gang Commander
            1 Goblin King
            1 Goblin Pyromancer
            1 Goblin Sharpshooter
            1 Goblin Tinkerer
            1 Skirk Prospector
            2 Naturalize
            8 Mountain
            4 Karplusan Forest
            4 Rishadan Port
            4 Wooded Foothills
            2 Wasteland
            1 Forest
            SB: 4 Pyroblast
            SB: 2 Naturalize
            SB: 2 Pyrokinesis
            SB: 2 Tormod's Crypt
            SB: 2 Tranquil Domain
            SB: 1 Anarchy
            SB: 1 Goblin Tinkerer
            SB: 1 Sulfuric Vortex
            """;
        var result = await mediator.Send(new ImportDeckCommand(goblinsDeck, "MTGO", "Legacy Goblins", Format.Legacy, seedUserId));
        if (result.UnresolvedCards.Count > 0)
            Console.WriteLine($"[Seed] Legacy Goblins — unresolved: {string.Join(", ", result.UnresolvedCards)}");
        else
            Console.WriteLine("[Seed] Legacy Goblins deck created.");
    }

    if (!existingDecks.Any(d => d.Name == "Legacy Enchantress"))
    {
        var enchantressDeck = """
            4 Argothian Enchantress
            3 Swords to Plowshares
            2 Replenish
            4 Enchantress's Presence
            4 Wild Growth
            3 Exploration
            3 Mirri's Guile
            3 Opalescence
            3 Parallax Wave
            3 Sterling Grove
            2 Aura of Silence
            2 Seal of Cleansing
            1 Solitary Confinement
            1 Sylvan Library
            7 Forest
            4 Brushland
            4 Windswept Heath
            3 Plains
            3 Serra's Sanctum
            1 Wooded Foothills
            SB: 2 Carpet of Flowers
            SB: 2 Circle of Protection: Red
            SB: 2 Gaea's Blessing
            SB: 2 Tormod's Crypt
            SB: 2 Xantid Swarm
            SB: 1 Seal of Cleansing
            SB: 1 Solitary Confinement
            SB: 1 Swords to Plowshares
            SB: 1 Tsabo's Web
            SB: 1 Worship
            """;
        var result = await mediator.Send(new ImportDeckCommand(enchantressDeck, "MTGO", "Legacy Enchantress", Format.Legacy, seedUserId));
        if (result.UnresolvedCards.Count > 0)
            Console.WriteLine($"[Seed] Legacy Enchantress — unresolved: {string.Join(", ", result.UnresolvedCards)}");
        else
            Console.WriteLine("[Seed] Legacy Enchantress deck created.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
