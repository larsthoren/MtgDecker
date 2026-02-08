# MtgDecker - Design Document

## Overview

An advanced Magic: The Gathering deck builder built with .NET 10, Blazor Web App (MudBlazor), Entity Framework Core, and SQL Server. Supports format-legal deckbuilding, personal collection tracking, and deck import/export.

## Decisions

| Decision | Choice |
|---|---|
| Framework | .NET 10 |
| UI | Blazor Web App, InteractiveServer rendering, MudBlazor |
| Database | SQL Server Express / LocalDB (free) |
| ORM | Entity Framework Core |
| Architecture | Clean Architecture with pragmatic feature slicing |
| Card data source | Scryfall bulk data download |
| Card images | Scryfall CDN (linked, not stored locally) |
| Format legality | Automatic from Scryfall data |
| Formats | Vintage, Legacy, Premodern, Modern, Pauper, Commander |
| Commander-specific rules | Deferred (legality only for now) |
| Users | Single-user, designed for multi-user later |
| Deck views | Toggleable card image grid and data table |
| Analytics | Essentials first (mana curve, color dist, type breakdown), designed for more |
| Deck export/import | MTGO and Arena text formats |
| Testing | TDD with xUnit, FluentAssertions, NSubstitute |

## Solution Structure

```
MtgDecker.sln
├── src/
│   ├── MtgDecker.Domain            (entities, enums, value objects - no dependencies)
│   ├── MtgDecker.Application       (use cases by feature - references Domain)
│   ├── MtgDecker.Infrastructure    (EF Core, Scryfall client - references Application + Domain)
│   └── MtgDecker.Web               (Blazor Web App, MudBlazor - references Application + Infrastructure)
└── tests/
    ├── MtgDecker.Domain.Tests
    ├── MtgDecker.Application.Tests
    └── MtgDecker.Infrastructure.Tests
```

**Dependency flow (strict):**
- Domain -> nothing
- Application -> Domain
- Infrastructure -> Application + Domain
- Web -> Application + Infrastructure (for DI registration)

**Key NuGet packages:**
- Domain: none
- Application: MediatR, FluentValidation
- Infrastructure: EF Core, EF Core SqlServer, System.Net.Http
- Web: MudBlazor

## Domain Layer

### Entities

**Card** - A unique card printing.
- `ScryfallId`, `OracleId`, `Name`, `ManaCost`, `Cmc`, `TypeLine`, `OracleText`
- `Colors`, `ColorIdentity`, `Rarity`, `SetCode`, `SetName`
- `ImageUri`, `ImageUriSmall`, `ImageUriArtCrop`
- Multi-faced cards have a `CardFaces` collection

**CardFace** - For multi-faced cards.
- `Name`, `ManaCost`, `TypeLine`, `OracleText`, `ImageUri`

**CardLegality** - Value object on Card.
- Dictionary of format name to legality status: `Legal`, `Banned`, `Restricted`, `NotLegal`

**Deck**
- `Id`, `Name`, `Format`, `Description`, `CreatedAt`, `UpdatedAt`, `UserId`
- Contains a collection of `DeckEntry` items

**DeckEntry**
- `CardId` (specific printing for preferred art/set), `Quantity`, `Category` (enum: `MainDeck`, `Sideboard`)
- Validates quantity limits based on format (4-of for 60-card formats, basic lands exempt)

**CollectionEntry** - Tracks physical card ownership.
- `UserId`, `CardId` (specific printing), `Quantity`, `IsFoil`
- `Condition` (enum: `Mint`, `NearMint`, `LightlyPlayed`, `Played`, `Damaged`)

**User**
- `Id`, `DisplayName` (minimal, ready for auth later)

**Format** - Enum with rules.
- `Vintage`, `Legacy`, `Premodern`, `Modern`, `Pauper`, `Commander`
- Properties: `MinDeckSize`, `MaxDeckSize`, `MaxCopies`, `HasSideboard`

### Domain Rules
- Deck size validation per format
- Copy limits per format (basic lands exempt)
- Shortage calculation: diff deck entries vs collection entries (matched by OracleId)

## Application Layer

Organized by feature folder. Each folder contains commands, queries, validators, and DTOs. Uses MediatR for dispatching, FluentValidation for input validation via MediatR pipeline behavior.

### Feature Folders

**Cards/**
- `SearchCardsQuery` - Text search with combinable filters: format, color, type, CMC, rarity, set. Paged results.
- `GetCardByIdQuery`, `GetCardByNameQuery`

**Decks/**
- `CreateDeckCommand`, `UpdateDeckCommand`, `DeleteDeckCommand`
- `GetDeckQuery`, `ListDecksQuery`
- `AddCardToDeckCommand`, `RemoveCardFromDeckCommand`, `UpdateCardQuantityCommand`
- `GetDeckShortagesQuery` - Compares deck against collection

**Collection/**
- `AddToCollectionCommand`, `RemoveFromCollectionCommand`, `UpdateCollectionEntryCommand`
- `SearchCollectionQuery`, `GetCollectionStatsQuery`

**Import/**
- `ImportBulkDataCommand` - Downloads and parses Scryfall bulk JSON, upserts into database
- `CheckForUpdatesQuery` - Compares local timestamp against Scryfall bulk data metadata

**DeckExport/**
- `ExportDeckQuery` - Outputs MTGO or Arena format text
- `ImportDeckCommand` - Parses text list, resolves card names, creates deck

**Stats/**
- `GetDeckStatsQuery` - Mana curve, color distribution, type breakdown
- Standalone feature, extensible for deeper analytics later

### Interfaces (implemented in Infrastructure)
- `ICardRepository`, `ICollectionRepository`, `IDeckRepository`
- `IScryfallClient`, `IBulkDataImporter`

## Infrastructure Layer

### Database (EF Core + SQL Server)

- `MtgDeckerDbContext` with DbSets for all entities
- Indexes on: `Card.Name`, `Card.OracleId`, `Card.SetCode`, `Card.Colors`, `CollectionEntry.UserId`, `DeckEntry.DeckId`
- `BulkDataImportMetadata` table tracks last import timestamp
- EF Core migrations for schema management

### Scryfall Integration

- `ScryfallClient` - HttpClient wrapper. Calls bulk-data endpoint, streams JSON. Identifies via User-Agent header.
- `BulkDataImporter` - Streams large JSON using `System.Text.Json` with `JsonSerializer.DeserializeAsyncEnumerable`. Upserts in batches of 1000.

### Deck Text Parsers

- `MtgoDeckParser` - Parses `4 Lightning Bolt` and `SB: 2 Pyroblast` format
- `ArenaDeckParser` - Parses `4 Lightning Bolt (LEA) 162` with set code and collector number
- Common `IDeckParser` interface

## Web Layer (Blazor + MudBlazor)

### Layout
- Sidebar navigation: Card Search, My Decks, My Collection, Import Data
- `MudLayout` with `MudAppBar` and `MudNavMenu`
- Dark theme by default (MTG aesthetic), light theme toggle

### Pages

**Card Search** (`/cards`)
- Filter bar: text input, format dropdown, color checkboxes (WUBRG), type selector, CMC range slider, rarity, set
- Toggleable grid view (card images in `MudGrid`) and table view (`MudDataGrid`)
- Card click opens detail dialog: full art, oracle text, format legalities, "Add to Collection" / "Add to Deck" actions

**Deck Builder** (`/decks/{id}/edit`)
- Two-panel layout: card search (left), deck list (right)
- Deck list with main deck and sideboard sections
- Toggle between image view and compact table view
- Top stats bar: card count, mana curve mini-chart (`MudChart`), color pie
- Shortage indicators: cards not in collection show red badge with quantity needed

**My Decks** (`/decks`)
- Grid of deck cards: name, format, color identity, card count, last modified
- Click to edit, context menu for export/delete

**My Collection** (`/collection`)
- Searchable, filterable table of owned cards
- Columns: card name, set, quantity, foil status, condition
- Bulk add via set browsing

**Import Data** (`/admin/import`)
- Import trigger button, progress indicator, last-imported timestamp

## Data Flow: Key User Journeys

### First-Time Setup
1. Launch app -> empty state with prompt to import
2. Click "Import Data" -> downloads Scryfall "Default Cards" bulk file (~80MB JSON)
3. Progress bar during streaming import, cards upserted in batches
4. Complete -> card search functional

### Building a Deck
1. Create new deck -> pick name and format
2. Deck builder opens: search left, empty deck right
3. Search cards -> results filtered to format-legal only
4. Click/drag to add -> deck list updates, stats recalculate live
5. Cards not owned show shortage badge
6. Toggle between image grid and table view
7. Save -> persists to database

### Managing Collection
1. Navigate to Collection -> search cards owned
2. Add entries: specific printing, quantity, foil, condition
3. Shortage indicators across all decks update

### Exporting a Deck
1. From deck view, click Export -> choose MTGO or Arena format
2. Text generated, copied to clipboard or downloaded as .txt

### Importing a Deck
1. Paste deck list -> parser resolves card names, creates deck, flags unrecognized cards

## Testing Strategy

**TDD workflow:** Write test first (red), implement minimum to pass (green), refactor.

**MtgDecker.Domain.Tests**
- Pure unit tests, no mocks. Format rules, deck validation, shortage calculation.

**MtgDecker.Application.Tests**
- Unit tests with mocked repositories (NSubstitute). Test each handler in isolation.
- FluentValidation rule tests for input rejection.

**MtgDecker.Infrastructure.Tests**
- Integration tests against SQL Server LocalDB. EF Core mappings, migrations, query correctness.
- Scryfall parser tests using fixture JSON files (subsets of real data).
- Deck text parser tests with sample deck lists and edge cases.

No UI tests initially. MudBlazor components are well-tested upstream, UI is thin over Application layer.

## Deferred Features
- Commander-specific rules (singleton, color identity, commander zone)
- Deep analytics (draw probability, opening hand simulator)
- Multi-user authentication
- Additional deck export formats
