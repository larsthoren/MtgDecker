# Production Seeding & Game-Only Routing Design

## Context

The MtgDecker app deploys to Azure with a Free-tier SQL database (32MB). The full Scryfall bulk import is ~500MB and only needed for development (card search, deck building). In production, only preset decks and gameplay are needed.

Two problems to solve:
1. Preset deck seeding fails in production because cards don't exist in the DB without a bulk import
2. Non-game pages (Card Search, My Decks, Collection, Import, Logs) should be hidden and blocked in production

## Design

### 1. Production Card Seeding

**New Scryfall endpoint:** Add `FetchCardsByNamesAsync(IEnumerable<string> names)` to `IScryfallClient` using Scryfall's `POST /cards/collection` endpoint. Accepts up to 75 card identifiers per request, returns full card JSON.

**New command:** `SeedPresetCardDataCommand` — runs on startup before `SeedPresetDecksCommand`.

**Flow:**
1. Collect all unique card names across all 12 preset decks from `PresetDeckRegistry`
2. Query local DB via `ICardRepository.GetByNamesAsync()` for existing cards
3. Compute missing card names (requested minus found)
4. If none missing, return early (idempotent)
5. Fetch missing cards from Scryfall collection endpoint in batches of 75
6. Map responses to `Card` entities using existing `ScryfallCardMapper`
7. Upsert cards into the DB via `ICardRepository` (or a new bulk insert method)
8. Log results: cards seeded, any not-found on Scryfall

**Startup order in Program.cs:**
```
1. Database.Migrate()
2. SeedPresetCardDataCommand  ← NEW (fetch missing cards from Scryfall API)
3. SeedPresetDecksCommand     ← existing (create deck entries, now all cards resolve)
```

**Scryfall API details:**
- Endpoint: `POST https://api.scryfall.com/cards/collection`
- Body: `{"identifiers": [{"name": "Goblin Lackey"}, {"name": "Aether Vial"}, ...]}`
- Max 75 identifiers per request
- ~200-300 unique cards across 12 preset decks = 3-4 API calls
- Rate limit: 10 req/sec (well within bounds)
- Response includes `not_found` array for unresolved names

### 2. Production-Only Game Page

**Route middleware:** A `app.Use(...)` middleware registered only in production, before `MapRazorComponents`. Uses an allowlist approach:

Allowed paths:
- `/` (redirects to `/game/new`)
- `/game`, `/game/*`
- `/_content/*`, `/_framework/*`, `/_blazor/*` (static assets + SignalR)

All other paths return 404.

**Navigation:** `MainLayout.razor` injects `IWebHostEnvironment` and conditionally renders nav items. In production, only "Play Game" shows. In development, all items show as today.

**Home redirect:** `Home.razor` checks `IWebHostEnvironment.IsProduction()`. If true, redirects to `/game/new` via `NavigationManager.NavigateTo("/game/new", replace: true)`. In development, shows the normal landing page.

### 3. Files Changed

| File | Change |
|------|--------|
| `IScryfallClient.cs` | Add `FetchCardsByNamesAsync` method |
| `ScryfallClient.cs` | Implement `POST /cards/collection` endpoint |
| `ScryfallCardMapper.cs` | May need minor updates if collection response differs from bulk |
| `SeedPresetCardDataCommand.cs` | **New** — fetches + upserts missing preset deck cards |
| `ICardRepository.cs` | Add bulk upsert method if needed |
| `CardRepository.cs` | Implement bulk upsert |
| `Program.cs` | Add `SeedPresetCardDataCommand` call, add route-blocking middleware |
| `MainLayout.razor` | Conditionally hide nav items in production |
| `Home.razor` | Redirect to `/game/new` in production |

### 4. Behavior Summary

| Environment | Card Search | Deck Building | Preset Decks | Game Play | Start Page |
|-------------|-------------|---------------|--------------|-----------|------------|
| Development | Full (bulk import) | Full | Seeded | Full | Landing page (`/`) |
| Production | 404 | 404 | Seeded (API fetch) | Full | `/game/new` (redirect) |
