# MtgDecker

Magic: The Gathering deck builder web application.

## Tech Stack

- .NET 10, C# 14
- Blazor Web App (InteractiveServer render mode)
- MudBlazor 8.x (UI component library)
- EF Core 10 with SQL Server Express LocalDB
- MediatR + FluentValidation (CQRS pipeline with validation behavior)
- xUnit + FluentAssertions + NSubstitute (testing)

## Architecture

Clean Architecture with four layers. Dependencies flow inward only.

```
Domain (zero dependencies)
  └─ Application (MediatR, FluentValidation)
       └─ Infrastructure (EF Core, HttpClient, Parsers)
       └─ Web (Blazor, MudBlazor)
Engine (standalone, no EF Core — referenced by Web)
```

- **Domain**: Entities, value objects, enums, domain services, exceptions. No external dependencies.
- **Application**: Commands/queries (MediatR handlers), validators, repository interfaces. Owns `TimeProvider` registration.
- **Infrastructure**: EF Core DbContext, repositories, Scryfall API client, deck parsers (MTGO/Arena).
- **Engine**: Standalone game engine — turn loop, combat, stack, triggers, mana system, AI bots. No EF Core dependency.
- **Web**: Blazor pages/components, MudBlazor dialogs, in-memory log viewer, game UI.

## Building & Running

No .sln file. Build/test individual projects:

```bash
# Must set PATH first in MSYS/Git Bash
export PATH="/c/Program Files/dotnet:$PATH"

# Build
dotnet build src/MtgDecker.Web/

# Run
dotnet run --project src/MtgDecker.Web/

# Test (run each project separately)
dotnet test tests/MtgDecker.Domain.Tests/
dotnet test tests/MtgDecker.Application.Tests/
dotnet test tests/MtgDecker.Infrastructure.Tests/
dotnet test tests/MtgDecker.Engine.Tests/
```

Database auto-migrates on startup in Development environment. For production, run migrations explicitly.

## Project Structure

```
src/
  MtgDecker.Domain/
    Entities/          Card, Deck, DeckEntry, CollectionEntry, User, CardFace
    Enums/             Format, DeckCategory, CardCondition, LegalityStatus
    ValueObjects/      CardLegality (IEquatable)
    Services/          SampleHandSimulator, ShortageCalculator
    Rules/             FormatRules
    Exceptions/        DomainException
  MtgDecker.Application/
    Cards/             Search, autocomplete, get-by-id/name queries
    Decks/             CRUD commands + validators
    Collection/        Add/remove/update/search commands
    DeckExport/        Import (MTGO/Arena parser) + Export (Text/CSV)
    Stats/             GetDeckStatsQuery (mana curve, color dist, type breakdown, rarity)
    Interfaces/        Repository contracts, IScryfallClient, IDeckParser
    Behaviors/         ValidationBehavior (MediatR pipeline)
  MtgDecker.Infrastructure/
    Data/              MtgDeckerDbContext, Configurations/, Migrations/, Repositories/
    Scryfall/          ScryfallClient, BulkDataImporter, ScryfallCard, ScryfallCardMapper
    Parsers/           MtgoDeckParser, ArenaDeckParser
  MtgDecker.Engine/
    GameEngine.cs      Turn loop, combat, stack resolution, state-based actions
    GameState.cs       Players, phase tracking, game log, winner
    GameCard.cs        In-game card with zones, tapping, damage, triggers
    CardDefinitions.cs Registry of known cards (mana abilities, triggers, effects)
    AI/                AiBotDecisionHandler, BoardEvaluator
    Simulation/        SimulationRunner, SimulationResult, BatchResult
    Triggers/          IEffect, Trigger, ETB effects (tokens, tutor, reveal)
    Effects/           SpellEffect implementations (Naturalize, Swords)
    Mana/              ManaCost, ManaPool, ManaAbility, ManaColor
    Enums/             Phase, ActionType, ZoneType, CombatStep, GameEvent
  MtgDecker.Web/
    Components/Pages/  CardSearch, DeckBuilder, MyDecks, MyCollection, ImportData, Logs, Game/
    Services/          InMemoryLogStore/Provider

tests/
  MtgDecker.Domain.Tests/         91 tests
  MtgDecker.Application.Tests/    143 tests
  MtgDecker.Infrastructure.Tests/ 57 tests
  MtgDecker.Engine.Tests/         1248 tests
```

## Key Patterns

- **CQRS**: Commands mutate state, queries read. All go through MediatR.
- **Validation pipeline**: FluentValidation validators auto-discovered and run via `ValidationBehavior<,>` before handlers.
- **TimeProvider**: Injected into all handlers that set timestamps. Domain methods accept `DateTime? utcNow = null` parameter. Never use `DateTime.UtcNow` directly in application handlers.
- **Deck mutations**: All on the `Deck` entity (AddCard, RemoveCard, UpdateCardQuantity, MoveCardCategory). These enforce format rules (max copies, sideboard availability).
- **DeckCategory**: MainDeck, Sideboard, Maybeboard. Maybeboard bypasses copy limits.
- **Card data**: Imported from Scryfall bulk data API, streamed and upserted in batches of 1000.
- **Card images**: Scryfall CDN links stored on Card entity, not downloaded locally.
- **Single-user**: Hardcoded UserId in Web layer, data model supports multi-user.

## Game Engine

The `MtgDecker.Engine` project is a standalone game engine with no database dependency. Key concepts:

- **IPlayerDecisionHandler**: Interface for all player decisions (mulligan, actions, combat, targeting). `InteractiveDecisionHandler` uses TaskCompletionSource for UI; `AiBotDecisionHandler` uses heuristics; `TestDecisionHandler` returns queued responses.
- **Mana system**: ManaCost parsing from Scryfall strings (`{2}{R}{R}`), ManaPool, ManaAbility on lands, auto-tap support. Cards must be registered in `CardDefinitions` to be playable; unregistered cards are rejected with a log message.
- **Combat**: Full MTG combat — declare attackers/blockers, multi-block ordering, damage assignment, summoning sickness, creature death processing.
- **Stack**: Spells go on the stack, priority passes, targeted spells with `TargetFilter`/`SpellEffect`, counter-spell support. Stack resolves LIFO.
- **Triggers**: Event-driven system (`GameEvent` → `Trigger` → `IEffect`). ETB effects for token creation, tutor, reveal-and-filter. `ProcessTriggersAsync` fires at relevant points.
- **AI simulation**: `AiBotDecisionHandler` (heuristic AI) + `BoardEvaluator` (static scoring) + `SimulationRunner` for bot-vs-bot games with batch statistics.
- **Enforcement**: Summoning sickness prevents tapping creatures the turn they enter (unless haste). Undo is scoped to mana taps only (untap individual lands via UI affordance). Target cancellation lets players back out of targeting sequences. No sandbox/freeform actions — the engine enforces all rules.
- **State-based actions**: Deck-out (MTG 104.3c) and life-check after combat. `GameState.Winner` tracks game outcome.

## Critical: EF Core + Blazor Server Pitfalls

**Never set `Id = Guid.NewGuid()` on entities added to tracked navigation properties.**
EF Core treats Guid keys as `ValueGeneratedOnAdd` by convention. When an untracked entity with a non-default key (e.g. `Guid.NewGuid()`) is discovered in a tracked collection during `DetectChanges`, EF marks it as `Modified` (existing) instead of `Added` (new), causing a failed UPDATE instead of INSERT. Leave the `Id` as `Guid.Empty` — EF will generate it on insert. This only applies to entities added via navigation properties; explicit `context.Add()` correctly forces `Added` state regardless of key value.

**DbContext is scoped per-circuit in Blazor Server (long-lived).**
A `DbContextResetBehavior` MediatR pipeline clears the change tracker before each request to prevent stale entity state across requests. Do not remove this behavior. If adding new repositories or handlers, ensure they don't rely on cross-request change tracking.

**Use `CultureInfo.InvariantCulture` for all price/decimal formatting.**
`ToString("F2")` without a culture uses the thread's current culture, which may use comma decimal separators. Always pass `CultureInfo.InvariantCulture`. The `System.Globalization` namespace is imported in `_Imports.razor`.

## Conventions

- TDD: Write failing test first, implement minimal code, refactor.
- Domain entity mutations go through methods, not direct property sets.
- Validators live in the same file as their command/query record.
- One handler per command/query. Handler class in same file as record.
- DTOs and filter types get their own files under Interfaces/.
- Dark theme by default (MTG aesthetic), light theme toggle available.

## Git Branch Strategy

- **`develop`** — default/integration branch. Feature branches merge here via PR.
- **`main`** — release/deploy branch. Merging to `main` triggers CI/CD deploy to Azure.
- Feature branches → PR to `develop` (daily work)
- `develop` → PR to `main` (release/deploy)
- No direct push to `main` or `develop` (branch protection enabled)

## Workflow

- Always use superpowers skills when applicable (TDD, code review, executing plans, writing plans, brainstorming, systematic debugging, verification before completion, etc.).
- Use `superpowers:requesting-code-review` after completing features or before merging.
- Use `superpowers:test-driven-development` when implementing any feature or bugfix.
- Use `superpowers:writing-plans` before multi-step implementation work.
- Use `superpowers:brainstorming` before creative work like new features or design changes.

## Environment Notes

- Windows with MSYS2/Git Bash shell
- `tail`, `head` commands not available in this MSYS environment
- Must export dotnet PATH before every shell session
- SQL Server Express LocalDB for database
- Connection string defaults to `Server=(localdb)\\mssqllocaldb;Database=MtgDecker;Trusted_Connection=True;`
