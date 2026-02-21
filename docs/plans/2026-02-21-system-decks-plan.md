# System Decks Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make UserId nullable on Deck so that decks with null UserId are permanent system decks — visible to all users, not editable or deletable. Users can clone system decks to their own collection.

**Architecture:** Domain property change + EF migration + application-layer guards on all mutation handlers + new ListSystemDecksQuery and CloneDeckCommand + Web layer split into "Preset Decks" / "My Decks" sections with read-only mode on DeckBuilder for system decks.

**Tech Stack:** .NET 10, EF Core 10, MediatR, FluentValidation, MudBlazor, xUnit + FluentAssertions + NSubstitute

---

### Task 1: Domain — Make UserId nullable and add IsSystemDeck

**Files:**
- Modify: `src/MtgDecker.Domain/Entities/Deck.cs:15` (UserId property)

**Step 1: Change UserId to nullable and add computed property**

In `src/MtgDecker.Domain/Entities/Deck.cs`, change line 15:

```csharp
// Before:
public Guid UserId { get; set; }

// After:
public Guid? UserId { get; set; }
public bool IsSystemDeck => UserId is null;
```

**Step 2: Build to verify no compile errors in Domain**

Run: `dotnet build src/MtgDecker.Domain/`
Expected: Build succeeds (Domain has no dependents that would break)

**Step 3: Fix compile errors in Application layer**

The `Guid` → `Guid?` change will break `ListDecksQuery` handler and any code passing `UserId` to methods expecting `Guid`. Fix each:

In `src/MtgDecker.Application/Decks/ListDecksQuery.cs`, the handler calls `_deckRepository.ListByUserAsync(request.UserId, ct)` — `UserId` is still `Guid` on the query record, so no change needed here.

In `src/MtgDecker.Application/DeckExport/SeedPresetDecksCommand.cs`, the handler passes `request.UserId` to `ListDecksQuery` and `ImportDeckCommand` — both expect `Guid`, and `SeedPresetDecksCommand.UserId` is `Guid`, so no change needed yet.

Run: `dotnet build src/MtgDecker.Application/`
Expected: Build succeeds

**Step 4: Fix compile errors in Infrastructure layer**

Run: `dotnet build src/MtgDecker.Infrastructure/`
Expected: Build succeeds (DeckConfiguration references `UserId` property but EF config is fine with nullable)

**Step 5: Fix compile errors in Web layer**

Run: `dotnet build src/MtgDecker.Web/`
Expected: Build succeeds (Web passes hardcoded `Guid` to commands, `Deck.UserId` is now `Guid?` but that's compatible)

**Step 6: Run all tests to verify nothing broke**

Run: `dotnet test tests/MtgDecker.Domain.Tests/ && dotnet test tests/MtgDecker.Application.Tests/ && dotnet test tests/MtgDecker.Infrastructure.Tests/`
Expected: All pass

**Step 7: Commit**

```bash
git add src/MtgDecker.Domain/Entities/Deck.cs
git commit -m "feat(domain): make Deck.UserId nullable, add IsSystemDeck computed property"
```

---

### Task 2: Infrastructure — Update EF config and add ListSystemDecksAsync

**Files:**
- Modify: `src/MtgDecker.Infrastructure/Data/Configurations/DeckConfiguration.cs:16`
- Modify: `src/MtgDecker.Application/Interfaces/IDeckRepository.cs`
- Modify: `src/MtgDecker.Infrastructure/Data/Repositories/DeckRepository.cs`

**Step 1: Write failing test for ListSystemDecksAsync**

In `tests/MtgDecker.Infrastructure.Tests/Data/Repositories/DeckRepositoryTests.cs`, add:

```csharp
[Fact]
public async Task ListSystemDecksAsync_ReturnsOnlyDecksWithNullUserId()
{
    // Arrange
    using var context = TestDbContextFactory.Create();
    var repository = new DeckRepository(context);

    var systemDeck = new Deck
    {
        Name = "System Deck",
        Format = Format.Legacy,
        UserId = null,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
    var userDeck = new Deck
    {
        Name = "User Deck",
        Format = Format.Modern,
        UserId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    context.Decks.AddRange(systemDeck, userDeck);
    await context.SaveChangesAsync();

    // Act
    var result = await repository.ListSystemDecksAsync();

    // Assert
    result.Should().HaveCount(1);
    result[0].Name.Should().Be("System Deck");
    result[0].UserId.Should().BeNull();
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Infrastructure.Tests/ --filter ListSystemDecksAsync`
Expected: FAIL — `ListSystemDecksAsync` does not exist

**Step 3: Update DeckConfiguration to make UserId optional**

In `src/MtgDecker.Infrastructure/Data/Configurations/DeckConfiguration.cs`, change line 16:

```csharp
// Before:
builder.Property(d => d.UserId).IsRequired();

// After:
builder.Property(d => d.UserId).IsRequired(false);
```

Also add the missing `TotalMaybeboardCount` ignore (existing bug fix — add after line 26):

```csharp
builder.Ignore(d => d.TotalMaybeboardCount);
builder.Ignore(d => d.IsSystemDeck);
```

**Step 4: Add ListSystemDecksAsync to IDeckRepository**

In `src/MtgDecker.Application/Interfaces/IDeckRepository.cs`, add after `ListByUserAsync`:

```csharp
Task<List<Deck>> ListSystemDecksAsync(CancellationToken ct = default);
```

**Step 5: Implement ListSystemDecksAsync in DeckRepository**

In `src/MtgDecker.Infrastructure/Data/Repositories/DeckRepository.cs`, add after `ListByUserAsync`:

```csharp
public async Task<List<Deck>> ListSystemDecksAsync(CancellationToken ct = default)
{
    return await _context.Decks
        .Where(d => d.UserId == null)
        .OrderBy(d => d.Name)
        .ToListAsync(ct);
}
```

**Step 6: Run test to verify it passes**

Run: `dotnet test tests/MtgDecker.Infrastructure.Tests/ --filter ListSystemDecksAsync`
Expected: PASS

**Step 7: Run all infrastructure tests**

Run: `dotnet test tests/MtgDecker.Infrastructure.Tests/`
Expected: All pass

**Step 8: Commit**

```bash
git add src/MtgDecker.Infrastructure/Data/Configurations/DeckConfiguration.cs \
        src/MtgDecker.Application/Interfaces/IDeckRepository.cs \
        src/MtgDecker.Infrastructure/Data/Repositories/DeckRepository.cs \
        tests/MtgDecker.Infrastructure.Tests/Data/Repositories/DeckRepositoryTests.cs
git commit -m "feat(infra): make UserId optional in EF config, add ListSystemDecksAsync"
```

---

### Task 3: EF Migration — Make UserId nullable and set existing decks to null

**Files:**
- Create: New migration in `src/MtgDecker.Infrastructure/Data/Migrations/`

**Step 1: Generate the migration**

Run:
```bash
dotnet ef migrations add MakeUserIdNullable --project src/MtgDecker.Infrastructure/ --startup-project src/MtgDecker.Web/
```

**Step 2: Edit the migration to add data update**

In the generated migration file, add after the `AlterColumn` call in the `Up` method:

```csharp
migrationBuilder.Sql("UPDATE Decks SET UserId = NULL");
```

**Step 3: Build to verify**

Run: `dotnet build src/MtgDecker.Infrastructure/`
Expected: Build succeeds

**Step 4: Commit**

```bash
git add src/MtgDecker.Infrastructure/Data/Migrations/
git commit -m "feat(infra): add migration to make UserId nullable and null existing decks"
```

---

### Task 4: Application — Add ListSystemDecksQuery

**Files:**
- Create: `src/MtgDecker.Application/Decks/ListSystemDecksQuery.cs`
- Create: `tests/MtgDecker.Application.Tests/Decks/ListSystemDecksQueryTests.cs`

**Step 1: Write failing test**

Create `tests/MtgDecker.Application.Tests/Decks/ListSystemDecksQueryTests.cs`:

```csharp
using FluentAssertions;
using MtgDecker.Application.Decks;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;
using NSubstitute;
using Xunit;

namespace MtgDecker.Application.Tests.Decks;

public class ListSystemDecksQueryTests
{
    private readonly IDeckRepository _deckRepository = Substitute.For<IDeckRepository>();

    [Fact]
    public async Task Handle_ReturnsSystemDecks()
    {
        // Arrange
        var systemDecks = new List<Deck>
        {
            new() { Name = "Legacy Goblins", Format = Format.Legacy, UserId = null },
            new() { Name = "Modern Burn", Format = Format.Modern, UserId = null }
        };
        _deckRepository.ListSystemDecksAsync(Arg.Any<CancellationToken>())
            .Returns(systemDecks);

        var handler = new ListSystemDecksQuery.Handler(_deckRepository);
        var query = new ListSystemDecksQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(d => d.UserId.Should().BeNull());
    }

    [Fact]
    public async Task Handle_NoSystemDecks_ReturnsEmptyList()
    {
        // Arrange
        _deckRepository.ListSystemDecksAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Deck>());

        var handler = new ListSystemDecksQuery.Handler(_deckRepository);
        var query = new ListSystemDecksQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/MtgDecker.Application.Tests/ --filter ListSystemDecksQuery`
Expected: FAIL — class does not exist

**Step 3: Implement ListSystemDecksQuery**

Create `src/MtgDecker.Application/Decks/ListSystemDecksQuery.cs`:

```csharp
using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Decks;

public record ListSystemDecksQuery() : IRequest<List<Deck>>;

public class ListSystemDecksQueryHandler : IRequestHandler<ListSystemDecksQuery, List<Deck>>
{
    private readonly IDeckRepository _deckRepository;

    public ListSystemDecksQueryHandler(IDeckRepository deckRepository)
    {
        _deckRepository = deckRepository;
    }

    public async Task<List<Deck>> Handle(ListSystemDecksQuery request, CancellationToken ct)
    {
        return await _deckRepository.ListSystemDecksAsync(ct);
    }
}
```

**Step 4: Fix test — update handler class name reference**

The test references `ListSystemDecksQuery.Handler` but the handler is `ListSystemDecksQueryHandler`. Update the test to use `ListSystemDecksQueryHandler` or make the handler a nested class. Use the non-nested pattern (consistent with existing code like `ListDecksQueryHandler`). Update test:

```csharp
var handler = new ListSystemDecksQueryHandler(_deckRepository);
```

**Step 5: Run test to verify it passes**

Run: `dotnet test tests/MtgDecker.Application.Tests/ --filter ListSystemDecksQuery`
Expected: PASS

**Step 6: Commit**

```bash
git add src/MtgDecker.Application/Decks/ListSystemDecksQuery.cs \
        tests/MtgDecker.Application.Tests/Decks/ListSystemDecksQueryTests.cs
git commit -m "feat(app): add ListSystemDecksQuery"
```

---

### Task 5: Application — Add system deck guards to mutation handlers

**Files:**
- Modify: `src/MtgDecker.Application/Decks/DeleteDeckCommand.cs`
- Modify: `src/MtgDecker.Application/Decks/AddCardToDeckCommand.cs`
- Modify: `src/MtgDecker.Application/Decks/RemoveCardFromDeckCommand.cs`
- Modify: `src/MtgDecker.Application/Decks/UpdateCardQuantityCommand.cs`
- Modify: `src/MtgDecker.Application/Decks/MoveCardCategoryCommand.cs`
- Modify: `src/MtgDecker.Application/Decks/UpdateDeckFormatCommand.cs`
- Modify: corresponding test files for each

**Step 1: Write failing tests for system deck guards**

Add a test to each existing test file. The pattern is the same for all six — here is the template (showing `DeleteDeckCommand` as example):

In `tests/MtgDecker.Application.Tests/Decks/DeleteDeckCommandTests.cs`, add:

```csharp
[Fact]
public async Task Handle_SystemDeck_ThrowsInvalidOperationException()
{
    // Arrange
    var deckId = Guid.NewGuid();
    var systemDeck = new Deck { Id = deckId, Name = "System", UserId = null };
    _deckRepository.GetByIdAsync(deckId, Arg.Any<CancellationToken>())
        .Returns(systemDeck);

    var handler = new DeleteDeckCommandHandler(_deckRepository);
    var command = new DeleteDeckCommand(deckId);

    // Act
    var act = () => handler.Handle(command, CancellationToken.None);

    // Assert
    await act.Should().ThrowAsync<InvalidOperationException>()
        .WithMessage("System decks cannot be modified.");
}
```

Repeat this pattern for each command test file, adjusting:
- `AddCardToDeckCommandTests.cs` — create handler with `_deckRepository` and `_cardRepository`, command with `(deckId, Guid.NewGuid(), 1, DeckCategory.MainDeck)`
- `RemoveCardFromDeckCommandTests.cs` — command with `(deckId, Guid.NewGuid(), DeckCategory.MainDeck)`
- `UpdateCardQuantityCommandTests.cs` — create handler with `_deckRepository`, `_cardRepository`, `TimeProvider.System`, command with `(deckId, Guid.NewGuid(), DeckCategory.MainDeck, 2)`
- `MoveCardCategoryCommandTests.cs` — command with `(deckId, Guid.NewGuid(), DeckCategory.MainDeck, DeckCategory.Sideboard)`
- `UpdateDeckFormatCommandTests.cs` — command with `(deckId, Format.Modern)`

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Application.Tests/ --filter SystemDeck`
Expected: All 6 tests FAIL

**Step 3: Add guard to each handler**

In each handler, add immediately after the deck is fetched and null-checked:

```csharp
if (deck.IsSystemDeck)
    throw new InvalidOperationException("System decks cannot be modified.");
```

Files to modify (add after the `KeyNotFoundException` throw):
- `src/MtgDecker.Application/Decks/DeleteDeckCommand.cs` — after line ~29
- `src/MtgDecker.Application/Decks/AddCardToDeckCommand.cs` — after line ~37
- `src/MtgDecker.Application/Decks/RemoveCardFromDeckCommand.cs` — after line ~33
- `src/MtgDecker.Application/Decks/UpdateCardQuantityCommand.cs` — after line ~37
- `src/MtgDecker.Application/Decks/MoveCardCategoryCommand.cs` — after line ~38
- `src/MtgDecker.Application/Decks/UpdateDeckFormatCommand.cs` — after line ~33

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Application.Tests/ --filter SystemDeck`
Expected: All 6 PASS

**Step 5: Run all application tests**

Run: `dotnet test tests/MtgDecker.Application.Tests/`
Expected: All pass

**Step 6: Commit**

```bash
git add src/MtgDecker.Application/Decks/ tests/MtgDecker.Application.Tests/Decks/
git commit -m "feat(app): add system deck mutation guards to all deck commands"
```

---

### Task 6: Application — Add CloneDeckCommand

**Files:**
- Create: `src/MtgDecker.Application/Decks/CloneDeckCommand.cs`
- Create: `tests/MtgDecker.Application.Tests/Decks/CloneDeckCommandTests.cs`

**Step 1: Write failing tests**

Create `tests/MtgDecker.Application.Tests/Decks/CloneDeckCommandTests.cs`:

```csharp
using FluentAssertions;
using FluentValidation.TestHelper;
using MtgDecker.Application.Decks;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;
using NSubstitute;
using Xunit;

namespace MtgDecker.Application.Tests.Decks;

public class CloneDeckCommandTests
{
    private readonly IDeckRepository _deckRepository = Substitute.For<IDeckRepository>();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    [Fact]
    public async Task Handle_ClonesSystemDeckToUser()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        var sourceDeck = new Deck
        {
            Id = sourceId,
            Name = "Legacy Goblins",
            Format = Format.Legacy,
            Description = "Goblin tribal",
            UserId = null,
            Entries = new List<DeckEntry>
            {
                new() { CardId = cardId, Quantity = 4, Category = DeckCategory.MainDeck }
            }
        };

        _deckRepository.GetByIdAsync(sourceId, Arg.Any<CancellationToken>())
            .Returns(sourceDeck);

        var handler = new CloneDeckCommandHandler(_deckRepository, _timeProvider);
        var command = new CloneDeckCommand(sourceId, userId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Name.Should().Be("Legacy Goblins");
        result.Format.Should().Be(Format.Legacy);
        result.Description.Should().Be("Goblin tribal");
        result.UserId.Should().Be(userId);
        result.Entries.Should().HaveCount(1);
        result.Entries[0].CardId.Should().Be(cardId);
        result.Entries[0].Quantity.Should().Be(4);
        result.Entries[0].Category.Should().Be(DeckCategory.MainDeck);

        await _deckRepository.Received(1).AddAsync(Arg.Any<Deck>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SourceNotFound_Throws()
    {
        // Arrange
        _deckRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Deck?)null);

        var handler = new CloneDeckCommandHandler(_deckRepository, _timeProvider);
        var command = new CloneDeckCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public void Validator_EmptySourceDeckId_Fails()
    {
        var validator = new CloneDeckCommandValidator();
        var command = new CloneDeckCommand(Guid.Empty, Guid.NewGuid());
        var result = validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SourceDeckId);
    }

    [Fact]
    public void Validator_EmptyUserId_Fails()
    {
        var validator = new CloneDeckCommandValidator();
        var command = new CloneDeckCommand(Guid.NewGuid(), Guid.Empty);
        var result = validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/MtgDecker.Application.Tests/ --filter CloneDeckCommand`
Expected: FAIL — class does not exist

**Step 3: Implement CloneDeckCommand**

Create `src/MtgDecker.Application/Decks/CloneDeckCommand.cs`:

```csharp
using FluentValidation;
using MediatR;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Decks;

public record CloneDeckCommand(Guid SourceDeckId, Guid UserId) : IRequest<Deck>;

public class CloneDeckCommandValidator : AbstractValidator<CloneDeckCommand>
{
    public CloneDeckCommandValidator()
    {
        RuleFor(x => x.SourceDeckId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public class CloneDeckCommandHandler : IRequestHandler<CloneDeckCommand, Deck>
{
    private readonly IDeckRepository _deckRepository;
    private readonly TimeProvider _timeProvider;

    public CloneDeckCommandHandler(IDeckRepository deckRepository, TimeProvider timeProvider)
    {
        _deckRepository = deckRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Deck> Handle(CloneDeckCommand request, CancellationToken ct)
    {
        var source = await _deckRepository.GetByIdAsync(request.SourceDeckId, ct)
            ?? throw new KeyNotFoundException($"Deck {request.SourceDeckId} not found.");

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var clone = new Deck
        {
            Name = source.Name,
            Format = source.Format,
            Description = source.Description,
            UserId = request.UserId,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            Entries = source.Entries.Select(e => new DeckEntry
            {
                CardId = e.CardId,
                Quantity = e.Quantity,
                Category = e.Category
            }).ToList()
        };

        await _deckRepository.AddAsync(clone, ct);
        return clone;
    }
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/MtgDecker.Application.Tests/ --filter CloneDeckCommand`
Expected: All 4 PASS

**Step 5: Commit**

```bash
git add src/MtgDecker.Application/Decks/CloneDeckCommand.cs \
        tests/MtgDecker.Application.Tests/Decks/CloneDeckCommandTests.cs
git commit -m "feat(app): add CloneDeckCommand for copying system decks to user"
```

---

### Task 7: Application — Update SeedPresetDecksCommand for null UserId

**Files:**
- Modify: `src/MtgDecker.Application/DeckExport/SeedPresetDecksCommand.cs`
- Modify: `src/MtgDecker.Application/DeckExport/ImportDeckCommand.cs` (UserId becomes `Guid?`)
- Modify: `tests/MtgDecker.Application.Tests/DeckExport/SeedPresetDecksCommandTests.cs`
- Modify: `tests/MtgDecker.Application.Tests/DeckExport/ImportDeckCommandTests.cs`

**Step 1: Read current SeedPresetDecksCommand and ImportDeckCommand to understand the flow**

`SeedPresetDecksCommand` takes `Guid UserId`, uses `ListDecksQuery(UserId)` to find existing decks, and passes `UserId` to `ImportDeckCommand`. We need to:
- Change `SeedPresetDecksCommand` to not require `UserId` (system decks have null UserId)
- Change it to use `ListSystemDecksAsync` instead of `ListDecksQuery`
- Change `ImportDeckCommand.UserId` to `Guid?` so it can accept null

**Step 2: Update ImportDeckCommand — make UserId nullable**

In `src/MtgDecker.Application/DeckExport/ImportDeckCommand.cs`:

```csharp
// Before:
public record ImportDeckCommand(string DeckText, string ParserFormat, string DeckName, Format DeckFormat, Guid UserId) : IRequest<ImportDeckResult>;

// After:
public record ImportDeckCommand(string DeckText, string ParserFormat, string DeckName, Format DeckFormat, Guid? UserId) : IRequest<ImportDeckResult>;
```

Also update the validator — `UserId` no longer needs `NotEmpty` since system deck imports have null. Remove the `UserId` validator rule, or change to allow null. Simplest: remove `RuleFor(x => x.UserId).NotEmpty()` if it exists. Check the validator — if there's no explicit UserId rule, no change needed.

**Step 3: Update SeedPresetDecksCommand — remove UserId parameter, use ListSystemDecksAsync**

In `src/MtgDecker.Application/DeckExport/SeedPresetDecksCommand.cs`:

```csharp
// Before:
public record SeedPresetDecksCommand(Guid UserId) : IRequest<SeedPresetDecksResult>;

// After:
public record SeedPresetDecksCommand() : IRequest<SeedPresetDecksResult>;
```

Update handler to inject `IDeckRepository` directly and use `ListSystemDecksAsync` instead of sending `ListDecksQuery`:

```csharp
public class SeedPresetDecksCommandHandler : IRequestHandler<SeedPresetDecksCommand, SeedPresetDecksResult>
{
    private readonly IMediator _mediator;
    private readonly IDeckRepository _deckRepository;

    public SeedPresetDecksCommandHandler(IMediator mediator, IDeckRepository deckRepository)
    {
        _mediator = mediator;
        _deckRepository = deckRepository;
    }

    public async Task<SeedPresetDecksResult> Handle(SeedPresetDecksCommand request, CancellationToken ct)
    {
        var existingDecks = await _deckRepository.ListSystemDecksAsync(ct);
        var existingNames = existingDecks.Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = new List<string>();
        var skipped = new List<string>();
        var unresolved = new Dictionary<string, List<string>>();

        foreach (var preset in PresetDeckRegistry.All)
        {
            if (existingNames.Contains(preset.Name))
            {
                skipped.Add(preset.Name);
                continue;
            }

            var result = await _mediator.Send(
                new ImportDeckCommand(preset.DeckTextMtgo, "MTGO", preset.Name, preset.Format, null), ct);

            created.Add(preset.Name);
            if (result.UnresolvedCards.Count > 0)
                unresolved[preset.Name] = result.UnresolvedCards;
        }

        return new SeedPresetDecksResult(created, skipped, unresolved);
    }
}
```

**Step 4: Update SeedPresetDecksCommand tests**

In `tests/MtgDecker.Application.Tests/DeckExport/SeedPresetDecksCommandTests.cs`, update:
- Constructor: add `IDeckRepository` substitute, pass to handler
- Change all `new SeedPresetDecksCommand(userId)` to `new SeedPresetDecksCommand()`
- Change mock setup from `_mediator.Send(Arg.Any<ListDecksQuery>(), ...)` to `_deckRepository.ListSystemDecksAsync(...)` returning `List<Deck>`
- Change `ImportDeckCommand` argument matching: `UserId` is now `null`

**Step 5: Update ImportDeckCommand tests**

In `tests/MtgDecker.Application.Tests/DeckExport/ImportDeckCommandTests.cs`:
- Tests that pass a `Guid` for `UserId` still work since `Guid?` accepts `Guid`
- Add one test for null UserId import:

```csharp
[Fact]
public async Task Handle_NullUserId_CreatesDeckWithNullUserId()
{
    // Arrange — set up parser and card resolution mocks
    // ... (follow existing test patterns)
    var command = new ImportDeckCommand("4 Lightning Bolt", "MTGO", "System Deck", Format.Legacy, null);

    // Act
    var result = await _handler.Handle(command, CancellationToken.None);

    // Assert
    result.Deck.UserId.Should().BeNull();
}
```

**Step 6: Update Web layer caller**

In the Web layer where `SeedPresetDecksCommand` is called, remove the `UserId` argument. Search for `SeedPresetDecksCommand` usage in `src/MtgDecker.Web/` and update:

```csharp
// Before:
new SeedPresetDecksCommand(UserId)

// After:
new SeedPresetDecksCommand()
```

**Step 7: Run all tests**

Run: `dotnet test tests/MtgDecker.Application.Tests/`
Expected: All pass

**Step 8: Commit**

```bash
git add src/MtgDecker.Application/DeckExport/ tests/MtgDecker.Application.Tests/DeckExport/ \
        src/MtgDecker.Web/
git commit -m "feat(app): update SeedPresetDecksCommand to create system decks with null UserId"
```

---

### Task 8: Web — Split MyDecks into Preset Decks and My Decks sections

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/MyDecks.razor`

**Step 1: Read current MyDecks.razor for full context**

Read `src/MtgDecker.Web/Components/Pages/MyDecks.razor` to understand the current layout.

**Step 2: Add system decks state and loading**

Add a field for system decks:

```csharp
private List<Deck> _systemDecks = new();
```

In `LoadDecks()`, also load system decks:

```csharp
_systemDecks = await Mediator.Send(new ListSystemDecksQuery());
```

**Step 3: Add "Preset Decks" section above "My Decks"**

Add a section above the existing deck cards with:
- `MudText` header: "Preset Decks"
- `MudGrid` with `MudCard` for each system deck
- Each card shows deck name, format, and a **"Clone"** button and a **"View"** button
- No edit/delete buttons
- Clone button sends `CloneDeckCommand(deck.Id, UserId)`, shows snackbar, reloads user decks
- View button navigates to `/decks/{deck.Id}/view`

```razor
@if (_systemDecks.Any())
{
    <MudText Typo="Typo.h5" Class="mb-2">Preset Decks</MudText>
    <MudGrid>
        @foreach (var deck in _systemDecks)
        {
            <MudItem xs="12" sm="6" md="4" lg="3">
                <MudCard Elevation="2" Class="pa-3">
                    <MudCardContent>
                        <MudText Typo="Typo.h6">@deck.Name</MudText>
                        <MudText Typo="Typo.body2" Color="Color.Secondary">@deck.Format</MudText>
                        @if (!string.IsNullOrWhiteSpace(deck.Description))
                        {
                            <MudText Typo="Typo.caption">@deck.Description</MudText>
                        }
                    </MudCardContent>
                    <MudCardActions>
                        <MudButton Variant="Variant.Text" Color="Color.Primary"
                                   OnClick="@(() => ViewDeck(deck.Id))">View</MudButton>
                        <MudButton Variant="Variant.Text" Color="Color.Secondary"
                                   OnClick="@(() => CloneDeck(deck))">Clone</MudButton>
                    </MudCardActions>
                </MudCard>
            </MudItem>
        }
    </MudGrid>
    <MudDivider Class="my-4" />
}
```

**Step 4: Add ViewDeck and CloneDeck methods**

```csharp
private void ViewDeck(Guid deckId)
{
    NavigationManager.NavigateTo($"/decks/{deckId}/view");
}

private async Task CloneDeck(Deck deck)
{
    try
    {
        var clone = await Mediator.Send(new CloneDeckCommand(deck.Id, UserId));
        Snackbar.Add($"Cloned '{deck.Name}' to your decks", Severity.Success);
        await LoadDecks();
    }
    catch (Exception ex)
    {
        Snackbar.Add($"Failed to clone deck: {ex.Message}", Severity.Error);
    }
}
```

**Step 5: Add using for ListSystemDecksQuery and CloneDeckCommand if needed**

The Blazor `_Imports.razor` should cover the namespace, but verify.

**Step 6: Build and verify**

Run: `dotnet build src/MtgDecker.Web/`
Expected: Build succeeds

**Step 7: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/MyDecks.razor
git commit -m "feat(web): split MyDecks into Preset Decks and My Decks sections"
```

---

### Task 9: Web — Add read-only view route and mode for DeckBuilder

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/DeckBuilder.razor`

**Step 1: Add a second route for view mode**

Add a route at the top:

```razor
@page "/decks/{DeckId:guid}/view"
@page "/decks/{DeckId:guid}/edit"
```

**Step 2: Detect view mode from the URL**

Add a property to detect view mode and a read-only flag:

```csharp
private bool _isReadOnly;

protected override async Task OnParametersSetAsync()
{
    var uri = NavigationManager.Uri;
    _isReadOnly = uri.Contains("/view", StringComparison.OrdinalIgnoreCase);
    // ... existing LoadDeck call
}
```

Or simpler — derive from the loaded deck:

```csharp
private bool IsReadOnly => _deck?.IsSystemDeck == true;
```

Use `IsReadOnly` property derived from the deck itself (more robust than URL parsing).

**Step 3: Hide mutation controls when read-only**

Wrap the following in `@if (!IsReadOnly)`:
- Card search bar and add-to-deck buttons
- Quantity +/- buttons on card entries
- Remove card buttons
- Format dropdown (or make it disabled)
- Delete deck button
- Any other mutation affordances

**Step 4: Add "Clone to My Decks" button in toolbar when read-only**

```razor
@if (IsReadOnly)
{
    <MudButton Variant="Variant.Filled" Color="Color.Primary"
               OnClick="@CloneToMyDecks" StartIcon="@Icons.Material.Filled.ContentCopy">
        Clone to My Decks
    </MudButton>
}
```

**Step 5: Add CloneToMyDecks method**

```csharp
private async Task CloneToMyDecks()
{
    try
    {
        var clone = await Mediator.Send(new CloneDeckCommand(DeckId, UserId));
        Snackbar.Add($"Cloned '{_deck!.Name}' to your decks", Severity.Success);
        NavigationManager.NavigateTo($"/decks/{clone.Id}/edit");
    }
    catch (Exception ex)
    {
        Snackbar.Add($"Failed to clone deck: {ex.Message}", Severity.Error);
    }
}
```

**Step 6: Build and verify**

Run: `dotnet build src/MtgDecker.Web/`
Expected: Build succeeds

**Step 7: Commit**

```bash
git add src/MtgDecker.Web/Components/Pages/DeckBuilder.razor
git commit -m "feat(web): add read-only view mode for system decks with clone button"
```

---

### Task 10: Final verification and cleanup

**Step 1: Run all tests**

Run:
```bash
dotnet test tests/MtgDecker.Domain.Tests/
dotnet test tests/MtgDecker.Application.Tests/
dotnet test tests/MtgDecker.Infrastructure.Tests/
dotnet test tests/MtgDecker.Engine.Tests/
```
Expected: All pass

**Step 2: Build the full Web project**

Run: `dotnet build src/MtgDecker.Web/`
Expected: Build succeeds

**Step 3: Verify no leftover hardcoded UserId in SeedPresetDecksCommand calls**

Search for any remaining `SeedPresetDecksCommand(` calls that still pass UserId and fix them.

**Step 4: Final commit if any cleanup needed**

```bash
git add -A
git commit -m "chore: final cleanup for system decks feature"
```
