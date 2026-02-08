# Phase 1: Solution Scaffolding & Domain Layer

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create the full solution structure with all projects, references, and packages, then implement the complete Domain layer with TDD.

**Architecture:** Clean Architecture with strict dependency flow. Domain has zero dependencies. All domain logic is pure C# with full unit test coverage.

**Tech Stack:** .NET 10, xUnit, FluentAssertions

---

## Prerequisites

Install .NET 10 SDK from https://dotnet.microsoft.com/download/dotnet/10.0

Verify with:
```bash
dotnet --version
# Expected: 10.x.x
```

---

### Task 1: Create Solution and Project Structure

**Files:**
- Create: `MtgDecker.sln`
- Create: `src/MtgDecker.Domain/MtgDecker.Domain.csproj`
- Create: `src/MtgDecker.Application/MtgDecker.Application.csproj`
- Create: `src/MtgDecker.Infrastructure/MtgDecker.Infrastructure.csproj`
- Create: `src/MtgDecker.Web/MtgDecker.Web.csproj`
- Create: `tests/MtgDecker.Domain.Tests/MtgDecker.Domain.Tests.csproj`
- Create: `tests/MtgDecker.Application.Tests/MtgDecker.Application.Tests.csproj`
- Create: `tests/MtgDecker.Infrastructure.Tests/MtgDecker.Infrastructure.Tests.csproj`

**Step 1: Create solution and source projects**

```bash
cd C:/Users/larst/MtgDecker

# Solution
dotnet new sln -n MtgDecker

# Source projects
dotnet new classlib -n MtgDecker.Domain -o src/MtgDecker.Domain
dotnet new classlib -n MtgDecker.Application -o src/MtgDecker.Application
dotnet new classlib -n MtgDecker.Infrastructure -o src/MtgDecker.Infrastructure
dotnet new blazor -n MtgDecker.Web -o src/MtgDecker.Web --interactivity Server --empty
```

**Step 2: Create test projects**

```bash
dotnet new xunit -n MtgDecker.Domain.Tests -o tests/MtgDecker.Domain.Tests
dotnet new xunit -n MtgDecker.Application.Tests -o tests/MtgDecker.Application.Tests
dotnet new xunit -n MtgDecker.Infrastructure.Tests -o tests/MtgDecker.Infrastructure.Tests
```

**Step 3: Add all projects to solution**

```bash
dotnet sln add src/MtgDecker.Domain/MtgDecker.Domain.csproj
dotnet sln add src/MtgDecker.Application/MtgDecker.Application.csproj
dotnet sln add src/MtgDecker.Infrastructure/MtgDecker.Infrastructure.csproj
dotnet sln add src/MtgDecker.Web/MtgDecker.Web.csproj
dotnet sln add tests/MtgDecker.Domain.Tests/MtgDecker.Domain.Tests.csproj
dotnet sln add tests/MtgDecker.Application.Tests/MtgDecker.Application.Tests.csproj
dotnet sln add tests/MtgDecker.Infrastructure.Tests/MtgDecker.Infrastructure.Tests.csproj
```

**Step 4: Set up project references (strict Clean Architecture)**

```bash
# Application -> Domain
dotnet add src/MtgDecker.Application/MtgDecker.Application.csproj reference src/MtgDecker.Domain/MtgDecker.Domain.csproj

# Infrastructure -> Application + Domain
dotnet add src/MtgDecker.Infrastructure/MtgDecker.Infrastructure.csproj reference src/MtgDecker.Application/MtgDecker.Application.csproj
dotnet add src/MtgDecker.Infrastructure/MtgDecker.Infrastructure.csproj reference src/MtgDecker.Domain/MtgDecker.Domain.csproj

# Web -> Application + Infrastructure
dotnet add src/MtgDecker.Web/MtgDecker.Web.csproj reference src/MtgDecker.Application/MtgDecker.Application.csproj
dotnet add src/MtgDecker.Web/MtgDecker.Web.csproj reference src/MtgDecker.Infrastructure/MtgDecker.Infrastructure.csproj

# Test projects -> their targets
dotnet add tests/MtgDecker.Domain.Tests/MtgDecker.Domain.Tests.csproj reference src/MtgDecker.Domain/MtgDecker.Domain.csproj
dotnet add tests/MtgDecker.Application.Tests/MtgDecker.Application.Tests.csproj reference src/MtgDecker.Application/MtgDecker.Application.csproj
dotnet add tests/MtgDecker.Application.Tests/MtgDecker.Application.Tests.csproj reference src/MtgDecker.Domain/MtgDecker.Domain.csproj
dotnet add tests/MtgDecker.Infrastructure.Tests/MtgDecker.Infrastructure.Tests.csproj reference src/MtgDecker.Infrastructure/MtgDecker.Infrastructure.csproj
dotnet add tests/MtgDecker.Infrastructure.Tests/MtgDecker.Infrastructure.Tests.csproj reference src/MtgDecker.Domain/MtgDecker.Domain.csproj
```

**Step 5: Add NuGet packages**

```bash
# Application layer
dotnet add src/MtgDecker.Application/MtgDecker.Application.csproj package MediatR
dotnet add src/MtgDecker.Application/MtgDecker.Application.csproj package FluentValidation

# Infrastructure layer
dotnet add src/MtgDecker.Infrastructure/MtgDecker.Infrastructure.csproj package Microsoft.EntityFrameworkCore
dotnet add src/MtgDecker.Infrastructure/MtgDecker.Infrastructure.csproj package Microsoft.EntityFrameworkCore.SqlServer
dotnet add src/MtgDecker.Infrastructure/MtgDecker.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Design

# Web layer
dotnet add src/MtgDecker.Web/MtgDecker.Web.csproj package MudBlazor

# Test projects
dotnet add tests/MtgDecker.Domain.Tests/MtgDecker.Domain.Tests.csproj package FluentAssertions
dotnet add tests/MtgDecker.Application.Tests/MtgDecker.Application.Tests.csproj package FluentAssertions
dotnet add tests/MtgDecker.Application.Tests/MtgDecker.Application.Tests.csproj package NSubstitute
dotnet add tests/MtgDecker.Infrastructure.Tests/MtgDecker.Infrastructure.Tests.csproj package FluentAssertions
dotnet add tests/MtgDecker.Infrastructure.Tests/MtgDecker.Infrastructure.Tests.csproj package Microsoft.EntityFrameworkCore.InMemory
```

**Step 6: Clean up template files and verify build**

Delete the auto-generated `Class1.cs` from Domain, Application, Infrastructure and `UnitTest1.cs` from test projects.

```bash
dotnet build
# Expected: Build succeeded with 0 errors
```

**Step 7: Add .gitignore and commit**

Create a standard .NET `.gitignore` (bin, obj, .vs, *.user, etc.).

```bash
dotnet new gitignore
git add -A
git commit -m "feat: scaffold solution with Clean Architecture project structure

Create MtgDecker.sln with Domain, Application, Infrastructure, Web projects
and corresponding test projects. Set up project references and NuGet packages."
```

---

### Task 2: Domain Enums and Format Rules

**Files:**
- Create: `src/MtgDecker.Domain/Enums/Format.cs`
- Create: `src/MtgDecker.Domain/Enums/DeckCategory.cs`
- Create: `src/MtgDecker.Domain/Enums/CardCondition.cs`
- Create: `src/MtgDecker.Domain/Enums/LegalityStatus.cs`
- Create: `src/MtgDecker.Domain/Rules/FormatRules.cs`
- Create: `tests/MtgDecker.Domain.Tests/Rules/FormatRulesTests.cs`

**Step 1: Write the failing tests for FormatRules**

```csharp
// tests/MtgDecker.Domain.Tests/Rules/FormatRulesTests.cs
using FluentAssertions;
using MtgDecker.Domain.Enums;
using MtgDecker.Domain.Rules;

namespace MtgDecker.Domain.Tests.Rules;

public class FormatRulesTests
{
    [Theory]
    [InlineData(Format.Vintage, 60)]
    [InlineData(Format.Legacy, 60)]
    [InlineData(Format.Premodern, 60)]
    [InlineData(Format.Modern, 60)]
    [InlineData(Format.Pauper, 60)]
    [InlineData(Format.Commander, 100)]
    public void GetMinDeckSize_ReturnsCorrectSize(Format format, int expected)
    {
        FormatRules.GetMinDeckSize(format).Should().Be(expected);
    }

    [Theory]
    [InlineData(Format.Vintage, null)]
    [InlineData(Format.Legacy, null)]
    [InlineData(Format.Premodern, null)]
    [InlineData(Format.Modern, null)]
    [InlineData(Format.Pauper, null)]
    [InlineData(Format.Commander, 100)]
    public void GetMaxDeckSize_ReturnsCorrectSize(Format format, int? expected)
    {
        FormatRules.GetMaxDeckSize(format).Should().Be(expected);
    }

    [Theory]
    [InlineData(Format.Vintage, 4)]
    [InlineData(Format.Legacy, 4)]
    [InlineData(Format.Premodern, 4)]
    [InlineData(Format.Modern, 4)]
    [InlineData(Format.Pauper, 4)]
    [InlineData(Format.Commander, 1)]
    public void GetMaxCopies_ReturnsCorrectLimit(Format format, int expected)
    {
        FormatRules.GetMaxCopies(format).Should().Be(expected);
    }

    [Theory]
    [InlineData(Format.Vintage, true)]
    [InlineData(Format.Legacy, true)]
    [InlineData(Format.Premodern, true)]
    [InlineData(Format.Modern, true)]
    [InlineData(Format.Pauper, true)]
    [InlineData(Format.Commander, false)]
    public void HasSideboard_ReturnsCorrectValue(Format format, bool expected)
    {
        FormatRules.HasSideboard(format).Should().Be(expected);
    }

    [Theory]
    [InlineData(Format.Vintage, 15)]
    [InlineData(Format.Legacy, 15)]
    [InlineData(Format.Modern, 15)]
    [InlineData(Format.Pauper, 15)]
    [InlineData(Format.Premodern, 15)]
    public void GetMaxSideboardSize_ReturnsCorrectSize(Format format, int expected)
    {
        FormatRules.GetMaxSideboardSize(format).Should().Be(expected);
    }

    [Fact]
    public void GetMaxSideboardSize_Commander_ReturnsZero()
    {
        FormatRules.GetMaxSideboardSize(Format.Commander).Should().Be(0);
    }

    [Theory]
    [InlineData(Format.Vintage, "vintage")]
    [InlineData(Format.Legacy, "legacy")]
    [InlineData(Format.Premodern, "premodern")]
    [InlineData(Format.Modern, "modern")]
    [InlineData(Format.Pauper, "pauper")]
    [InlineData(Format.Commander, "commander")]
    public void GetScryfallName_ReturnsCorrectApiName(Format format, string expected)
    {
        FormatRules.GetScryfallName(format).Should().Be(expected);
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/MtgDecker.Domain.Tests --filter "FormatRulesTests" -v minimal
# Expected: FAIL - types/classes not found
```

**Step 3: Implement the enums and FormatRules**

```csharp
// src/MtgDecker.Domain/Enums/Format.cs
namespace MtgDecker.Domain.Enums;

public enum Format
{
    Vintage,
    Legacy,
    Premodern,
    Modern,
    Pauper,
    Commander
}
```

```csharp
// src/MtgDecker.Domain/Enums/DeckCategory.cs
namespace MtgDecker.Domain.Enums;

public enum DeckCategory
{
    MainDeck,
    Sideboard
}
```

```csharp
// src/MtgDecker.Domain/Enums/CardCondition.cs
namespace MtgDecker.Domain.Enums;

public enum CardCondition
{
    Mint,
    NearMint,
    LightlyPlayed,
    Played,
    Damaged
}
```

```csharp
// src/MtgDecker.Domain/Enums/LegalityStatus.cs
namespace MtgDecker.Domain.Enums;

public enum LegalityStatus
{
    Legal,
    Banned,
    Restricted,
    NotLegal
}
```

```csharp
// src/MtgDecker.Domain/Rules/FormatRules.cs
using MtgDecker.Domain.Enums;

namespace MtgDecker.Domain.Rules;

public static class FormatRules
{
    public static int GetMinDeckSize(Format format) => format switch
    {
        Format.Commander => 100,
        _ => 60
    };

    public static int? GetMaxDeckSize(Format format) => format switch
    {
        Format.Commander => 100,
        _ => null
    };

    public static int GetMaxCopies(Format format) => format switch
    {
        Format.Commander => 1,
        _ => 4
    };

    public static bool HasSideboard(Format format) => format switch
    {
        Format.Commander => false,
        _ => true
    };

    public static int GetMaxSideboardSize(Format format) => format switch
    {
        Format.Commander => 0,
        _ => 15
    };

    public static string GetScryfallName(Format format) => format switch
    {
        Format.Vintage => "vintage",
        Format.Legacy => "legacy",
        Format.Premodern => "premodern",
        Format.Modern => "modern",
        Format.Pauper => "pauper",
        Format.Commander => "commander",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/MtgDecker.Domain.Tests --filter "FormatRulesTests" -v minimal
# Expected: All tests PASS
```

**Step 5: Commit**

```bash
git add src/MtgDecker.Domain/Enums/ src/MtgDecker.Domain/Rules/ tests/MtgDecker.Domain.Tests/Rules/
git commit -m "feat: add domain enums and format rules with tests"
```

---

### Task 3: Card and CardFace Entities

**Files:**
- Create: `src/MtgDecker.Domain/Entities/Card.cs`
- Create: `src/MtgDecker.Domain/Entities/CardFace.cs`
- Create: `src/MtgDecker.Domain/ValueObjects/CardLegality.cs`
- Create: `tests/MtgDecker.Domain.Tests/Entities/CardTests.cs`

**Step 1: Write the failing tests**

```csharp
// tests/MtgDecker.Domain.Tests/Entities/CardTests.cs
using FluentAssertions;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;
using MtgDecker.Domain.ValueObjects;

namespace MtgDecker.Domain.Tests.Entities;

public class CardTests
{
    [Fact]
    public void IsBasicLand_WithBasicLandTypeLine_ReturnsTrue()
    {
        var card = CreateCard(typeLine: "Basic Land — Mountain");
        card.IsBasicLand.Should().BeTrue();
    }

    [Fact]
    public void IsBasicLand_WithNonBasicLand_ReturnsFalse()
    {
        var card = CreateCard(typeLine: "Land");
        card.IsBasicLand.Should().BeFalse();
    }

    [Fact]
    public void IsBasicLand_WithSnowBasicLand_ReturnsTrue()
    {
        var card = CreateCard(typeLine: "Basic Snow Land — Island");
        card.IsBasicLand.Should().BeTrue();
    }

    [Fact]
    public void IsLegalIn_WhenLegal_ReturnsTrue()
    {
        var card = CreateCard();
        card.Legalities.Add(new CardLegality("modern", LegalityStatus.Legal));

        card.IsLegalIn(Format.Modern).Should().BeTrue();
    }

    [Fact]
    public void IsLegalIn_WhenBanned_ReturnsFalse()
    {
        var card = CreateCard();
        card.Legalities.Add(new CardLegality("modern", LegalityStatus.Banned));

        card.IsLegalIn(Format.Modern).Should().BeFalse();
    }

    [Fact]
    public void IsLegalIn_WhenRestricted_ReturnsTrue()
    {
        var card = CreateCard();
        card.Legalities.Add(new CardLegality("vintage", LegalityStatus.Restricted));

        card.IsLegalIn(Format.Vintage).Should().BeTrue();
    }

    [Fact]
    public void IsLegalIn_WhenNotInList_ReturnsFalse()
    {
        var card = CreateCard();

        card.IsLegalIn(Format.Modern).Should().BeFalse();
    }

    [Fact]
    public void IsRestrictedIn_WhenRestricted_ReturnsTrue()
    {
        var card = CreateCard();
        card.Legalities.Add(new CardLegality("vintage", LegalityStatus.Restricted));

        card.IsRestrictedIn(Format.Vintage).Should().BeTrue();
    }

    [Fact]
    public void HasMultipleFaces_WithFaces_ReturnsTrue()
    {
        var card = CreateCard();
        card.Faces.Add(new CardFace { Name = "Front" });
        card.Faces.Add(new CardFace { Name = "Back" });

        card.HasMultipleFaces.Should().BeTrue();
    }

    [Fact]
    public void HasMultipleFaces_WithNoFaces_ReturnsFalse()
    {
        var card = CreateCard();

        card.HasMultipleFaces.Should().BeFalse();
    }

    private static Card CreateCard(
        string name = "Test Card",
        string typeLine = "Creature — Human",
        string? oracleId = null)
    {
        return new Card
        {
            Id = Guid.NewGuid(),
            ScryfallId = Guid.NewGuid().ToString(),
            OracleId = oracleId ?? Guid.NewGuid().ToString(),
            Name = name,
            TypeLine = typeLine,
            Rarity = "common",
            SetCode = "tst",
            SetName = "Test Set"
        };
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/MtgDecker.Domain.Tests --filter "CardTests" -v minimal
# Expected: FAIL - types not found
```

**Step 3: Implement Card, CardFace, and CardLegality**

```csharp
// src/MtgDecker.Domain/ValueObjects/CardLegality.cs
using MtgDecker.Domain.Enums;

namespace MtgDecker.Domain.ValueObjects;

public class CardLegality
{
    public string FormatName { get; private set; }
    public LegalityStatus Status { get; private set; }

    public CardLegality(string formatName, LegalityStatus status)
    {
        FormatName = formatName;
        Status = status;
    }

    // EF Core needs a parameterless constructor
    private CardLegality() { FormatName = string.Empty; }
}
```

```csharp
// src/MtgDecker.Domain/Entities/CardFace.cs
namespace MtgDecker.Domain.Entities;

public class CardFace
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ManaCost { get; set; }
    public string? TypeLine { get; set; }
    public string? OracleText { get; set; }
    public string? ImageUri { get; set; }
    public string? Power { get; set; }
    public string? Toughness { get; set; }
}
```

```csharp
// src/MtgDecker.Domain/Entities/Card.cs
using MtgDecker.Domain.Enums;
using MtgDecker.Domain.Rules;
using MtgDecker.Domain.ValueObjects;

namespace MtgDecker.Domain.Entities;

public class Card
{
    public Guid Id { get; set; }
    public string ScryfallId { get; set; } = string.Empty;
    public string OracleId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ManaCost { get; set; }
    public double Cmc { get; set; }
    public string TypeLine { get; set; } = string.Empty;
    public string? OracleText { get; set; }
    public string Colors { get; set; } = string.Empty;
    public string ColorIdentity { get; set; } = string.Empty;
    public string Rarity { get; set; } = string.Empty;
    public string SetCode { get; set; } = string.Empty;
    public string SetName { get; set; } = string.Empty;
    public string? CollectorNumber { get; set; }
    public string? ImageUri { get; set; }
    public string? ImageUriSmall { get; set; }
    public string? ImageUriArtCrop { get; set; }
    public string? Layout { get; set; }

    public List<CardFace> Faces { get; set; } = new();
    public List<CardLegality> Legalities { get; set; } = new();

    public bool IsBasicLand => TypeLine.Contains("Basic", StringComparison.OrdinalIgnoreCase)
                               && TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase);

    public bool HasMultipleFaces => Faces.Count > 1;

    public bool IsLegalIn(Format format)
    {
        var scryfallName = FormatRules.GetScryfallName(format);
        var legality = Legalities.FirstOrDefault(l => l.FormatName == scryfallName);
        return legality?.Status is LegalityStatus.Legal or LegalityStatus.Restricted;
    }

    public bool IsRestrictedIn(Format format)
    {
        var scryfallName = FormatRules.GetScryfallName(format);
        var legality = Legalities.FirstOrDefault(l => l.FormatName == scryfallName);
        return legality?.Status == LegalityStatus.Restricted;
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/MtgDecker.Domain.Tests --filter "CardTests" -v minimal
# Expected: All tests PASS
```

**Step 5: Commit**

```bash
git add src/MtgDecker.Domain/Entities/ src/MtgDecker.Domain/ValueObjects/ tests/MtgDecker.Domain.Tests/Entities/
git commit -m "feat: add Card, CardFace entities and CardLegality value object with tests"
```

---

### Task 4: Deck and DeckEntry Entities with Validation

**Files:**
- Create: `src/MtgDecker.Domain/Entities/Deck.cs`
- Create: `src/MtgDecker.Domain/Entities/DeckEntry.cs`
- Create: `tests/MtgDecker.Domain.Tests/Entities/DeckTests.cs`

**Step 1: Write the failing tests**

```csharp
// tests/MtgDecker.Domain.Tests/Entities/DeckTests.cs
using FluentAssertions;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Domain.Tests.Entities;

public class DeckTests
{
    [Fact]
    public void AddCard_ValidCard_AddsEntry()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");

        deck.AddCard(card, 4, DeckCategory.MainDeck);

        deck.Entries.Should().HaveCount(1);
        deck.Entries[0].Quantity.Should().Be(4);
    }

    [Fact]
    public void AddCard_ExceedsMaxCopies_ThrowsException()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");

        var act = () => deck.AddCard(card, 5, DeckCategory.MainDeck);

        act.Should().Throw<DomainException>()
            .WithMessage("*cannot exceed 4 copies*");
    }

    [Fact]
    public void AddCard_BasicLandExceedsMaxCopies_Succeeds()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Mountain", typeLine: "Basic Land — Mountain");

        deck.AddCard(card, 20, DeckCategory.MainDeck);

        deck.Entries.Should().HaveCount(1);
        deck.Entries[0].Quantity.Should().Be(20);
    }

    [Fact]
    public void AddCard_ToSideboardInFormatWithNoSideboard_ThrowsException()
    {
        var deck = CreateDeck(Format.Commander);
        var card = CreateCard("Sol Ring");

        var act = () => deck.AddCard(card, 1, DeckCategory.Sideboard);

        act.Should().Throw<DomainException>()
            .WithMessage("*does not allow a sideboard*");
    }

    [Fact]
    public void AddCard_DuplicateCard_ThrowsException()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");
        deck.AddCard(card, 2, DeckCategory.MainDeck);

        var act = () => deck.AddCard(card, 2, DeckCategory.MainDeck);

        act.Should().Throw<DomainException>()
            .WithMessage("*already in the deck*");
    }

    [Fact]
    public void UpdateCardQuantity_ValidQuantity_UpdatesEntry()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");
        deck.AddCard(card, 2, DeckCategory.MainDeck);

        deck.UpdateCardQuantity(card.Id, 4);

        deck.Entries[0].Quantity.Should().Be(4);
    }

    [Fact]
    public void RemoveCard_ExistingCard_RemovesEntry()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");
        deck.AddCard(card, 4, DeckCategory.MainDeck);

        deck.RemoveCard(card.Id);

        deck.Entries.Should().BeEmpty();
    }

    [Fact]
    public void TotalCardCount_ReturnsSumOfAllEntries()
    {
        var deck = CreateDeck(Format.Modern);
        deck.AddCard(CreateCard("Card A"), 4, DeckCategory.MainDeck);
        deck.AddCard(CreateCard("Card B"), 3, DeckCategory.MainDeck);
        deck.AddCard(CreateCard("Card C"), 2, DeckCategory.Sideboard);

        deck.TotalMainDeckCount.Should().Be(7);
        deck.TotalSideboardCount.Should().Be(2);
    }

    [Fact]
    public void AddCard_ZeroQuantity_ThrowsException()
    {
        var deck = CreateDeck(Format.Modern);
        var card = CreateCard("Lightning Bolt");

        var act = () => deck.AddCard(card, 0, DeckCategory.MainDeck);

        act.Should().Throw<DomainException>()
            .WithMessage("*must be at least 1*");
    }

    private static Deck CreateDeck(Format format)
    {
        return new Deck
        {
            Id = Guid.NewGuid(),
            Name = "Test Deck",
            Format = format,
            UserId = Guid.NewGuid()
        };
    }

    private static Card CreateCard(string name, string typeLine = "Instant")
    {
        return new Card
        {
            Id = Guid.NewGuid(),
            ScryfallId = Guid.NewGuid().ToString(),
            OracleId = Guid.NewGuid().ToString(),
            Name = name,
            TypeLine = typeLine,
            Rarity = "common",
            SetCode = "tst",
            SetName = "Test Set"
        };
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/MtgDecker.Domain.Tests --filter "DeckTests" -v minimal
# Expected: FAIL - types not found
```

**Step 3: Implement DomainException, DeckEntry, and Deck**

```csharp
// src/MtgDecker.Domain/Exceptions/DomainException.cs
namespace MtgDecker.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
```

```csharp
// src/MtgDecker.Domain/Entities/DeckEntry.cs
using MtgDecker.Domain.Enums;

namespace MtgDecker.Domain.Entities;

public class DeckEntry
{
    public Guid Id { get; set; }
    public Guid DeckId { get; set; }
    public Guid CardId { get; set; }
    public int Quantity { get; set; }
    public DeckCategory Category { get; set; }
}
```

```csharp
// src/MtgDecker.Domain/Entities/Deck.cs
using MtgDecker.Domain.Enums;
using MtgDecker.Domain.Exceptions;
using MtgDecker.Domain.Rules;

namespace MtgDecker.Domain.Entities;

public class Deck
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Format Format { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid UserId { get; set; }

    public List<DeckEntry> Entries { get; set; } = new();

    public int TotalMainDeckCount => Entries
        .Where(e => e.Category == DeckCategory.MainDeck)
        .Sum(e => e.Quantity);

    public int TotalSideboardCount => Entries
        .Where(e => e.Category == DeckCategory.Sideboard)
        .Sum(e => e.Quantity);

    public void AddCard(Card card, int quantity, DeckCategory category)
    {
        if (quantity < 1)
            throw new DomainException("Quantity must be at least 1.");

        if (category == DeckCategory.Sideboard && !FormatRules.HasSideboard(Format))
            throw new DomainException($"{Format} does not allow a sideboard.");

        if (Entries.Any(e => e.CardId == card.Id))
            throw new DomainException($"{card.Name} is already in the deck.");

        if (!card.IsBasicLand && quantity > FormatRules.GetMaxCopies(Format))
            throw new DomainException(
                $"A deck cannot exceed {FormatRules.GetMaxCopies(Format)} copies of {card.Name}.");

        Entries.Add(new DeckEntry
        {
            Id = Guid.NewGuid(),
            DeckId = Id,
            CardId = card.Id,
            Quantity = quantity,
            Category = category
        });

        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateCardQuantity(Guid cardId, int quantity)
    {
        var entry = Entries.FirstOrDefault(e => e.CardId == cardId)
            ?? throw new DomainException("Card not found in deck.");

        if (quantity < 1)
            throw new DomainException("Quantity must be at least 1.");

        entry.Quantity = quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RemoveCard(Guid cardId)
    {
        var entry = Entries.FirstOrDefault(e => e.CardId == cardId)
            ?? throw new DomainException("Card not found in deck.");

        Entries.Remove(entry);
        UpdatedAt = DateTime.UtcNow;
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/MtgDecker.Domain.Tests --filter "DeckTests" -v minimal
# Expected: All tests PASS
```

**Step 5: Commit**

```bash
git add src/MtgDecker.Domain/Entities/Deck.cs src/MtgDecker.Domain/Entities/DeckEntry.cs src/MtgDecker.Domain/Exceptions/ tests/MtgDecker.Domain.Tests/Entities/DeckTests.cs
git commit -m "feat: add Deck and DeckEntry entities with validation rules and tests"
```

---

### Task 5: CollectionEntry and Shortage Calculation

**Files:**
- Create: `src/MtgDecker.Domain/Entities/CollectionEntry.cs`
- Create: `src/MtgDecker.Domain/Services/ShortageCalculator.cs`
- Create: `tests/MtgDecker.Domain.Tests/Services/ShortageCalculatorTests.cs`

**Step 1: Write the failing tests**

```csharp
// tests/MtgDecker.Domain.Tests/Services/ShortageCalculatorTests.cs
using FluentAssertions;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;
using MtgDecker.Domain.Services;

namespace MtgDecker.Domain.Tests.Services;

public class ShortageCalculatorTests
{
    [Fact]
    public void Calculate_CardNotOwned_ReturnsFullQuantityAsShortage()
    {
        var oracleId = Guid.NewGuid().ToString();
        var card = CreateCard("Lightning Bolt", oracleId);
        var deck = CreateDeckWithEntry(card, quantity: 4);
        var collection = new List<CollectionEntry>();

        var shortages = ShortageCalculator.Calculate(deck, collection, cardLookup: new[] { card });

        shortages.Should().HaveCount(1);
        shortages[0].CardName.Should().Be("Lightning Bolt");
        shortages[0].Needed.Should().Be(4);
        shortages[0].Owned.Should().Be(0);
        shortages[0].Shortage.Should().Be(4);
    }

    [Fact]
    public void Calculate_CardPartiallyOwned_ReturnsShortage()
    {
        var oracleId = Guid.NewGuid().ToString();
        var card = CreateCard("Lightning Bolt", oracleId);
        var deck = CreateDeckWithEntry(card, quantity: 4);
        var collection = new List<CollectionEntry>
        {
            new() { CardId = card.Id, Quantity = 2, UserId = deck.UserId }
        };

        var shortages = ShortageCalculator.Calculate(deck, collection, cardLookup: new[] { card });

        shortages.Should().HaveCount(1);
        shortages[0].Shortage.Should().Be(2);
    }

    [Fact]
    public void Calculate_CardFullyOwned_ReturnsNoShortage()
    {
        var oracleId = Guid.NewGuid().ToString();
        var card = CreateCard("Lightning Bolt", oracleId);
        var deck = CreateDeckWithEntry(card, quantity: 4);
        var collection = new List<CollectionEntry>
        {
            new() { CardId = card.Id, Quantity = 4, UserId = deck.UserId }
        };

        var shortages = ShortageCalculator.Calculate(deck, collection, cardLookup: new[] { card });

        shortages.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_DifferentPrintingSameOracle_CountsAsOwned()
    {
        var oracleId = Guid.NewGuid().ToString();
        var deckCard = CreateCard("Lightning Bolt", oracleId);
        var ownedCard = CreateCard("Lightning Bolt", oracleId); // different printing, same oracle
        var deck = CreateDeckWithEntry(deckCard, quantity: 4);
        var collection = new List<CollectionEntry>
        {
            new() { CardId = ownedCard.Id, Quantity = 3, UserId = deck.UserId }
        };

        var shortages = ShortageCalculator.Calculate(
            deck, collection, cardLookup: new[] { deckCard, ownedCard });

        shortages.Should().HaveCount(1);
        shortages[0].Shortage.Should().Be(1);
    }

    private static Card CreateCard(string name, string oracleId)
    {
        return new Card
        {
            Id = Guid.NewGuid(),
            ScryfallId = Guid.NewGuid().ToString(),
            OracleId = oracleId,
            Name = name,
            TypeLine = "Instant",
            Rarity = "common",
            SetCode = "tst",
            SetName = "Test Set"
        };
    }

    private static Deck CreateDeckWithEntry(Card card, int quantity)
    {
        var deck = new Deck
        {
            Id = Guid.NewGuid(),
            Name = "Test Deck",
            Format = Format.Modern,
            UserId = Guid.NewGuid()
        };
        deck.AddCard(card, quantity, DeckCategory.MainDeck);
        return deck;
    }
}
```

**Step 2: Run tests to verify they fail**

```bash
dotnet test tests/MtgDecker.Domain.Tests --filter "ShortageCalculatorTests" -v minimal
# Expected: FAIL - types not found
```

**Step 3: Implement CollectionEntry and ShortageCalculator**

```csharp
// src/MtgDecker.Domain/Entities/CollectionEntry.cs
using MtgDecker.Domain.Enums;

namespace MtgDecker.Domain.Entities;

public class CollectionEntry
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CardId { get; set; }
    public int Quantity { get; set; }
    public bool IsFoil { get; set; }
    public CardCondition Condition { get; set; } = CardCondition.NearMint;
}
```

```csharp
// src/MtgDecker.Domain/Services/ShortageCalculator.cs
using MtgDecker.Domain.Entities;

namespace MtgDecker.Domain.Services;

public static class ShortageCalculator
{
    public static List<CardShortage> Calculate(
        Deck deck,
        IEnumerable<CollectionEntry> collection,
        IEnumerable<Card> cardLookup)
    {
        var cardsByid = cardLookup.ToDictionary(c => c.Id);

        // Group collection entries by OracleId to count across printings
        var ownedByOracleId = collection
            .Where(ce => cardsByid.ContainsKey(ce.CardId))
            .GroupBy(ce => cardsByid[ce.CardId].OracleId)
            .ToDictionary(g => g.Key, g => g.Sum(ce => ce.Quantity));

        var shortages = new List<CardShortage>();

        foreach (var entry in deck.Entries)
        {
            if (!cardsByid.TryGetValue(entry.CardId, out var card))
                continue;

            var owned = ownedByOracleId.GetValueOrDefault(card.OracleId, 0);
            var shortage = entry.Quantity - owned;

            if (shortage > 0)
            {
                shortages.Add(new CardShortage(card.Name, entry.Quantity, owned, shortage));
            }
        }

        return shortages;
    }
}

public record CardShortage(string CardName, int Needed, int Owned, int Shortage);
```

**Step 4: Run tests to verify they pass**

```bash
dotnet test tests/MtgDecker.Domain.Tests --filter "ShortageCalculatorTests" -v minimal
# Expected: All tests PASS
```

**Step 5: Commit**

```bash
git add src/MtgDecker.Domain/Entities/CollectionEntry.cs src/MtgDecker.Domain/Services/ tests/MtgDecker.Domain.Tests/Services/
git commit -m "feat: add CollectionEntry and ShortageCalculator with tests"
```

---

### Task 6: User and BulkDataImportMetadata Entities

**Files:**
- Create: `src/MtgDecker.Domain/Entities/User.cs`
- Create: `src/MtgDecker.Domain/Entities/BulkDataImportMetadata.cs`

**Step 1: Implement entities**

These are simple data holders with no domain logic to TDD.

```csharp
// src/MtgDecker.Domain/Entities/User.cs
namespace MtgDecker.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
```

```csharp
// src/MtgDecker.Domain/Entities/BulkDataImportMetadata.cs
namespace MtgDecker.Domain.Entities;

public class BulkDataImportMetadata
{
    public Guid Id { get; set; }
    public DateTime ImportedAt { get; set; }
    public string ScryfallDataType { get; set; } = string.Empty;
    public int CardCount { get; set; }
}
```

**Step 2: Verify build**

```bash
dotnet build
# Expected: Build succeeded
```

**Step 3: Commit**

```bash
git add src/MtgDecker.Domain/Entities/User.cs src/MtgDecker.Domain/Entities/BulkDataImportMetadata.cs
git commit -m "feat: add User and BulkDataImportMetadata entities"
```

---

### Task 7: Application Layer Interfaces

**Files:**
- Create: `src/MtgDecker.Application/Interfaces/ICardRepository.cs`
- Create: `src/MtgDecker.Application/Interfaces/IDeckRepository.cs`
- Create: `src/MtgDecker.Application/Interfaces/ICollectionRepository.cs`
- Create: `src/MtgDecker.Application/Interfaces/IScryfallClient.cs`
- Create: `src/MtgDecker.Application/Interfaces/IBulkDataImporter.cs`

**Step 1: Implement repository interfaces**

```csharp
// src/MtgDecker.Application/Interfaces/ICardRepository.cs
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Interfaces;

public interface ICardRepository
{
    Task<Card?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Card?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<(List<Card> Cards, int TotalCount)> SearchAsync(CardSearchFilter filter, CancellationToken ct = default);
    Task<List<Card>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<List<Card>> GetByOracleIdAsync(string oracleId, CancellationToken ct = default);
    Task UpsertBatchAsync(IEnumerable<Card> cards, CancellationToken ct = default);
}

public class CardSearchFilter
{
    public string? SearchText { get; set; }
    public string? Format { get; set; }
    public List<string>? Colors { get; set; }
    public string? Type { get; set; }
    public double? MinCmc { get; set; }
    public double? MaxCmc { get; set; }
    public string? Rarity { get; set; }
    public string? SetCode { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
```

```csharp
// src/MtgDecker.Application/Interfaces/IDeckRepository.cs
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Interfaces;

public interface IDeckRepository
{
    Task<Deck?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Deck>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Deck deck, CancellationToken ct = default);
    Task UpdateAsync(Deck deck, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

```csharp
// src/MtgDecker.Application/Interfaces/ICollectionRepository.cs
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Interfaces;

public interface ICollectionRepository
{
    Task<List<CollectionEntry>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<CollectionEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(CollectionEntry entry, CancellationToken ct = default);
    Task UpdateAsync(CollectionEntry entry, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<List<CollectionEntry>> SearchAsync(Guid userId, string? searchText, CancellationToken ct = default);
}
```

```csharp
// src/MtgDecker.Application/Interfaces/IScryfallClient.cs
namespace MtgDecker.Application.Interfaces;

public interface IScryfallClient
{
    Task<BulkDataInfo?> GetBulkDataInfoAsync(string dataType = "default_cards", CancellationToken ct = default);
    Task<Stream> DownloadBulkDataAsync(string downloadUri, CancellationToken ct = default);
}

public class BulkDataInfo
{
    public string DownloadUri { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public long Size { get; set; }
}
```

```csharp
// src/MtgDecker.Application/Interfaces/IBulkDataImporter.cs
namespace MtgDecker.Application.Interfaces;

public interface IBulkDataImporter
{
    Task<int> ImportFromStreamAsync(Stream jsonStream, IProgress<int>? progress = null, CancellationToken ct = default);
}
```

**Step 2: Verify build**

```bash
dotnet build
# Expected: Build succeeded
```

**Step 3: Commit**

```bash
git add src/MtgDecker.Application/Interfaces/
git commit -m "feat: add application layer repository and service interfaces"
```

---

### Task 8: Run Full Test Suite and Final Verification

**Step 1: Run all tests**

```bash
dotnet test --verbosity minimal
# Expected: All tests pass
```

**Step 2: Verify solution structure**

```bash
dotnet build
# Expected: Build succeeded, 0 warnings ideally
```

**Step 3: Final commit if any cleanup was needed**

```bash
git status
# If clean: Phase 1 complete!
```

---

## Phase 1 Complete

After this phase, you will have:
- Full solution with 7 projects and correct dependency flow
- All domain entities: Card, CardFace, Deck, DeckEntry, CollectionEntry, User, BulkDataImportMetadata
- All domain enums: Format, DeckCategory, CardCondition, LegalityStatus
- FormatRules with full test coverage
- Deck validation logic (copy limits, basic land exception, sideboard rules) with tests
- ShortageCalculator with tests
- CardLegality value object
- Application layer interfaces ready for Infrastructure implementation
- Clean git history with atomic commits
