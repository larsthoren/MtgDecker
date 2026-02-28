# Production Seeding & Game-Only Routing Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Enable preset deck seeding in production (without full Scryfall bulk import) and restrict the UI to game-only pages in production.

**Architecture:** Add a Scryfall collection endpoint to fetch individual cards by name, a new startup command to seed missing card data, and environment-aware middleware + nav to restrict production to game pages only.

**Tech Stack:** .NET 10, Blazor, MediatR, EF Core, Scryfall REST API, xUnit + FluentAssertions + NSubstitute

**Worktree:** `C:\Users\larst\MtgDecker\.worktrees\production-seeding` (branch: `feat/production-seeding-and-routing`)

---

### Task 1: Add FetchCardsByNamesAsync to IScryfallClient and ScryfallClient

Add a method to fetch cards from Scryfall's `POST /cards/collection` endpoint in batches of 75.

**Files:**
- Modify: `src/MtgDecker.Application/Interfaces/IScryfallClient.cs`
- Modify: `src/MtgDecker.Infrastructure/Scryfall/ScryfallClient.cs`
- Modify: `src/MtgDecker.Infrastructure/Scryfall/ScryfallCard.cs` (add response DTO)
- Test: `tests/MtgDecker.Infrastructure.Tests/Scryfall/ScryfallClientTests.cs`

**Step 1: Add the collection response DTO to ScryfallCard.cs**

Add at the end of `src/MtgDecker.Infrastructure/Scryfall/ScryfallCard.cs`:

```csharp
public class ScryfallCollectionResponse
{
    [JsonPropertyName("data")]
    public List<ScryfallCard> Data { get; set; } = new();

    [JsonPropertyName("not_found")]
    public List<ScryfallCardIdentifier> NotFound { get; set; } = new();
}

public class ScryfallCardIdentifier
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
```

**Step 2: Add method to IScryfallClient interface**

Add to `src/MtgDecker.Application/Interfaces/IScryfallClient.cs`:

```csharp
Task<List<ScryfallCard>> FetchCardsByNamesAsync(IEnumerable<string> names, CancellationToken ct = default);
```

Note: `IScryfallClient` is in the Application layer but references `ScryfallCard` which is in Infrastructure. Since the interface already depends on `BulkDataInfo` from Application, the new method should return a type from Application too. However, `ScryfallCard` is the existing pattern used by the bulk importer. For consistency, keep the same approach — the Application layer references the Infrastructure Scryfall types transitively through the interface. Check how the existing methods work: they return `BulkDataInfo` (Application type) and `Stream`. The new method should return something the Application layer can use. Since `ScryfallCardMapper` is in Infrastructure, and `Card` is in Domain, the cleanest approach is to have the client return `ScryfallCard` objects (same as bulk import pattern) and let the caller use `ScryfallCardMapper`.

Actually, looking at the imports: `IScryfallClient` only references `BulkDataInfo` and `Stream` — no `ScryfallCard`. The `ScryfallCard` type lives in Infrastructure. So the interface method should not return `ScryfallCard`. Instead, add a new Application-layer DTO or have the Infrastructure implementation handle mapping internally.

**Revised approach:** Keep the interface clean. Add the method to `IScryfallClient` returning `List<Card>` (Domain entity). The `ScryfallClient` implementation fetches from Scryfall and maps internally using `ScryfallCardMapper`.

```csharp
// In IScryfallClient.cs — add this using
using MtgDecker.Domain.Entities;

// Add method
Task<(List<Card> Found, List<string> NotFound)> FetchCardsByNamesAsync(
    IEnumerable<string> names, CancellationToken ct = default);
```

**Step 3: Implement in ScryfallClient**

Add to `src/MtgDecker.Infrastructure/Scryfall/ScryfallClient.cs`:

```csharp
public async Task<(List<Card> Found, List<string> NotFound)> FetchCardsByNamesAsync(
    IEnumerable<string> names, CancellationToken ct = default)
{
    var nameList = names.ToList();
    var allCards = new List<Card>();
    var allNotFound = new List<string>();

    // Scryfall collection endpoint accepts max 75 identifiers per request
    foreach (var batch in nameList.Chunk(75))
    {
        var identifiers = batch.Select(n => new { name = n }).ToArray();
        var requestBody = new { identifiers };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("cards/collection", content, ct);
        response.EnsureSuccessStatusCode();

        var responseStream = await response.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync<ScryfallCollectionResponse>(
            responseStream, cancellationToken: ct);

        if (result?.Data != null)
        {
            allCards.AddRange(result.Data.Select(ScryfallCardMapper.MapToCard));
        }

        if (result?.NotFound != null)
        {
            allNotFound.AddRange(result.NotFound
                .Where(nf => nf.Name != null)
                .Select(nf => nf.Name!));
        }

        // Respect Scryfall rate limit (100ms between requests)
        if (batch != nameList.Chunk(75).Last())
            await Task.Delay(100, ct);
    }

    return (allCards, allNotFound);
}
```

Add `using System.Text;` and `using MtgDecker.Domain.Entities;` to the top of `ScryfallClient.cs`.

**Step 4: Write tests for FetchCardsByNamesAsync**

Add to `tests/MtgDecker.Infrastructure.Tests/Scryfall/ScryfallClientTests.cs`:

```csharp
[Fact]
public async Task FetchCardsByNamesAsync_ReturnsMappedCards()
{
    var collectionResponse = new
    {
        data = new[]
        {
            new
            {
                id = "abc-123",
                oracle_id = "oracle-1",
                name = "Lightning Bolt",
                mana_cost = "{R}",
                cmc = 1.0,
                type_line = "Instant",
                oracle_text = "Deal 3 damage to any target.",
                colors = new[] { "R" },
                color_identity = new[] { "R" },
                rarity = "common",
                set = "lea",
                set_name = "Limited Edition Alpha",
                collector_number = "161",
                layout = "normal",
                image_uris = new { normal = "https://cards.scryfall.io/normal/bolt.jpg", small = (string?)null, art_crop = (string?)null },
                legalities = new Dictionary<string, string> { ["legacy"] = "legal" },
                prices = new { usd = "1.00", usd_foil = (string?)null, eur = (string?)null, eur_foil = (string?)null, tix = (string?)null }
            }
        },
        not_found = Array.Empty<object>()
    };

    var handler = new FakeHttpMessageHandler(JsonSerializer.Serialize(collectionResponse));
    var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.scryfall.com/") };
    var client = new ScryfallClient(httpClient);

    var (found, notFound) = await client.FetchCardsByNamesAsync(new[] { "Lightning Bolt" });

    found.Should().HaveCount(1);
    found[0].Name.Should().Be("Lightning Bolt");
    found[0].ManaCost.Should().Be("{R}");
    found[0].TypeLine.Should().Be("Instant");
    notFound.Should().BeEmpty();
}

[Fact]
public async Task FetchCardsByNamesAsync_ReportsNotFoundCards()
{
    var collectionResponse = new
    {
        data = Array.Empty<object>(),
        not_found = new[] { new { name = "Nonexistent Card" } }
    };

    var handler = new FakeHttpMessageHandler(JsonSerializer.Serialize(collectionResponse));
    var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.scryfall.com/") };
    var client = new ScryfallClient(httpClient);

    var (found, notFound) = await client.FetchCardsByNamesAsync(new[] { "Nonexistent Card" });

    found.Should().BeEmpty();
    notFound.Should().Contain("Nonexistent Card");
}
```

**Step 5: Run tests**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
cd C:\Users\larst\MtgDecker\.worktrees\production-seeding
dotnet test tests/MtgDecker.Infrastructure.Tests/ --verbosity quiet
```

Expected: All tests pass (existing + 2 new).

**Step 6: Commit**

```bash
git add -A
git commit -m "feat: add FetchCardsByNamesAsync to ScryfallClient

Implement Scryfall POST /cards/collection endpoint for fetching
individual cards by name in batches of 75. Returns mapped Card
entities and a list of not-found card names."
```

---

### Task 2: Create SeedPresetCardDataCommand

New MediatR command that collects all card names from preset decks, checks which are missing from the DB, fetches them from Scryfall, and upserts them.

**Files:**
- Create: `src/MtgDecker.Application/DeckExport/SeedPresetCardDataCommand.cs`
- Test: `tests/MtgDecker.Application.Tests/DeckExport/SeedPresetCardDataCommandTests.cs`

**Step 1: Create the command, result, and handler**

Create `src/MtgDecker.Application/DeckExport/SeedPresetCardDataCommand.cs`:

```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using MtgDecker.Application.Interfaces;

namespace MtgDecker.Application.DeckExport;

public record SeedPresetCardDataCommand() : IRequest<SeedPresetCardDataResult>;

public record SeedPresetCardDataResult(int SeededCount, List<string> NotFoundOnScryfall);

public class SeedPresetCardDataHandler : IRequestHandler<SeedPresetCardDataCommand, SeedPresetCardDataResult>
{
    private readonly ICardRepository _cardRepository;
    private readonly IScryfallClient _scryfallClient;
    private readonly ILogger<SeedPresetCardDataHandler> _logger;

    public SeedPresetCardDataHandler(
        ICardRepository cardRepository,
        IScryfallClient scryfallClient,
        ILogger<SeedPresetCardDataHandler> logger)
    {
        _cardRepository = cardRepository;
        _scryfallClient = scryfallClient;
        _logger = logger;
    }

    public async Task<SeedPresetCardDataResult> Handle(
        SeedPresetCardDataCommand request, CancellationToken cancellationToken)
    {
        // 1. Collect all unique card names from preset decks
        var allNames = PresetDeckRegistry.All
            .SelectMany(d => ParseCardNames(d.DeckTextMtgo))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 2. Check which cards already exist in DB
        var existingCards = await _cardRepository.GetByNamesAsync(allNames, cancellationToken);
        var existingNames = existingCards.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingNames = allNames.Where(n => !existingNames.Contains(n)).ToList();

        if (missingNames.Count == 0)
        {
            _logger.LogInformation("All {Count} preset deck cards already exist in database", allNames.Count);
            return new SeedPresetCardDataResult(0, new List<string>());
        }

        _logger.LogInformation("Fetching {Count} missing cards from Scryfall", missingNames.Count);

        // 3. Fetch missing cards from Scryfall
        var (fetchedCards, notFound) = await _scryfallClient.FetchCardsByNamesAsync(missingNames, cancellationToken);

        // 4. Upsert fetched cards
        if (fetchedCards.Count > 0)
        {
            await _cardRepository.UpsertBatchAsync(fetchedCards, cancellationToken);
            _logger.LogInformation("Seeded {Count} cards from Scryfall", fetchedCards.Count);
        }

        if (notFound.Count > 0)
        {
            _logger.LogWarning("Cards not found on Scryfall: {Cards}", string.Join(", ", notFound));
        }

        return new SeedPresetCardDataResult(fetchedCards.Count, notFound);
    }

    private static IEnumerable<string> ParseCardNames(string deckText)
    {
        foreach (var line in deckText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;

            // Remove SB: prefix
            if (trimmed.StartsWith("SB:", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[3..].Trim();

            // Format: "4 Card Name" — skip the quantity
            var spaceIndex = trimmed.IndexOf(' ');
            if (spaceIndex > 0 && int.TryParse(trimmed[..spaceIndex], out _))
            {
                yield return trimmed[(spaceIndex + 1)..].Trim();
            }
        }
    }
}
```

**Step 2: Write tests**

Create `tests/MtgDecker.Application.Tests/DeckExport/SeedPresetCardDataCommandTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using MtgDecker.Application.DeckExport;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Tests.DeckExport;

public class SeedPresetCardDataCommandTests
{
    private readonly ICardRepository _cardRepository = Substitute.For<ICardRepository>();
    private readonly IScryfallClient _scryfallClient = Substitute.For<IScryfallClient>();
    private readonly ILogger<SeedPresetCardDataHandler> _logger = Substitute.For<ILogger<SeedPresetCardDataHandler>>();
    private readonly SeedPresetCardDataHandler _handler;

    public SeedPresetCardDataCommandTests()
    {
        _handler = new SeedPresetCardDataHandler(_cardRepository, _scryfallClient, _logger);
    }

    [Fact]
    public async Task SkipsScryfall_WhenAllCardsExist()
    {
        // Return a card for every name requested
        _cardRepository.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var names = callInfo.Arg<IEnumerable<string>>().ToList();
                return names.Select(n => new Card { Name = n }).ToList();
            });

        var result = await _handler.Handle(new SeedPresetCardDataCommand(), CancellationToken.None);

        result.SeededCount.Should().Be(0);
        result.NotFoundOnScryfall.Should().BeEmpty();
        await _scryfallClient.DidNotReceive()
            .FetchCardsByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FetchesMissingCards_FromScryfall()
    {
        // DB has no cards
        _cardRepository.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>());

        // Scryfall returns some cards
        _scryfallClient.FetchCardsByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var names = callInfo.Arg<IEnumerable<string>>().ToList();
                var cards = names.Take(5).Select(n => new Card { Name = n }).ToList();
                var notFound = names.Skip(5).Take(1).ToList();
                return (cards, notFound);
            });

        var result = await _handler.Handle(new SeedPresetCardDataCommand(), CancellationToken.None);

        result.SeededCount.Should().Be(5);
        await _cardRepository.Received(1)
            .UpsertBatchAsync(Arg.Any<IEnumerable<Card>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReportsNotFoundCards()
    {
        _cardRepository.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Card>());

        _scryfallClient.FetchCardsByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns((new List<Card>(), new List<string> { "Unknown Card" }));

        var result = await _handler.Handle(new SeedPresetCardDataCommand(), CancellationToken.None);

        result.NotFoundOnScryfall.Should().Contain("Unknown Card");
    }

    [Fact]
    public async Task ParsesCardNames_FromPresetRegistry()
    {
        // Verify the handler actually sends card names to the repository
        var capturedNames = new List<string>();
        _cardRepository.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedNames = callInfo.Arg<IEnumerable<string>>().ToList();
                return capturedNames.Select(n => new Card { Name = n }).ToList();
            });

        await _handler.Handle(new SeedPresetCardDataCommand(), CancellationToken.None);

        // Should contain known cards from preset decks
        capturedNames.Should().Contain("Goblin Lackey");
        capturedNames.Should().Contain("Mountain");
        // Should not contain duplicates
        capturedNames.Should().OnlyHaveUniqueItems(StringComparer.OrdinalIgnoreCase);
    }
}
```

**Step 3: Run tests**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
cd C:\Users\larst\MtgDecker\.worktrees\production-seeding
dotnet test tests/MtgDecker.Application.Tests/ --verbosity quiet
```

Expected: All tests pass (existing + 4 new).

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: add SeedPresetCardDataCommand for production card seeding

Fetches missing preset deck cards from Scryfall collection API and
upserts them into the database. Runs before SeedPresetDecksCommand
on startup to ensure all card data is available."
```

---

### Task 3: Wire up SeedPresetCardDataCommand in Program.cs

Add the new seed command to the startup sequence, before the existing `SeedPresetDecksCommand`.

**Files:**
- Modify: `src/MtgDecker.Web/Program.cs`

**Step 1: Add the card data seed call**

In `src/MtgDecker.Web/Program.cs`, find the seed block (lines 42-54) and add the card seed before it:

```csharp
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

// Seed preset decks for game testing (existing code)
{
    ...
}
```

Add `using MtgDecker.Application.DeckExport;` if not already imported (it should be, since `SeedPresetDecksCommand` is already used).

**Step 2: Build to verify**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
cd C:\Users\larst\MtgDecker\.worktrees\production-seeding
dotnet build src/MtgDecker.Web/
```

Expected: Build succeeded.

**Step 3: Commit**

```bash
git add src/MtgDecker.Web/Program.cs
git commit -m "feat: wire SeedPresetCardDataCommand into startup sequence

Runs before SeedPresetDecksCommand to ensure card data is available
in the database before creating preset deck entries."
```

---

### Task 4: Add production route-blocking middleware

Block non-game routes in production with a middleware that returns 404 for disallowed paths.

**Files:**
- Modify: `src/MtgDecker.Web/Program.cs`

**Step 1: Add the route-blocking middleware**

In `src/MtgDecker.Web/Program.cs`, add after the `UseHsts()` block (line 61) and before `UseStatusCodePagesWithReExecute`:

```csharp
// In production, only allow game pages and static assets
if (!app.Environment.IsDevelopment())
{
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
```

**Step 2: Build to verify**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
cd C:\Users\larst\MtgDecker\.worktrees\production-seeding
dotnet build src/MtgDecker.Web/
```

Expected: Build succeeded.

**Step 3: Commit**

```bash
git add src/MtgDecker.Web/Program.cs
git commit -m "feat: add production route-blocking middleware

Block non-game routes in production. Only allow /, /game/*, and
static asset paths. All other paths return 404."
```

---

### Task 5: Conditionally hide nav items and redirect home page

In production, only show "Play Game" in the nav menu and redirect `/` to `/game/new`.

**Files:**
- Modify: `src/MtgDecker.Web/Components/Layout/MainLayout.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/Home.razor`

**Step 1: Update MainLayout.razor**

Add `@inject IWebHostEnvironment Environment` at the top (after other `@inject` directives).

Wrap the non-game nav links with an environment check:

```razor
<MudNavMenu>
    @if (Environment.IsDevelopment())
    {
        <MudNavLink Href="/cards" Match="NavLinkMatch.Prefix"
                    Icon="@Icons.Material.Filled.Search">Card Search</MudNavLink>
        <MudNavLink Href="/decks" Match="NavLinkMatch.Prefix"
                    Icon="@Icons.Material.Filled.LibraryBooks">My Decks</MudNavLink>
        <MudNavLink Href="/collection" Match="NavLinkMatch.Prefix"
                    Icon="@Icons.Material.Filled.CollectionsBookmark">My Collection</MudNavLink>
    }
    <MudNavLink Href="/game/new" Match="NavLinkMatch.Prefix"
                Icon="@Icons.Material.Filled.SportsEsports">Play Game</MudNavLink>
    @if (Environment.IsDevelopment())
    {
        <MudNavLink Href="/admin/import" Match="NavLinkMatch.Prefix"
                    Icon="@Icons.Material.Filled.CloudDownload">Import Data</MudNavLink>
        <MudNavLink Href="/admin/logs" Match="NavLinkMatch.Prefix"
                    Icon="@Icons.Material.Filled.BugReport">Logs</MudNavLink>
    }
</MudNavMenu>
```

**Step 2: Update Home.razor**

Replace the contents of `src/MtgDecker.Web/Components/Pages/Home.razor`:

```razor
@page "/"
@inject NavigationManager Navigation
@inject IWebHostEnvironment Environment

@if (Environment.IsDevelopment())
{
    <PageTitle>MtgDecker</PageTitle>

    <MudContainer MaxWidth="MaxWidth.Medium" Class="mt-8">
        <MudCard>
            <MudCardContent Class="d-flex flex-column align-center pa-8">
                <MudText Typo="Typo.h3" Class="mb-4">MtgDecker</MudText>
                <MudText Typo="Typo.h6" Color="Color.Secondary" Class="mb-6">
                    Magic: The Gathering Deck Builder
                </MudText>
                <MudText Typo="Typo.body1" Class="mb-8" Align="Align.Center">
                    Build decks, track your collection, and manage format legality.
                </MudText>
                <MudButtonGroup Variant="Variant.Filled">
                    <MudButton Color="Color.Primary" Href="/cards" StartIcon="@Icons.Material.Filled.Search">
                        Search Cards
                    </MudButton>
                    <MudButton Color="Color.Secondary" Href="/decks" StartIcon="@Icons.Material.Filled.LibraryBooks">
                        My Decks
                    </MudButton>
                </MudButtonGroup>
            </MudCardContent>
        </MudCard>
    </MudContainer>
}

@code {
    protected override void OnInitialized()
    {
        if (!Environment.IsDevelopment())
        {
            Navigation.NavigateTo("/game/new", replace: true);
        }
    }
}
```

**Step 3: Build to verify**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
cd C:\Users\larst\MtgDecker\.worktrees\production-seeding
dotnet build src/MtgDecker.Web/
```

Expected: Build succeeded.

**Step 4: Commit**

```bash
git add src/MtgDecker.Web/Components/Layout/MainLayout.razor src/MtgDecker.Web/Components/Pages/Home.razor
git commit -m "feat: hide non-game pages and redirect home in production

In production, nav menu only shows Play Game. Home page redirects
to /game/new. In development, everything works as before."
```

---

### Task 6: Run all tests and verify build

Final verification that everything compiles and all tests pass.

**Step 1: Build the full project**

```bash
export PATH="/c/Program Files/dotnet:$PATH"
cd C:\Users\larst\MtgDecker\.worktrees\production-seeding
dotnet build src/MtgDecker.Web/
```

**Step 2: Run infrastructure tests**

```bash
dotnet test tests/MtgDecker.Infrastructure.Tests/ --verbosity quiet
```

**Step 3: Run application tests**

```bash
dotnet test tests/MtgDecker.Application.Tests/ --verbosity quiet
```

**Step 4: Verify git status is clean**

```bash
git status
```

All changes should be committed. Push and create PR targeting `develop`.

```bash
git push -u origin feat/production-seeding-and-routing
```

Create PR:
```bash
gh pr create --base develop --title "feat: production seeding and game-only routing" --body "..."
```
