# Phase 3: Scryfall Integration & Deck Parsers

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement Scryfall bulk data client, JSON-to-domain card mapper, bulk data importer, and MTGO/Arena deck text parsers.

**Architecture:** Infrastructure layer implements IScryfallClient and IBulkDataImporter from Application. Scryfall JSON is deserialized into DTOs, then mapped to domain Card entities. Deck parsers implement IDeckParser interface defined in Application.

**Tech Stack:** System.Text.Json, HttpClient, xUnit, FluentAssertions

**Prerequisites:** Phase 2 complete (82 tests passing).

**Run commands with:** `export PATH="/c/Program Files/dotnet:$PATH"`

---

### Task 1: Scryfall DTOs and Card Mapper

**Files:**
- Create: `src/MtgDecker.Infrastructure/Scryfall/ScryfallCard.cs`
- Create: `src/MtgDecker.Infrastructure/Scryfall/ScryfallCardMapper.cs`
- Create: `tests/MtgDecker.Infrastructure.Tests/Scryfall/ScryfallCardMapperTests.cs`

### Task 2: ScryfallClient Implementation with Tests

**Files:**
- Create: `src/MtgDecker.Infrastructure/Scryfall/ScryfallClient.cs`
- Create: `tests/MtgDecker.Infrastructure.Tests/Scryfall/ScryfallClientTests.cs`

### Task 3: BulkDataImporter with Tests

**Files:**
- Create: `src/MtgDecker.Infrastructure/Scryfall/BulkDataImporter.cs`
- Create: `tests/MtgDecker.Infrastructure.Tests/Scryfall/BulkDataImporterTests.cs`
- Create: `tests/MtgDecker.Infrastructure.Tests/Scryfall/Fixtures/sample-cards.json`

### Task 4: IDeckParser Interface and Deck Parsers with Tests

**Files:**
- Create: `src/MtgDecker.Application/Interfaces/IDeckParser.cs`
- Create: `src/MtgDecker.Infrastructure/Parsers/MtgoDeckParser.cs`
- Create: `src/MtgDecker.Infrastructure/Parsers/ArenaDeckParser.cs`
- Create: `tests/MtgDecker.Infrastructure.Tests/Parsers/MtgoDeckParserTests.cs`
- Create: `tests/MtgDecker.Infrastructure.Tests/Parsers/ArenaDeckParserTests.cs`

### Task 5: Update DI Registration and Final Verification

**Files:**
- Modify: `src/MtgDecker.Infrastructure/DependencyInjection.cs`

---

## Phase 3 Complete

After this phase:
- ScryfallClient for downloading bulk data metadata and streams
- ScryfallCardMapper converting JSON DTOs to domain entities
- BulkDataImporter streaming large JSON files with batch processing
- MTGO and Arena deck text parsers
- Full test coverage for all new code
