# Mana System Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a mana system to the game engine — mana pool, land tapping for mana, casting costs, mana payment flow, and hardcoded card definitions for the two starter decks (Legacy Goblins + Legacy Enchantress).

**Architecture:** New types in `Engine/Enums/` and `Engine/Mana/` namespaces. Static card registry maps card names to game properties. Player gets a ManaPool and land-drop counter. GameEngine's ExecuteAction becomes async to support mana choice prompts. Cards not in the registry continue working in sandbox mode (no mana required).

**Tech Stack:** .NET 10, C# 14, xUnit + FluentAssertions, existing Engine project structure.

---

### Task 1: ManaColor Enum

**Files:**
- Create: `src/MtgDecker.Engine/Enums/ManaColor.cs`

**Step 1:** Create the ManaColor enum file.

```csharp
// src/MtgDecker.Engine/Enums/ManaColor.cs
namespace MtgDecker.Engine.Enums;

public enum ManaColor
{
    White,
    Blue,
    Black,
    Red,
    Green,
    Colorless
}
```

**Step 2:** Verify it compiles.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet build src/MtgDecker.Engine/
```

Expected output: `Build succeeded. 0 Warning(s) 0 Error(s)`

**Step 3:** Commit.

```bash
git add src/MtgDecker.Engine/Enums/ManaColor.cs
git commit -m "feat(engine): add ManaColor enum"
```

---

### Task 2: CardType Flags Enum

**Files:**
- Create: `src/MtgDecker.Engine/Enums/CardType.cs`

**Step 1:** Create the CardType flags enum file.

```csharp
// src/MtgDecker.Engine/Enums/CardType.cs
namespace MtgDecker.Engine.Enums;

[Flags]
public enum CardType
{
    None = 0,
    Land = 1,
    Creature = 2,
    Enchantment = 4,
    Instant = 8,
    Sorcery = 16,
    Artifact = 32
}
```

**Step 2:** Verify it compiles.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet build src/MtgDecker.Engine/
```

Expected output: `Build succeeded. 0 Warning(s) 0 Error(s)`

**Step 3:** Commit.

```bash
git add src/MtgDecker.Engine/Enums/CardType.cs
git commit -m "feat(engine): add CardType flags enum"
```

---

### Task 3: ManaCost Value Object with Parsing

**Files:**
- Create: `tests/MtgDecker.Engine.Tests/Mana/ManaCostTests.cs`
- Create: `src/MtgDecker.Engine/Mana/ManaCost.cs`

**Step 1:** Write the failing tests.

```csharp
// tests/MtgDecker.Engine.Tests/Mana/ManaCostTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.Mana;

public class ManaCostTests
{
    [Fact]
    public void Parse_SingleColor_ParsesCorrectly()
    {
        var cost = ManaCost.Parse("{R}");

        cost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(1);
        cost.GenericCost.Should().Be(0);
        cost.ConvertedManaCost.Should().Be(1);
    }

    [Fact]
    public void Parse_GenericPlusColor_ParsesCorrectly()
    {
        var cost = ManaCost.Parse("{1}{R}");

        cost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(1);
        cost.GenericCost.Should().Be(1);
        cost.ConvertedManaCost.Should().Be(2);
    }

    [Fact]
    public void Parse_TwoSameColor_ParsesCorrectly()
    {
        var cost = ManaCost.Parse("{1}{R}{R}");

        cost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(2);
        cost.GenericCost.Should().Be(1);
        cost.ConvertedManaCost.Should().Be(3);
    }

    [Fact]
    public void Parse_TwoDifferentColors_ParsesCorrectly()
    {
        var cost = ManaCost.Parse("{G}{W}");

        cost.ColorRequirements.Should().ContainKey(ManaColor.Green).WhoseValue.Should().Be(1);
        cost.ColorRequirements.Should().ContainKey(ManaColor.White).WhoseValue.Should().Be(1);
        cost.GenericCost.Should().Be(0);
        cost.ConvertedManaCost.Should().Be(2);
    }

    [Fact]
    public void Parse_GenericOnly_ParsesCorrectly()
    {
        var cost = ManaCost.Parse("{3}");

        cost.ColorRequirements.Should().BeEmpty();
        cost.GenericCost.Should().Be(3);
        cost.ConvertedManaCost.Should().Be(3);
    }

    [Fact]
    public void Parse_ComplexCost_ParsesCorrectly()
    {
        var cost = ManaCost.Parse("{3}{R}{R}");

        cost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(2);
        cost.GenericCost.Should().Be(3);
        cost.ConvertedManaCost.Should().Be(5);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsZero()
    {
        var cost = ManaCost.Parse("");

        cost.ConvertedManaCost.Should().Be(0);
        cost.GenericCost.Should().Be(0);
        cost.ColorRequirements.Should().BeEmpty();
    }

    [Fact]
    public void Parse_Null_ReturnsZero()
    {
        var cost = ManaCost.Parse(null);

        cost.ConvertedManaCost.Should().Be(0);
        cost.GenericCost.Should().Be(0);
        cost.ColorRequirements.Should().BeEmpty();
    }

    [Fact]
    public void Parse_LargeGeneric_ParsesCorrectly()
    {
        var cost = ManaCost.Parse("{2}{W}{W}");

        cost.ColorRequirements.Should().ContainKey(ManaColor.White).WhoseValue.Should().Be(2);
        cost.GenericCost.Should().Be(2);
        cost.ConvertedManaCost.Should().Be(4);
    }

    [Fact]
    public void ConvertedManaCost_SumsCorrectly()
    {
        var cost = ManaCost.Parse("{1}{G}");

        cost.ConvertedManaCost.Should().Be(2);
    }

    [Fact]
    public void Zero_HasNoCost()
    {
        var cost = ManaCost.Zero;

        cost.ConvertedManaCost.Should().Be(0);
        cost.GenericCost.Should().Be(0);
        cost.ColorRequirements.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ColorlessMana_ParsesCorrectly()
    {
        var cost = ManaCost.Parse("{C}");

        cost.ColorRequirements.Should().ContainKey(ManaColor.Colorless).WhoseValue.Should().Be(1);
        cost.GenericCost.Should().Be(0);
        cost.ConvertedManaCost.Should().Be(1);
    }
}
```

**Step 2:** Run the tests — confirm they fail (ManaCost class does not exist yet).

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~ManaCostTests" -v n
```

Expected output: Build failure — `ManaCost` type not found.

**Step 3:** Write the ManaCost implementation.

```csharp
// src/MtgDecker.Engine/Mana/ManaCost.cs
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Mana;

public sealed partial class ManaCost
{
    private static readonly ReadOnlyDictionary<string, ManaColor> SymbolToColor = new(
        new Dictionary<string, ManaColor>
        {
            ["W"] = ManaColor.White,
            ["U"] = ManaColor.Blue,
            ["B"] = ManaColor.Black,
            ["R"] = ManaColor.Red,
            ["G"] = ManaColor.Green,
            ["C"] = ManaColor.Colorless
        });

    public static ManaCost Zero { get; } = new(new Dictionary<ManaColor, int>(), 0);

    public IReadOnlyDictionary<ManaColor, int> ColorRequirements { get; }
    public int GenericCost { get; }
    public int ConvertedManaCost { get; }

    private ManaCost(Dictionary<ManaColor, int> colorRequirements, int genericCost)
    {
        ColorRequirements = new ReadOnlyDictionary<ManaColor, int>(colorRequirements);
        GenericCost = genericCost;
        ConvertedManaCost = genericCost + colorRequirements.Values.Sum();
    }

    public static ManaCost Parse(string? manaCostString)
    {
        if (string.IsNullOrEmpty(manaCostString))
            return Zero;

        var colorRequirements = new Dictionary<ManaColor, int>();
        var genericCost = 0;

        foreach (Match match in ManaSymbolRegex().Matches(manaCostString))
        {
            var symbol = match.Groups[1].Value;

            if (SymbolToColor.TryGetValue(symbol, out var color))
            {
                colorRequirements.TryGetValue(color, out var current);
                colorRequirements[color] = current + 1;
            }
            else if (int.TryParse(symbol, out var generic))
            {
                genericCost += generic;
            }
        }

        return new ManaCost(colorRequirements, genericCost);
    }

    [GeneratedRegex(@"\{([^}]+)\}")]
    private static partial Regex ManaSymbolRegex();
}
```

**Step 4:** Run the tests — confirm all 12 pass.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~ManaCostTests" -v n
```

Expected output: `Passed! - Failed: 0, Passed: 12, Skipped: 0, Total: 12`

**Step 5:** Commit.

```bash
git add src/MtgDecker.Engine/Mana/ManaCost.cs tests/MtgDecker.Engine.Tests/Mana/ManaCostTests.cs
git commit -m "feat(engine): add ManaCost value object with Scryfall format parsing"
```

---

### Task 4: ManaPool Class

**Files:**
- Create: `tests/MtgDecker.Engine.Tests/Mana/ManaPoolTests.cs`
- Create: `src/MtgDecker.Engine/Mana/ManaPool.cs`

**Step 1:** Write all ManaPool tests (RED).

```csharp
// tests/MtgDecker.Engine.Tests/Mana/ManaPoolTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.Mana;

public class ManaPoolTests
{
    [Fact]
    public void Add_IncreasesColorAmount()
    {
        var pool = new ManaPool();

        pool.Add(ManaColor.Red, 3);

        pool[ManaColor.Red].Should().Be(3);
    }

    [Fact]
    public void Add_MultipleColors_TracksIndependently()
    {
        var pool = new ManaPool();

        pool.Add(ManaColor.Red, 2);
        pool.Add(ManaColor.Blue, 1);

        pool[ManaColor.Red].Should().Be(2);
        pool[ManaColor.Blue].Should().Be(1);
    }

    [Fact]
    public void Add_SameColorTwice_Accumulates()
    {
        var pool = new ManaPool();

        pool.Add(ManaColor.Green, 2);
        pool.Add(ManaColor.Green, 3);

        pool[ManaColor.Green].Should().Be(5);
    }

    [Fact]
    public void Add_ZeroOrNegativeAmount_DoesNothing()
    {
        var pool = new ManaPool();

        pool.Add(ManaColor.White, 0);
        pool.Add(ManaColor.White, -1);

        pool[ManaColor.White].Should().Be(0);
    }

    [Fact]
    public void Total_SumsAllColors()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 2);
        pool.Add(ManaColor.Blue, 1);
        pool.Add(ManaColor.Green, 3);

        pool.Total.Should().Be(6);
    }

    [Fact]
    public void Indexer_UnaddedColor_ReturnsZero()
    {
        var pool = new ManaPool();

        pool[ManaColor.Black].Should().Be(0);
    }

    [Fact]
    public void CanPay_ExactMana_ReturnsTrue()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 2);
        pool.Add(ManaColor.Blue, 1);

        var cost = ManaCost.Parse("{U}{R}{R}");

        pool.CanPay(cost).Should().BeTrue();
    }

    [Fact]
    public void CanPay_InsufficientColor_ReturnsFalse()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 1);
        pool.Add(ManaColor.Blue, 1);

        var cost = ManaCost.Parse("{R}{R}");

        pool.CanPay(cost).Should().BeFalse();
    }

    [Fact]
    public void CanPay_InsufficientTotal_ReturnsFalse()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 1);

        var cost = ManaCost.Parse("{2}{R}");

        pool.CanPay(cost).Should().BeFalse();
    }

    [Fact]
    public void CanPay_GenericPaidByAnyColor_ReturnsTrue()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 1);
        pool.Add(ManaColor.Green, 2);

        var cost = ManaCost.Parse("{2}{R}");

        pool.CanPay(cost).Should().BeTrue();
    }

    [Fact]
    public void Pay_DeductsColoredFirst_ThenGeneric()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 2);
        pool.Add(ManaColor.Green, 3);

        var cost = ManaCost.Parse("{2}{R}");

        var result = pool.Pay(cost);

        result.Should().BeTrue();
        pool[ManaColor.Red].Should().Be(1);
        pool[ManaColor.Green].Should().Be(1);
    }

    [Fact]
    public void Pay_InsufficientMana_ReturnsFalse_NoChange()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 1);

        var cost = ManaCost.Parse("{2}{R}");

        var result = pool.Pay(cost);

        result.Should().BeFalse();
        pool[ManaColor.Red].Should().Be(1);
    }

    [Fact]
    public void Pay_ExactAmount_EmptiesPool()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.White, 2);

        var cost = ManaCost.Parse("{W}{W}");

        var result = pool.Pay(cost);

        result.Should().BeTrue();
        pool[ManaColor.White].Should().Be(0);
        pool.Total.Should().Be(0);
    }

    [Fact]
    public void Clear_EmptiesPool()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 3);
        pool.Add(ManaColor.Blue, 2);

        pool.Clear();

        pool.Total.Should().Be(0);
        pool[ManaColor.Red].Should().Be(0);
        pool[ManaColor.Blue].Should().Be(0);
    }

    [Fact]
    public void Available_ReturnsOnlyNonZeroColors()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 2);
        pool.Add(ManaColor.Blue, 0);
        pool.Add(ManaColor.Green, 1);

        var available = pool.Available;

        available.Should().HaveCount(2);
        available.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(2);
        available.Should().ContainKey(ManaColor.Green).WhoseValue.Should().Be(1);
        available.Should().NotContainKey(ManaColor.Blue);
    }

    [Fact]
    public void CanPay_ZeroCost_AlwaysTrue()
    {
        var pool = new ManaPool();

        var cost = ManaCost.Parse("{0}");

        pool.CanPay(cost).Should().BeTrue();
    }
}
```

**Step 2:** Run tests to confirm RED (compilation failure — ManaPool does not exist).

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~ManaPoolTests" -v n
```

Expected: Build error — `ManaPool` type not found.

**Step 3:** Write ManaPool implementation (GREEN).

```csharp
// src/MtgDecker.Engine/Mana/ManaPool.cs
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Mana;

public class ManaPool
{
    private readonly Dictionary<ManaColor, int> _pool = new();

    public int this[ManaColor color] => _pool.GetValueOrDefault(color, 0);

    public int Total => _pool.Values.Sum();

    public IReadOnlyDictionary<ManaColor, int> Available =>
        _pool.Where(kv => kv.Value > 0)
             .ToDictionary(kv => kv.Key, kv => kv.Value);

    public void Add(ManaColor color, int amount = 1)
    {
        if (amount <= 0) return;
        _pool[color] = _pool.GetValueOrDefault(color, 0) + amount;
    }

    public bool CanPay(ManaCost cost)
    {
        foreach (var (color, required) in cost.ColorRequirements)
        {
            if (this[color] < required) return false;
        }

        var totalAfterColored = Total - cost.ColorRequirements.Values.Sum();
        return totalAfterColored >= cost.GenericCost;
    }

    public bool Pay(ManaCost cost)
    {
        if (!CanPay(cost)) return false;

        foreach (var (color, required) in cost.ColorRequirements)
        {
            _pool[color] -= required;
            if (_pool[color] == 0) _pool.Remove(color);
        }

        var remaining = cost.GenericCost;
        while (remaining > 0)
        {
            var largest = _pool.OrderByDescending(kv => kv.Value).FirstOrDefault();
            if (largest.Value <= 0) break;
            var take = Math.Min(remaining, largest.Value);
            _pool[largest.Key] -= take;
            if (_pool[largest.Key] == 0) _pool.Remove(largest.Key);
            remaining -= take;
        }

        return true;
    }

    public void Deduct(ManaColor color, int amount)
    {
        if (!_pool.ContainsKey(color)) return;
        _pool[color] = Math.Max(0, _pool[color] - amount);
        if (_pool[color] == 0) _pool.Remove(color);
    }

    public void Clear() => _pool.Clear();
}
```

**Step 4:** Run tests to confirm GREEN (all 15 pass).

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~ManaPoolTests" -v n
```

Expected: `Passed! - Failed: 0, Passed: 15, Skipped: 0`

**Step 5:** Commit.

```bash
git add src/MtgDecker.Engine/Mana/ManaPool.cs tests/MtgDecker.Engine.Tests/Mana/ManaPoolTests.cs
git commit -m "feat(engine): add ManaPool with add/pay/canPay tracking"
```

---

### Task 5: ManaAbility Class

**Files:**
- Create: `tests/MtgDecker.Engine.Tests/Mana/ManaAbilityTests.cs`
- Create: `src/MtgDecker.Engine/Mana/ManaAbility.cs`

**Step 1:** Write all ManaAbility tests (RED).

```csharp
// tests/MtgDecker.Engine.Tests/Mana/ManaAbilityTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.Mana;

public class ManaAbilityTests
{
    [Fact]
    public void Fixed_StoresColor()
    {
        var ability = ManaAbility.Fixed(ManaColor.Red);

        ability.FixedColor.Should().Be(ManaColor.Red);
    }

    [Fact]
    public void Fixed_HasFixedType()
    {
        var ability = ManaAbility.Fixed(ManaColor.Blue);

        ability.Type.Should().Be(ManaAbilityType.Fixed);
    }

    [Fact]
    public void Fixed_HasNoChoiceColors()
    {
        var ability = ManaAbility.Fixed(ManaColor.Green);

        ability.ChoiceColors.Should().BeNull();
    }

    [Fact]
    public void Choice_StoresAllOptions()
    {
        var ability = ManaAbility.Choice(ManaColor.Colorless, ManaColor.Red, ManaColor.Green);

        ability.ChoiceColors.Should().NotBeNull();
        ability.ChoiceColors.Should().HaveCount(3);
        ability.ChoiceColors.Should().ContainInOrder(ManaColor.Colorless, ManaColor.Red, ManaColor.Green);
    }

    [Fact]
    public void Choice_HasChoiceType()
    {
        var ability = ManaAbility.Choice(ManaColor.White, ManaColor.Black);

        ability.Type.Should().Be(ManaAbilityType.Choice);
    }

    [Fact]
    public void Choice_HasNoFixedColor()
    {
        var ability = ManaAbility.Choice(ManaColor.Red, ManaColor.Green);

        ability.FixedColor.Should().BeNull();
    }
}
```

**Step 2:** Run tests to confirm RED (compilation failure — ManaAbility does not exist).

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~ManaAbilityTests" -v n
```

Expected: Build error — `ManaAbility` type not found.

**Step 3:** Write ManaAbility implementation (GREEN).

```csharp
// src/MtgDecker.Engine/Mana/ManaAbility.cs
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Mana;

public class ManaAbility
{
    public ManaAbilityType Type { get; }
    public ManaColor? FixedColor { get; }
    public IReadOnlyList<ManaColor>? ChoiceColors { get; }

    private ManaAbility(ManaAbilityType type, ManaColor? fixedColor, IReadOnlyList<ManaColor>? choiceColors)
    {
        Type = type;
        FixedColor = fixedColor;
        ChoiceColors = choiceColors;
    }

    public static ManaAbility Fixed(ManaColor color) =>
        new(ManaAbilityType.Fixed, color, null);

    public static ManaAbility Choice(params ManaColor[] colors) =>
        new(ManaAbilityType.Choice, null, colors.ToList().AsReadOnly());
}

public enum ManaAbilityType
{
    Fixed,
    Choice
}
```

**Step 4:** Run tests to confirm GREEN (all 6 pass).

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~ManaAbilityTests" -v n
```

Expected: `Passed! - Failed: 0, Passed: 6, Skipped: 0`

**Step 5:** Commit.

```bash
git add src/MtgDecker.Engine/Mana/ManaAbility.cs tests/MtgDecker.Engine.Tests/Mana/ManaAbilityTests.cs
git commit -m "feat(engine): add ManaAbility with Fixed and Choice factory methods"
```

---

### Task 6: CardDefinitions Static Registry

**Files:**
- Create: `src/MtgDecker.Engine/CardDefinition.cs`
- Create: `src/MtgDecker.Engine/CardDefinitions.cs`
- Create: `tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs`

**Step 1:** Write all 11 tests for CardDefinitions.

```csharp
// tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests;

public class CardDefinitionsTests
{
    [Fact]
    public void TryGet_KnownCreature_ReturnsDefinition()
    {
        var result = CardDefinitions.TryGet("Goblin Lackey", out var def);

        result.Should().BeTrue();
        def.Should().NotBeNull();
    }

    [Fact]
    public void TryGet_KnownLand_ReturnsDefinition()
    {
        var result = CardDefinitions.TryGet("Mountain", out var def);

        result.Should().BeTrue();
        def.Should().NotBeNull();
    }

    [Fact]
    public void TryGet_UnknownCard_ReturnsFalse()
    {
        var result = CardDefinitions.TryGet("Nonexistent Card XYZ", out var def);

        result.Should().BeFalse();
        def.Should().BeNull();
    }

    [Fact]
    public void GoblinLackey_HasCorrectCost()
    {
        CardDefinitions.TryGet("Goblin Lackey", out var def);

        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(1);
        def.ManaCost.GenericCost.Should().Be(0);
    }

    [Fact]
    public void Mountain_HasFixedManaAbility()
    {
        CardDefinitions.TryGet("Mountain", out var def);

        def!.ManaAbility.Should().NotBeNull();
        def.ManaAbility!.Type.Should().Be(ManaAbilityType.Fixed);
        def.ManaAbility.FixedColor.Should().Be(ManaColor.Red);
    }

    [Fact]
    public void KarplusanForest_HasChoiceManaAbility()
    {
        CardDefinitions.TryGet("Karplusan Forest", out var def);

        def!.ManaAbility.Should().NotBeNull();
        def.ManaAbility!.Type.Should().Be(ManaAbilityType.Choice);
        def.ManaAbility.ChoiceColors.Should().BeEquivalentTo(
            new[] { ManaColor.Colorless, ManaColor.Red, ManaColor.Green });
    }

    [Fact]
    public void SiegeGangCommander_HasCorrectCostAndStats()
    {
        CardDefinitions.TryGet("Siege-Gang Commander", out var def);

        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(5);
        def.ManaCost.GenericCost.Should().Be(3);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(2);
        def.Power.Should().Be(2);
        def.Toughness.Should().Be(2);
    }

    [Fact]
    public void ArgothianEnchantress_HasCreatureAndEnchantmentTypes()
    {
        CardDefinitions.TryGet("Argothian Enchantress", out var def);

        def!.CardTypes.Should().HaveFlag(CardType.Creature);
        def.CardTypes.Should().HaveFlag(CardType.Enchantment);
    }

    [Fact]
    public void AllStarterDeckCards_HaveDefinitions()
    {
        var allCardNames = new[]
        {
            "Goblin Lackey", "Goblin Matron", "Goblin Piledriver", "Goblin Ringleader",
            "Goblin Warchief", "Mogg Fanatic", "Gempalm Incinerator", "Siege-Gang Commander",
            "Goblin King", "Goblin Pyromancer", "Goblin Sharpshooter", "Goblin Tinkerer",
            "Skirk Prospector", "Naturalize", "Mountain", "Forest", "Karplusan Forest",
            "Wooded Foothills", "Rishadan Port", "Wasteland",
            "Argothian Enchantress", "Swords to Plowshares", "Replenish",
            "Enchantress's Presence", "Wild Growth", "Exploration", "Mirri's Guile",
            "Opalescence", "Parallax Wave", "Sterling Grove", "Aura of Silence",
            "Seal of Cleansing", "Solitary Confinement", "Sylvan Library",
            "Plains", "Brushland", "Windswept Heath", "Serra's Sanctum"
        };

        foreach (var name in allCardNames)
        {
            CardDefinitions.TryGet(name, out var def).Should().BeTrue(
                because: $"'{name}' should be registered in CardDefinitions");
        }
    }

    [Theory]
    [InlineData("Mountain")]
    [InlineData("Forest")]
    [InlineData("Plains")]
    public void Lands_HaveNoManaCost(string landName)
    {
        CardDefinitions.TryGet(landName, out var def);

        def!.ManaCost.Should().BeNull();
    }

    [Theory]
    [InlineData("Naturalize")]
    [InlineData("Swords to Plowshares")]
    public void Instants_HaveCorrectType(string cardName)
    {
        CardDefinitions.TryGet(cardName, out var def);

        def!.CardTypes.Should().HaveFlag(CardType.Instant);
    }
}
```

**Step 2:** Run tests — they should fail (classes do not exist).

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~CardDefinitionsTests" -v n
```

Expected: Build errors.

**Step 3:** Write the `CardDefinition` record.

```csharp
// src/MtgDecker.Engine/CardDefinition.cs
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public record CardDefinition(
    ManaCost? ManaCost,
    ManaAbility? ManaAbility,
    int? Power,
    int? Toughness,
    CardType CardTypes
);
```

**Step 4:** Write the `CardDefinitions` static registry.

```csharp
// src/MtgDecker.Engine/CardDefinitions.cs
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public static class CardDefinitions
{
    private static readonly FrozenDictionary<string, CardDefinition> Registry;

    static CardDefinitions()
    {
        var cards = new Dictionary<string, CardDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            // === Goblins deck ===
            ["Goblin Lackey"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature),
            ["Goblin Matron"] = new(ManaCost.Parse("{2}{R}"), null, 1, 1, CardType.Creature),
            ["Goblin Piledriver"] = new(ManaCost.Parse("{1}{R}"), null, 1, 2, CardType.Creature),
            ["Goblin Ringleader"] = new(ManaCost.Parse("{3}{R}"), null, 2, 2, CardType.Creature),
            ["Goblin Warchief"] = new(ManaCost.Parse("{1}{R}{R}"), null, 2, 2, CardType.Creature),
            ["Mogg Fanatic"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature),
            ["Gempalm Incinerator"] = new(ManaCost.Parse("{1}{R}"), null, 2, 1, CardType.Creature),
            ["Siege-Gang Commander"] = new(ManaCost.Parse("{3}{R}{R}"), null, 2, 2, CardType.Creature),
            ["Goblin King"] = new(ManaCost.Parse("{1}{R}{R}"), null, 2, 2, CardType.Creature),
            ["Goblin Pyromancer"] = new(ManaCost.Parse("{3}{R}"), null, 2, 2, CardType.Creature),
            ["Goblin Sharpshooter"] = new(ManaCost.Parse("{2}{R}"), null, 1, 1, CardType.Creature),
            ["Goblin Tinkerer"] = new(ManaCost.Parse("{1}{R}"), null, 1, 1, CardType.Creature),
            ["Skirk Prospector"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature),
            ["Naturalize"] = new(ManaCost.Parse("{1}{G}"), null, null, null, CardType.Instant),

            // === Goblins lands ===
            ["Mountain"] = new(null, ManaAbility.Fixed(ManaColor.Red), null, null, CardType.Land),
            ["Forest"] = new(null, ManaAbility.Fixed(ManaColor.Green), null, null, CardType.Land),
            ["Karplusan Forest"] = new(null, ManaAbility.Choice(ManaColor.Colorless, ManaColor.Red, ManaColor.Green), null, null, CardType.Land),
            ["Wooded Foothills"] = new(null, null, null, null, CardType.Land),
            ["Rishadan Port"] = new(null, null, null, null, CardType.Land),
            ["Wasteland"] = new(null, null, null, null, CardType.Land),

            // === Enchantress deck ===
            ["Argothian Enchantress"] = new(ManaCost.Parse("{1}{G}"), null, 0, 1, CardType.Creature | CardType.Enchantment),
            ["Swords to Plowshares"] = new(ManaCost.Parse("{W}"), null, null, null, CardType.Instant),
            ["Replenish"] = new(ManaCost.Parse("{3}{W}"), null, null, null, CardType.Sorcery),
            ["Enchantress's Presence"] = new(ManaCost.Parse("{2}{G}"), null, null, null, CardType.Enchantment),
            ["Wild Growth"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Enchantment),
            ["Exploration"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Enchantment),
            ["Mirri's Guile"] = new(ManaCost.Parse("{G}"), null, null, null, CardType.Enchantment),
            ["Opalescence"] = new(ManaCost.Parse("{2}{W}{W}"), null, null, null, CardType.Enchantment),
            ["Parallax Wave"] = new(ManaCost.Parse("{2}{W}{W}"), null, null, null, CardType.Enchantment),
            ["Sterling Grove"] = new(ManaCost.Parse("{G}{W}"), null, null, null, CardType.Enchantment),
            ["Aura of Silence"] = new(ManaCost.Parse("{1}{W}{W}"), null, null, null, CardType.Enchantment),
            ["Seal of Cleansing"] = new(ManaCost.Parse("{1}{W}"), null, null, null, CardType.Enchantment),
            ["Solitary Confinement"] = new(ManaCost.Parse("{2}{W}"), null, null, null, CardType.Enchantment),
            ["Sylvan Library"] = new(ManaCost.Parse("{1}{G}"), null, null, null, CardType.Enchantment),

            // === Enchantress lands ===
            ["Plains"] = new(null, ManaAbility.Fixed(ManaColor.White), null, null, CardType.Land),
            ["Brushland"] = new(null, ManaAbility.Choice(ManaColor.Colorless, ManaColor.Green, ManaColor.White), null, null, CardType.Land),
            ["Windswept Heath"] = new(null, null, null, null, CardType.Land),
            ["Serra's Sanctum"] = new(null, null, null, null, CardType.Land),
        };

        Registry = cards.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public static bool TryGet(string cardName, [NotNullWhen(true)] out CardDefinition? definition)
    {
        return Registry.TryGetValue(cardName, out definition);
    }
}
```

**Step 5:** Run tests — all 11 should pass.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~CardDefinitionsTests" -v n
```

Expected: `Passed! - Failed: 0, Passed: 11, Skipped: 0`

**Step 6:** Commit.

```bash
git add src/MtgDecker.Engine/CardDefinition.cs src/MtgDecker.Engine/CardDefinitions.cs tests/MtgDecker.Engine.Tests/CardDefinitionsTests.cs
git commit -m "feat(engine): add CardDefinitions static registry with starter deck cards"
```

---

### Task 7: GameCard Extensions

**Files:**
- Modify: `src/MtgDecker.Engine/GameCard.cs`
- Create: `tests/MtgDecker.Engine.Tests/GameCardTests.cs`

**Step 1:** Write all 8 tests for GameCard factory and resolved properties.

```csharp
// tests/MtgDecker.Engine.Tests/GameCardTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests;

public class GameCardTests
{
    [Fact]
    public void Create_KnownCard_ResolvesManaCost()
    {
        var card = GameCard.Create("Goblin Lackey", "Creature — Goblin");

        card.ManaCost.Should().NotBeNull();
        card.ManaCost!.ConvertedManaCost.Should().Be(1);
        card.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(1);
    }

    [Fact]
    public void Create_KnownCard_ResolvesManaAbility()
    {
        var card = GameCard.Create("Mountain", "Basic Land — Mountain");

        card.ManaAbility.Should().NotBeNull();
        card.ManaAbility!.Type.Should().Be(ManaAbilityType.Fixed);
        card.ManaAbility.FixedColor.Should().Be(ManaColor.Red);
    }

    [Fact]
    public void Create_KnownCard_ResolvesPowerToughness()
    {
        var card = GameCard.Create("Goblin Lackey", "Creature — Goblin");

        card.Power.Should().Be(1);
        card.Toughness.Should().Be(1);
    }

    [Fact]
    public void Create_KnownCard_ResolvesCardTypes()
    {
        var card = GameCard.Create("Mountain", "Basic Land — Mountain");

        card.CardTypes.Should().HaveFlag(CardType.Land);
    }

    [Fact]
    public void Create_UnknownCard_LeavesPropertiesNull()
    {
        var card = GameCard.Create("Totally Unknown Card", "Artifact");

        card.ManaCost.Should().BeNull();
        card.ManaAbility.Should().BeNull();
        card.Power.Should().BeNull();
        card.Toughness.Should().BeNull();
    }

    [Fact]
    public void Create_UnknownCard_CardTypesIsNone()
    {
        var card = GameCard.Create("Totally Unknown Card", "Artifact");

        card.CardTypes.Should().Be(CardType.None);
    }

    [Fact]
    public void IsLand_TrueForRegisteredLand()
    {
        var card = GameCard.Create("Mountain", "Basic Land — Mountain");

        card.IsLand.Should().BeTrue();
    }

    [Fact]
    public void IsLand_TrueForTypeLine_BackwardCompat()
    {
        var card = new GameCard { Name = "Some Custom Land", TypeLine = "Land" };

        card.IsLand.Should().BeTrue();
    }
}
```

**Step 2:** Run tests — they should fail (new properties/method do not exist).

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~GameCardTests" -v n
```

Expected: Build errors.

**Step 3:** Modify `GameCard.cs` to add resolved properties and factory method.

```csharp
// src/MtgDecker.Engine/GameCard.cs
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public class GameCard
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = string.Empty;
    public string TypeLine { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public bool IsTapped { get; set; }

    // Resolved from CardDefinitions registry
    public ManaCost? ManaCost { get; set; }
    public ManaAbility? ManaAbility { get; set; }
    public int? Power { get; set; }
    public int? Toughness { get; set; }
    public CardType CardTypes { get; set; } = CardType.None;

    // Backward-compatible: check both CardTypes flags and TypeLine
    public bool IsLand =>
        CardTypes.HasFlag(CardType.Land) ||
        TypeLine.Contains("Land", StringComparison.OrdinalIgnoreCase);

    public bool IsCreature =>
        CardTypes.HasFlag(CardType.Creature) ||
        TypeLine.Contains("Creature", StringComparison.OrdinalIgnoreCase);

    public static GameCard Create(string name, string typeLine = "", string? imageUrl = null)
    {
        var card = new GameCard { Name = name, TypeLine = typeLine, ImageUrl = imageUrl };
        if (CardDefinitions.TryGet(name, out var def))
        {
            card.ManaCost = def.ManaCost;
            card.ManaAbility = def.ManaAbility;
            card.Power = def.Power;
            card.Toughness = def.Toughness;
            card.CardTypes = def.CardTypes;
        }
        return card;
    }
}
```

**Step 4:** Run tests — all 8 should pass.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~GameCardTests" -v n
```

Expected: `Passed! - Failed: 0, Passed: 8, Skipped: 0`

**Step 5:** Run all engine tests to confirm no regressions.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ -v n
```

Expected: All tests pass.

**Step 6:** Commit.

```bash
git add src/MtgDecker.Engine/GameCard.cs tests/MtgDecker.Engine.Tests/GameCardTests.cs
git commit -m "feat(engine): add GameCard factory with CardDefinitions resolution"
```

---

### Task 8: Player Extensions (ManaPool + LandsPlayedThisTurn)

**Files:**
- Modify: `src/MtgDecker.Engine/Player.cs`
- Create: `tests/MtgDecker.Engine.Tests/PlayerManaTests.cs`

**Step 1:** Write all 4 tests (RED).

```csharp
// tests/MtgDecker.Engine.Tests/PlayerManaTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class PlayerManaTests
{
    private Player CreatePlayer() =>
        new(Guid.NewGuid(), "Alice", new TestDecisionHandler());

    [Fact]
    public void NewPlayer_HasEmptyManaPool()
    {
        var player = CreatePlayer();

        player.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public void NewPlayer_LandsPlayedThisTurn_IsZero()
    {
        var player = CreatePlayer();

        player.LandsPlayedThisTurn.Should().Be(0);
    }

    [Fact]
    public void ManaPool_CanAddAndTrackMana()
    {
        var player = CreatePlayer();

        player.ManaPool.Add(ManaColor.Green, 1);
        player.ManaPool.Add(ManaColor.Red, 2);

        player.ManaPool[ManaColor.Green].Should().Be(1);
        player.ManaPool[ManaColor.Red].Should().Be(2);
        player.ManaPool.Total.Should().Be(3);
    }

    [Fact]
    public void LandsPlayedThisTurn_CanBeSetAndReset()
    {
        var player = CreatePlayer();

        player.LandsPlayedThisTurn = 1;
        player.LandsPlayedThisTurn.Should().Be(1);

        player.LandsPlayedThisTurn = 0;
        player.LandsPlayedThisTurn.Should().Be(0);
    }
}
```

**Step 2:** Run tests to confirm RED.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~PlayerManaTests" -v n
```

Expected: Build errors — `Player` has no `ManaPool` or `LandsPlayedThisTurn`.

**Step 3:** Modify `src/MtgDecker.Engine/Player.cs` to add the two new properties.

```csharp
// src/MtgDecker.Engine/Player.cs
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public class Player
{
    public Guid Id { get; }
    public string Name { get; }
    public IPlayerDecisionHandler DecisionHandler { get; }

    public Zone Library { get; }
    public Zone Hand { get; }
    public Zone Battlefield { get; }
    public Zone Graveyard { get; }
    public Zone Exile { get; }
    public int Life { get; private set; } = 20;
    public Stack<GameAction> ActionHistory { get; } = new();
    public ManaPool ManaPool { get; } = new();
    public int LandsPlayedThisTurn { get; set; }

    public void AdjustLife(int delta)
    {
        Life += delta;
    }

    public Player(Guid id, string name, IPlayerDecisionHandler decisionHandler)
    {
        Id = id;
        Name = name;
        DecisionHandler = decisionHandler;
        Library = new Zone(ZoneType.Library);
        Hand = new Zone(ZoneType.Hand);
        Battlefield = new Zone(ZoneType.Battlefield);
        Graveyard = new Zone(ZoneType.Graveyard);
        Exile = new Zone(ZoneType.Exile);
    }

    public Zone GetZone(ZoneType type) => type switch
    {
        ZoneType.Library => Library,
        ZoneType.Hand => Hand,
        ZoneType.Battlefield => Battlefield,
        ZoneType.Graveyard => Graveyard,
        ZoneType.Exile => Exile,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
```

**Step 4:** Run tests to confirm GREEN.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~PlayerManaTests" -v n
```

Expected: `Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4`

**Step 5:** Run all existing tests to confirm no regressions.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ -v n
```

Expected: All tests pass.

**Step 6:** Commit.

```bash
git add src/MtgDecker.Engine/Player.cs tests/MtgDecker.Engine.Tests/PlayerManaTests.cs
git commit -m "feat(engine): add ManaPool and LandsPlayedThisTurn to Player"
```

---

### Task 9: Decision Handler Extensions (Mana Color Choice + Generic Payment)

**Files:**
- Modify: `src/MtgDecker.Engine/IPlayerDecisionHandler.cs`
- Modify: `src/MtgDecker.Engine/InteractiveDecisionHandler.cs`
- Modify: `tests/MtgDecker.Engine.Tests/Helpers/TestDecisionHandler.cs`
- Create: `tests/MtgDecker.Engine.Tests/DecisionHandlerManaTests.cs`

**Step 1:** Write all 7 tests (RED).

```csharp
// tests/MtgDecker.Engine.Tests/DecisionHandlerManaTests.cs
using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class DecisionHandlerManaTests
{
    [Fact]
    public async Task InteractiveHandler_ChooseManaColor_WaitsForSubmit()
    {
        var handler = new InteractiveDecisionHandler();
        var options = new List<ManaColor> { ManaColor.Red, ManaColor.Green };

        var task = handler.ChooseManaColor(options);

        task.IsCompleted.Should().BeFalse();
        handler.IsWaitingForManaColor.Should().BeTrue();
    }

    [Fact]
    public async Task InteractiveHandler_SubmitManaColor_CompletesTask()
    {
        var handler = new InteractiveDecisionHandler();
        var options = new List<ManaColor> { ManaColor.Red, ManaColor.Green };

        var task = handler.ChooseManaColor(options);
        handler.SubmitManaColor(ManaColor.Green);

        var result = await task;
        result.Should().Be(ManaColor.Green);
        handler.IsWaitingForManaColor.Should().BeFalse();
    }

    [Fact]
    public async Task InteractiveHandler_ChooseGenericPayment_WaitsForSubmit()
    {
        var handler = new InteractiveDecisionHandler();
        var available = new Dictionary<ManaColor, int>
        {
            { ManaColor.Red, 2 },
            { ManaColor.Green, 1 }
        };

        var task = handler.ChooseGenericPayment(2, available);

        task.IsCompleted.Should().BeFalse();
        handler.IsWaitingForGenericPayment.Should().BeTrue();
    }

    [Fact]
    public async Task InteractiveHandler_SubmitGenericPayment_CompletesTask()
    {
        var handler = new InteractiveDecisionHandler();
        var available = new Dictionary<ManaColor, int>
        {
            { ManaColor.Red, 2 },
            { ManaColor.Green, 1 }
        };
        var payment = new Dictionary<ManaColor, int>
        {
            { ManaColor.Red, 1 },
            { ManaColor.Green, 1 }
        };

        var task = handler.ChooseGenericPayment(2, available);
        handler.SubmitGenericPayment(payment);

        var result = await task;
        result.Should().BeEquivalentTo(payment);
        handler.IsWaitingForGenericPayment.Should().BeFalse();
    }

    [Fact]
    public async Task TestHandler_ChooseManaColor_ReturnsEnqueued()
    {
        var handler = new TestDecisionHandler();
        handler.EnqueueManaColor(ManaColor.Blue);
        var options = new List<ManaColor> { ManaColor.Red, ManaColor.Blue };

        var result = await handler.ChooseManaColor(options);

        result.Should().Be(ManaColor.Blue);
    }

    [Fact]
    public async Task TestHandler_ChooseManaColor_DefaultsToFirstOption()
    {
        var handler = new TestDecisionHandler();
        var options = new List<ManaColor> { ManaColor.Black, ManaColor.White };

        var result = await handler.ChooseManaColor(options);

        result.Should().Be(ManaColor.Black);
    }

    [Fact]
    public async Task TestHandler_ChooseGenericPayment_ReturnsEnqueued()
    {
        var handler = new TestDecisionHandler();
        var payment = new Dictionary<ManaColor, int>
        {
            { ManaColor.Red, 2 }
        };
        handler.EnqueueGenericPayment(payment);
        var available = new Dictionary<ManaColor, int>
        {
            { ManaColor.Red, 3 },
            { ManaColor.Green, 1 }
        };

        var result = await handler.ChooseGenericPayment(2, available);

        result.Should().BeEquivalentTo(payment);
    }
}
```

**Step 2:** Run tests to confirm RED.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~DecisionHandlerManaTests" -v n
```

Expected: Build errors — methods don't exist yet.

**Step 3:** Modify `src/MtgDecker.Engine/IPlayerDecisionHandler.cs`:

```csharp
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public interface IPlayerDecisionHandler
{
    Task<GameAction> GetAction(GameState gameState, Guid playerId, CancellationToken ct = default);
    Task<MulliganDecision> GetMulliganDecision(IReadOnlyList<GameCard> hand, int mulliganCount, CancellationToken ct = default);
    Task<IReadOnlyList<GameCard>> ChooseCardsToBottom(IReadOnlyList<GameCard> hand, int count, CancellationToken ct = default);
    Task<ManaColor> ChooseManaColor(IReadOnlyList<ManaColor> options, CancellationToken ct = default);
    Task<Dictionary<ManaColor, int>> ChooseGenericPayment(int genericAmount, Dictionary<ManaColor, int> available, CancellationToken ct = default);
}
```

**Step 4:** Modify `src/MtgDecker.Engine/InteractiveDecisionHandler.cs` — add TCS fields + Submit methods:

```csharp
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine;

public class InteractiveDecisionHandler : IPlayerDecisionHandler
{
    private TaskCompletionSource<GameAction>? _actionTcs;
    private TaskCompletionSource<MulliganDecision>? _mulliganTcs;
    private TaskCompletionSource<IReadOnlyList<GameCard>>? _bottomCardsTcs;
    private TaskCompletionSource<ManaColor>? _manaColorTcs;
    private TaskCompletionSource<Dictionary<ManaColor, int>>? _genericPaymentTcs;

    public bool IsWaitingForAction => _actionTcs is { Task.IsCompleted: false };
    public bool IsWaitingForMulligan => _mulliganTcs is { Task.IsCompleted: false };
    public bool IsWaitingForBottomCards => _bottomCardsTcs is { Task.IsCompleted: false };
    public bool IsWaitingForManaColor => _manaColorTcs is { Task.IsCompleted: false };
    public bool IsWaitingForGenericPayment => _genericPaymentTcs is { Task.IsCompleted: false };

    public event Action? OnWaitingForInput;

    public Task<GameAction> GetAction(GameState gameState, Guid playerId, CancellationToken ct = default)
    {
        _actionTcs = new TaskCompletionSource<GameAction>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => _actionTcs.TrySetCanceled());
        _actionTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _actionTcs.Task;
    }

    public Task<MulliganDecision> GetMulliganDecision(IReadOnlyList<GameCard> hand, int mulliganCount, CancellationToken ct = default)
    {
        _mulliganTcs = new TaskCompletionSource<MulliganDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => _mulliganTcs.TrySetCanceled());
        _mulliganTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _mulliganTcs.Task;
    }

    public Task<IReadOnlyList<GameCard>> ChooseCardsToBottom(IReadOnlyList<GameCard> hand, int count, CancellationToken ct = default)
    {
        _bottomCardsTcs = new TaskCompletionSource<IReadOnlyList<GameCard>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => _bottomCardsTcs.TrySetCanceled());
        _bottomCardsTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _bottomCardsTcs.Task;
    }

    public Task<ManaColor> ChooseManaColor(IReadOnlyList<ManaColor> options, CancellationToken ct = default)
    {
        _manaColorTcs = new TaskCompletionSource<ManaColor>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => _manaColorTcs.TrySetCanceled());
        _manaColorTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _manaColorTcs.Task;
    }

    public Task<Dictionary<ManaColor, int>> ChooseGenericPayment(int genericAmount, Dictionary<ManaColor, int> available, CancellationToken ct = default)
    {
        _genericPaymentTcs = new TaskCompletionSource<Dictionary<ManaColor, int>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = ct.Register(() => _genericPaymentTcs.TrySetCanceled());
        _genericPaymentTcs.Task.ContinueWith(_ => registration.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        OnWaitingForInput?.Invoke();
        return _genericPaymentTcs.Task;
    }

    public void SubmitAction(GameAction action) =>
        _actionTcs?.TrySetResult(action);

    public void SubmitMulliganDecision(MulliganDecision decision) =>
        _mulliganTcs?.TrySetResult(decision);

    public async Task SubmitBottomCardsAsync(IReadOnlyList<GameCard> cards)
    {
        for (int i = 0; i < 50; i++)
        {
            if (_bottomCardsTcs?.TrySetResult(cards) == true)
                return;
            await Task.Delay(10);
        }
    }

    public void SubmitManaColor(ManaColor color) =>
        _manaColorTcs?.TrySetResult(color);

    public void SubmitGenericPayment(Dictionary<ManaColor, int> payment) =>
        _genericPaymentTcs?.TrySetResult(payment);
}
```

**Step 5:** Modify `tests/MtgDecker.Engine.Tests/Helpers/TestDecisionHandler.cs`:

```csharp
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;

namespace MtgDecker.Engine.Tests.Helpers;

public class TestDecisionHandler : IPlayerDecisionHandler
{
    private readonly Queue<GameAction> _actions = new();
    private readonly Queue<MulliganDecision> _mulliganDecisions = new();
    private readonly Queue<Func<IReadOnlyList<GameCard>, int, IReadOnlyList<GameCard>>> _bottomChoices = new();
    private readonly Queue<ManaColor> _manaColorChoices = new();
    private readonly Queue<Dictionary<ManaColor, int>> _genericPaymentChoices = new();

    public void EnqueueAction(GameAction action) => _actions.Enqueue(action);
    public void EnqueueMulligan(MulliganDecision decision) => _mulliganDecisions.Enqueue(decision);
    public void EnqueueBottomChoice(Func<IReadOnlyList<GameCard>, int, IReadOnlyList<GameCard>> chooser) =>
        _bottomChoices.Enqueue(chooser);
    public void EnqueueManaColor(ManaColor color) => _manaColorChoices.Enqueue(color);
    public void EnqueueGenericPayment(Dictionary<ManaColor, int> payment) => _genericPaymentChoices.Enqueue(payment);

    public Task<GameAction> GetAction(GameState gameState, Guid playerId, CancellationToken ct = default)
    {
        if (_actions.Count == 0)
            return Task.FromResult(GameAction.Pass(playerId));
        return Task.FromResult(_actions.Dequeue());
    }

    public Task<MulliganDecision> GetMulliganDecision(IReadOnlyList<GameCard> hand, int mulliganCount, CancellationToken ct = default)
    {
        if (_mulliganDecisions.Count == 0)
            return Task.FromResult(MulliganDecision.Keep);
        return Task.FromResult(_mulliganDecisions.Dequeue());
    }

    public Task<IReadOnlyList<GameCard>> ChooseCardsToBottom(IReadOnlyList<GameCard> hand, int count, CancellationToken ct = default)
    {
        if (_bottomChoices.Count == 0)
            return Task.FromResult<IReadOnlyList<GameCard>>(hand.Take(count).ToList());
        return Task.FromResult(_bottomChoices.Dequeue()(hand, count));
    }

    public Task<ManaColor> ChooseManaColor(IReadOnlyList<ManaColor> options, CancellationToken ct = default)
    {
        if (_manaColorChoices.Count == 0)
            return Task.FromResult(options[0]);
        return Task.FromResult(_manaColorChoices.Dequeue());
    }

    public Task<Dictionary<ManaColor, int>> ChooseGenericPayment(int genericAmount, Dictionary<ManaColor, int> available, CancellationToken ct = default)
    {
        if (_genericPaymentChoices.Count == 0)
        {
            var payment = new Dictionary<ManaColor, int>();
            var remaining = genericAmount;
            foreach (var (color, amount) in available)
            {
                if (remaining <= 0) break;
                var take = Math.Min(amount, remaining);
                if (take > 0)
                {
                    payment[color] = take;
                    remaining -= take;
                }
            }
            return Task.FromResult(payment);
        }
        return Task.FromResult(_genericPaymentChoices.Dequeue());
    }
}
```

**Step 6:** Run tests to confirm GREEN.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~DecisionHandlerManaTests" -v n
```

Expected: `Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7`

**Step 7:** Run all existing tests to confirm no regressions.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ -v n
```

Expected: All tests pass.

**Step 8:** Commit.

```bash
git add src/MtgDecker.Engine/IPlayerDecisionHandler.cs src/MtgDecker.Engine/InteractiveDecisionHandler.cs tests/MtgDecker.Engine.Tests/Helpers/TestDecisionHandler.cs tests/MtgDecker.Engine.Tests/DecisionHandlerManaTests.cs
git commit -m "feat(engine): add mana color choice and generic payment to decision handlers"
```

---

### Task 10: GameEngine — Mana Pool Clearing at Phase Transitions + Land Drop Reset

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs`
- Create: `tests/MtgDecker.Engine.Tests/ManaPoolClearingTests.cs`

**Step 1:** Write 3 tests for mana pool clearing and land-drop reset.

```csharp
// tests/MtgDecker.Engine.Tests/ManaPoolClearingTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class ManaPoolClearingTests
{
    private (GameEngine engine, GameState state) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);

        // Need 7+ cards for draw
        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state);
    }

    [Fact]
    public async Task ManaPool_ClearedAfterPhase()
    {
        var (engine, state) = CreateSetup();
        await engine.StartGameAsync();

        state.Player1.ManaPool.Add(ManaColor.Red, 3);
        state.Player1.ManaPool.Total.Should().Be(3);

        await engine.RunTurnAsync();

        state.Player1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task ManaPool_ClearedForBothPlayers()
    {
        var (engine, state) = CreateSetup();
        await engine.StartGameAsync();

        state.Player1.ManaPool.Add(ManaColor.Blue, 2);
        state.Player2.ManaPool.Add(ManaColor.Green, 4);

        await engine.RunTurnAsync();

        state.Player1.ManaPool.Total.Should().Be(0);
        state.Player2.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task LandsPlayedThisTurn_ResetsAtTurnStart()
    {
        var (engine, state) = CreateSetup();
        await engine.StartGameAsync();

        state.ActivePlayer.LandsPlayedThisTurn = 1;

        var activeBeforeTurn = state.ActivePlayer;
        await engine.RunTurnAsync();

        activeBeforeTurn.LandsPlayedThisTurn.Should().Be(0);
    }
}
```

**Step 2:** Run tests — expect failures.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~ManaPoolClearingTests" -v n
```

Expected: Failures — mana pools not cleared, LandsPlayedThisTurn not reset.

**Step 3:** Modify `GameEngine.RunTurnAsync` — add land-drop reset and mana pool clearing.

Add `_state.ActivePlayer.LandsPlayedThisTurn = 0;` before the do-while loop. Add pool clearing after each phase's priority:

```csharp
public async Task RunTurnAsync(CancellationToken ct = default)
{
    _turnStateMachine.Reset();
    _state.ActivePlayer.LandsPlayedThisTurn = 0;
    _state.Log($"Turn {_state.TurnNumber}: {_state.ActivePlayer.Name}'s turn.");

    do
    {
        var phase = _turnStateMachine.CurrentPhase;
        _state.CurrentPhase = phase.Phase;
        _state.Log($"Phase: {phase.Phase}");

        if (phase.HasTurnBasedAction)
        {
            bool skipDraw = phase.Phase == Phase.Draw && _state.IsFirstTurn;
            if (!skipDraw)
                ExecuteTurnBasedAction(phase.Phase);
        }

        if (phase.GrantsPriority)
            await RunPriorityAsync(ct);

        _state.Player1.ManaPool.Clear();
        _state.Player2.ManaPool.Clear();

    } while (_turnStateMachine.AdvancePhase() != null);

    _state.IsFirstTurn = false;
    _state.TurnNumber++;
    _state.ActivePlayer = _state.GetOpponent(_state.ActivePlayer);
}
```

**Step 4:** Run tests — all 3 pass.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~ManaPoolClearingTests" -v n
```

Expected: `Passed! - Failed: 0, Passed: 3, Skipped: 0`

**Step 5:** Run full test suite.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ -v n
```

Expected: All tests pass.

**Step 6:** Commit.

```bash
git add tests/MtgDecker.Engine.Tests/ManaPoolClearingTests.cs src/MtgDecker.Engine/GameEngine.cs
git commit -m "feat(engine): clear mana pools at phase transitions, reset land drops at turn start"
```

---

### Task 11: GameEngine — Tap-for-Mana

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs`
- Create: `tests/MtgDecker.Engine.Tests/TapForManaTests.cs`

**Step 1:** Write 8 tests for tap-for-mana behavior.

```csharp
// tests/MtgDecker.Engine.Tests/TapForManaTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class TapForManaTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler handler) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);

        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1);
    }

    [Fact]
    public async Task TapBasicLand_AddsFixedManaToPool()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        state.Player1.Battlefield.Add(mountain);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, mountain.Id));

        state.Player1.ManaPool[ManaColor.Red].Should().Be(1);
    }

    [Fact]
    public async Task TapBasicLand_SetsTappedTrue()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        state.Player1.Battlefield.Add(mountain);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, mountain.Id));

        mountain.IsTapped.Should().BeTrue();
    }

    [Fact]
    public async Task TapPainLand_PromptsForChoice()
    {
        var (engine, state, handler) = CreateSetup();
        await engine.StartGameAsync();

        var karplusan = GameCard.Create("Karplusan Forest", "Land");
        state.Player1.Battlefield.Add(karplusan);

        handler.EnqueueManaColor(ManaColor.Green);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, karplusan.Id));

        state.Player1.ManaPool[ManaColor.Green].Should().Be(1);
    }

    [Fact]
    public async Task TapPainLand_AddsChosenColorToPool()
    {
        var (engine, state, handler) = CreateSetup();
        await engine.StartGameAsync();

        var karplusan = GameCard.Create("Karplusan Forest", "Land");
        state.Player1.Battlefield.Add(karplusan);

        handler.EnqueueManaColor(ManaColor.Red);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, karplusan.Id));

        state.Player1.ManaPool[ManaColor.Red].Should().Be(1);
    }

    [Fact]
    public async Task TapCardWithNoManaAbility_DoesNotAddMana()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var goblin = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        state.Player1.Battlefield.Add(goblin);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, goblin.Id));

        state.Player1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task TapCardWithNoManaAbility_StillSetsTapped()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var goblin = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        state.Player1.Battlefield.Add(goblin);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, goblin.Id));

        goblin.IsTapped.Should().BeTrue();
    }

    [Fact]
    public async Task TapCard_WithNoRegistryEntry_WorksAsBefore()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var unknownCard = new GameCard { Name = "Unknown Widget" };
        state.Player1.Battlefield.Add(unknownCard);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, unknownCard.Id));

        unknownCard.IsTapped.Should().BeTrue();
        state.Player1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task TapAlreadyTappedCard_DoesNothing()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var mountain = GameCard.Create("Mountain", "Basic Land — Mountain");
        mountain.IsTapped = true;
        state.Player1.Battlefield.Add(mountain);

        await engine.ExecuteAction(GameAction.TapCard(state.Player1.Id, mountain.Id));

        state.Player1.ManaPool.Total.Should().Be(0);
    }
}
```

**Step 2:** Run tests — expect compilation errors (`ExecuteAction` is `void`, tests `await` it).

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~TapForManaTests" -v n
```

Expected: Build errors.

**Step 3:** Modify `GameEngine.cs`:
1. Change `ExecuteAction` from `internal void` to `internal async Task`, add `CancellationToken ct = default` parameter.
2. Replace the `TapCard` case with mana production logic.
3. Update the call in `RunPriorityAsync` from `ExecuteAction(action)` to `await ExecuteAction(action, ct)`.

New TapCard case:

```csharp
case ActionType.TapCard:
    var tapTarget = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
    if (tapTarget != null && !tapTarget.IsTapped)
    {
        tapTarget.IsTapped = true;
        player.ActionHistory.Push(action);

        if (tapTarget.ManaAbility != null)
        {
            var ability = tapTarget.ManaAbility;
            if (ability.Type == ManaAbilityType.Fixed)
            {
                player.ManaPool.Add(ability.FixedColor!.Value);
                _state.Log($"{player.Name} taps {tapTarget.Name} for {ability.FixedColor}.");
            }
            else if (ability.Type == ManaAbilityType.Choice)
            {
                var chosen = await player.DecisionHandler.ChooseManaColor(
                    ability.ChoiceColors!, ct);
                player.ManaPool.Add(chosen);
                _state.Log($"{player.Name} taps {tapTarget.Name} for {chosen}.");
            }
        }
        else
        {
            _state.Log($"{player.Name} taps {tapTarget.Name}.");
        }
    }
    break;
```

Add the necessary `using` at top of GameEngine.cs:

```csharp
using MtgDecker.Engine.Mana;
```

**Step 4:** Run tests — all 8 pass.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~TapForManaTests" -v n
```

Expected: `Passed! - Failed: 0, Passed: 8, Skipped: 0`

**Step 5:** Run full test suite.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ -v n
```

Expected: All tests pass. Fix any existing tests broken by the async signature change.

**Step 6:** Commit.

```bash
git add tests/MtgDecker.Engine.Tests/TapForManaTests.cs src/MtgDecker.Engine/GameEngine.cs
git commit -m "feat(engine): tap-for-mana produces mana from ManaAbility, ExecuteAction now async"
```

---

### Task 12: GameEngine — CastSpell Flow + Land Drop Enforcement

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs`
- Create: `tests/MtgDecker.Engine.Tests/CastSpellTests.cs`

This is the largest task. Three parts: land drops (A), casting (B), sandbox fallback (C).

**Step 1:** Write all 15 tests.

```csharp
// tests/MtgDecker.Engine.Tests/CastSpellTests.cs
using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class CastSpellTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler handler) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);

        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }

        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1);
    }

    // --- Part A: Land Drops ---

    [Fact]
    public async Task PlayLand_MovesToBattlefield()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        state.Player1.Hand.Add(forest);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, forest.Id));

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == forest.Id);
        state.Player1.Hand.Cards.Should().NotContain(c => c.Id == forest.Id);
    }

    [Fact]
    public async Task PlayLand_IncrementsLandsPlayed()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        state.Player1.Hand.Add(forest);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, forest.Id));

        state.Player1.LandsPlayedThisTurn.Should().Be(1);
    }

    [Fact]
    public async Task PlaySecondLand_Rejected()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var forest1 = GameCard.Create("Forest", "Basic Land — Forest");
        var forest2 = GameCard.Create("Forest", "Basic Land — Forest");
        state.Player1.Hand.Add(forest1);
        state.Player1.Hand.Add(forest2);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, forest1.Id));
        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, forest2.Id));

        state.Player1.Battlefield.Cards.Where(c => c.Name == "Forest").Should().HaveCount(1);
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == forest2.Id);
    }

    [Fact]
    public async Task PlayLand_NoManaDeducted()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        state.Player1.ManaPool.Add(ManaColor.Green, 3);
        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        state.Player1.Hand.Add(forest);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, forest.Id));

        state.Player1.ManaPool.Total.Should().Be(3, "playing a land should not cost mana");
    }

    [Fact]
    public async Task PlayLand_Logs()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        state.Player1.Hand.Add(forest);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, forest.Id));

        state.GameLog.Should().Contain(m => m.Contains("land drop"));
    }

    // --- Part B: Casting Spells ---

    [Fact]
    public async Task CastSpell_DeductsManaCost()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var goblin = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        state.Player1.Hand.Add(goblin);
        state.Player1.ManaPool.Add(ManaColor.Red, 1);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, goblin.Id));

        state.Player1.ManaPool.Total.Should().Be(0);
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == goblin.Id);
    }

    [Fact]
    public async Task CastSpell_InsufficientMana_Rejected()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var goblin = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        state.Player1.Hand.Add(goblin);
        // No mana in pool

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, goblin.Id));

        state.Player1.Hand.Cards.Should().Contain(c => c.Id == goblin.Id);
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == goblin.Id);
    }

    [Fact]
    public async Task CastSpell_Creature_GoesToBattlefield()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var goblin = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        state.Player1.Hand.Add(goblin);
        state.Player1.ManaPool.Add(ManaColor.Red, 1);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, goblin.Id));

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == goblin.Id);
    }

    [Fact]
    public async Task CastSpell_Instant_GoesToGraveyard()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var swords = GameCard.Create("Swords to Plowshares", "Instant");
        state.Player1.Hand.Add(swords);
        state.Player1.ManaPool.Add(ManaColor.White, 1);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, swords.Id));

        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == swords.Id);
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == swords.Id);
    }

    [Fact]
    public async Task CastSpell_Sorcery_GoesToGraveyard()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var replenish = GameCard.Create("Replenish", "Sorcery");
        state.Player1.Hand.Add(replenish);
        state.Player1.ManaPool.Add(ManaColor.White, 4); // {3}{W} — need 1W + 3 generic

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, replenish.Id));

        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == replenish.Id);
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == replenish.Id);
    }

    [Fact]
    public async Task CastSpell_AmbiguousGeneric_PromptsPlayer()
    {
        var (engine, state, handler) = CreateSetup();
        await engine.StartGameAsync();

        // Goblin Piledriver: {1}{R} — need 1R + 1 generic
        var piledriver = GameCard.Create("Goblin Piledriver", "Creature — Goblin");
        state.Player1.Hand.Add(piledriver);
        state.Player1.ManaPool.Add(ManaColor.Red, 2);
        state.Player1.ManaPool.Add(ManaColor.Green, 1);
        // After paying {R}, pool has R=1 G=1 — ambiguous for generic {1}

        handler.EnqueueGenericPayment(new Dictionary<ManaColor, int> { { ManaColor.Green, 1 } });

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, piledriver.Id));

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == piledriver.Id);
        state.Player1.ManaPool[ManaColor.Red].Should().Be(1);
        state.Player1.ManaPool[ManaColor.Green].Should().Be(0);
    }

    [Fact]
    public async Task CastSpell_UnambiguousGeneric_AutoPays()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        // Goblin Piledriver: {1}{R} — give exactly {R}{R}: after color, only R=1 left
        var piledriver = GameCard.Create("Goblin Piledriver", "Creature — Goblin");
        state.Player1.Hand.Add(piledriver);
        state.Player1.ManaPool.Add(ManaColor.Red, 2);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, piledriver.Id));

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == piledriver.Id);
        state.Player1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task CastSpell_Logs()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var goblin = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        state.Player1.Hand.Add(goblin);
        state.Player1.ManaPool.Add(ManaColor.Red, 1);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, goblin.Id));

        state.GameLog.Should().Contain(m => m.Contains("casts"));
    }

    // --- Part C: Sandbox Fallback ---

    [Fact]
    public async Task SandboxCard_NoManaCost_PlaysFreely()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var widget = new GameCard { Name = "Unknown Widget" };
        state.Player1.Hand.Add(widget);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, widget.Id));

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == widget.Id);
        state.Player1.Hand.Cards.Should().NotContain(c => c.Id == widget.Id);
    }

    [Fact]
    public async Task SandboxCard_GoesToBattlefield()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var widget = new GameCard { Name = "Unknown Widget" };
        state.Player1.Hand.Add(widget);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, widget.Id));

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == widget.Id);
    }
}
```

**Step 2:** Run tests — expect failures.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~CastSpellTests" -v n
```

Expected: Multiple failures.

**Step 3:** Replace the `PlayCard` case in `GameEngine.ExecuteAction` with the full land/cast/sandbox logic:

```csharp
case ActionType.PlayCard:
    var playCard = player.Hand.Cards.FirstOrDefault(c => c.Id == action.CardId);
    if (playCard == null) break;

    if (playCard.IsLand)
    {
        // Part A: Land drop enforcement
        if (player.LandsPlayedThisTurn >= 1)
        {
            _state.Log($"{player.Name} cannot play another land this turn.");
            break;
        }
        player.Hand.RemoveById(playCard.Id);
        player.Battlefield.Add(playCard);
        player.LandsPlayedThisTurn++;
        _state.Log($"{player.Name} plays {playCard.Name} (land drop).");
    }
    else if (playCard.ManaCost != null)
    {
        // Part B: Cast spell with mana payment
        if (!player.ManaPool.CanPay(playCard.ManaCost))
        {
            _state.Log($"{player.Name} cannot cast {playCard.Name} — not enough mana.");
            break;
        }

        var cost = playCard.ManaCost;

        // Calculate remaining pool after colored requirements
        var remaining = new Dictionary<ManaColor, int>();
        foreach (var kvp in player.ManaPool.Available)
        {
            var after = kvp.Value;
            if (cost.ColorRequirements.TryGetValue(kvp.Key, out var needed))
                after -= needed;
            if (after > 0)
                remaining[kvp.Key] = after;
        }

        // Deduct colored requirements
        foreach (var (color, required) in cost.ColorRequirements)
            player.ManaPool.Deduct(color, required);

        // Handle generic cost
        if (cost.GenericCost > 0)
        {
            int distinctColors = remaining.Count(kv => kv.Value > 0);
            int totalRemaining = remaining.Values.Sum();

            if (distinctColors <= 1 || totalRemaining == cost.GenericCost)
            {
                // Unambiguous: auto-pay
                var toPay = cost.GenericCost;
                foreach (var (color, amount) in remaining)
                {
                    var take = Math.Min(amount, toPay);
                    if (take > 0)
                    {
                        player.ManaPool.Deduct(color, take);
                        toPay -= take;
                    }
                    if (toPay == 0) break;
                }
            }
            else
            {
                // Ambiguous: prompt player
                var genericPayment = await player.DecisionHandler
                    .ChooseGenericPayment(cost.GenericCost, remaining, ct);
                foreach (var (color, amount) in genericPayment)
                    player.ManaPool.Deduct(color, amount);
            }
        }

        // Move card to destination
        player.Hand.RemoveById(playCard.Id);
        bool isInstantOrSorcery = playCard.CardTypes.HasFlag(CardType.Instant)
                                || playCard.CardTypes.HasFlag(CardType.Sorcery);
        if (isInstantOrSorcery)
        {
            player.Graveyard.Add(playCard);
            _state.Log($"{player.Name} casts {playCard.Name} (→ graveyard).");
        }
        else
        {
            player.Battlefield.Add(playCard);
            _state.Log($"{player.Name} casts {playCard.Name}.");
        }
        player.ActionHistory.Push(action);
    }
    else
    {
        // Part C: Sandbox — no ManaCost, not a land
        player.Hand.RemoveById(playCard.Id);
        player.Battlefield.Add(playCard);
        player.ActionHistory.Push(action);
        _state.Log($"{player.Name} plays {playCard.Name}.");
    }
    break;
```

**Step 4:** Run cast spell tests — all 15 pass.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ --filter "FullyQualifiedName~CastSpellTests" -v n
```

Expected: `Passed! - Failed: 0, Passed: 15, Skipped: 0`

**Step 5:** Run full test suite. Existing tests that used `PlayCard` with cards that match registry names (like "Mountain" or "Forest") may now hit land-drop or mana-check logic. Fix any regressions by either giving tests unregistered card names or adding required mana to pool.

```bash
export PATH="/c/Program Files/dotnet:$PATH"
dotnet test tests/MtgDecker.Engine.Tests/ -v n
```

Expected: All tests pass after any regression fixes.

**Step 6:** Commit.

```bash
git add tests/MtgDecker.Engine.Tests/CastSpellTests.cs src/MtgDecker.Engine/GameEngine.cs
git commit -m "feat(engine): land drop enforcement, cast spell with mana payment, sandbox fallback"
```

---

## Implementation Notes

**Task dependencies:** Tasks 1-5 are independent foundation types. Task 6 depends on 1-5. Task 7 depends on 6. Task 8 depends on 4. Task 9 depends on 1. Tasks 10-12 depend on all prior tasks.

**Recommended execution order:** Sequential 1→12. Tasks 1-2 can be combined into one commit. Tasks 4-5 are independent of 3 and can be parallelized.

**Regression risk areas:**
- Task 11 changes `ExecuteAction` from `void` to `async Task` — all callers and tests that call it need updating.
- Task 12 changes `PlayCard` behavior — existing tests using `PlayCard` with registered card names will now require mana in pool or need to use unregistered names.
- `DeckBuilder.AddLand("Mountain", n)` creates cards with TypeLine containing "Land" which triggers `IsLand`. If these tests use `PlayCard`, they now hit land-drop logic.

**Total new tests:** ~80 (12+15+6+11+8+4+7+3+8+15 minus overlaps)

**Total new/modified files:**
- 7 new source files
- 3 modified source files
- 8 new test files
