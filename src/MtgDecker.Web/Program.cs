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

// Auto-migrate database on startup (dev only; use explicit migrations in production)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MtgDeckerDbContext>();
    db.Database.Migrate();
}

// Seed preset decks for game testing
if (app.Environment.IsDevelopment())
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
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
