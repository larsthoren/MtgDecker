# Continuous Effects, Legendary Rule, Fetch Lands Design

## Goal

Add four interconnected engine features that unlock full mechanical support for many cards in the two starter decks: a unified continuous effect system (lord buffs, cost reduction, keyword grants, extra land drops), the legendary rule, fetch lands, and haste.

## Architecture

All changes live in `MtgDecker.Engine` — no web/UI dependency for the core mechanics. The continuous effect system is the backbone: a single recalculation pass after every board state change applies P/T modifiers, keywords, cost reductions, and land drop bonuses. State-based actions (legendary rule, life check) run immediately after recalculation.

## Components

### 1. ContinuousEffect System

#### New Types

```csharp
public enum ContinuousEffectType
{
    ModifyPowerToughness,
    GrantKeyword,
    ModifyCost,
    ExtraLandDrop,
}

public enum Keyword { Haste, Shroud, Mountainwalk }

public record ContinuousEffect(
    Guid SourceId,
    ContinuousEffectType Type,
    Func<GameCard, Player, bool> Applies,
    int PowerMod = 0,
    int ToughnessMod = 0,
    bool UntilEndOfTurn = false,
    Keyword? GrantedKeyword = null,
    int CostMod = 0,
    Func<GameCard, bool>? CostApplies = null,
    int ExtraLandDrops = 0);
```

#### CardDefinition Extension

```csharp
public IReadOnlyList<ContinuousEffect> ContinuousEffects { get; init; } = [];
```

Effects are registered with `SourceId = Guid.Empty` in the definition. When a card enters the battlefield, effects are stamped with the actual `GameCard.Id` and added to `GameState.ActiveEffects`.

#### GameState Extension

```csharp
public List<ContinuousEffect> ActiveEffects { get; } = new();
```

#### RecalculateState() on GameEngine

Called after every board state change. Steps:

1. **Rebuild ActiveEffects** — scan both players' battlefields, collect all `ContinuousEffect` entries from registered `CardDefinition`s, stamp with actual card IDs.
2. **P/T modifiers** — for each creature on the battlefield, set `EffectivePower = BasePower + sum of applicable ModifyPowerToughness effects` (same for toughness). Lord effects exclude their own source via `card.Id != effect.SourceId`.
3. **Keywords** — for each card, rebuild `ActiveKeywords` HashSet from applicable GrantKeyword effects.
4. **Land drops** — for each player, set `MaxLandDrops = 1 + sum of applicable ExtraLandDrop effects` where the player controls the source.
5. **Strip UntilEndOfTurn** — called at end-of-turn cleanup; remove expired effects and recalculate.

#### Deck Examples

```csharp
// Goblin King: Other Goblins get +1/+1
ContinuousEffects = [new ContinuousEffect(
    Guid.Empty, ContinuousEffectType.ModifyPowerToughness,
    (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
    PowerMod: 1, ToughnessMod: 1)]
// Note: lord "other" filter uses card.Id != effect.SourceId during evaluation

// Exploration: You may play an additional land each turn
ContinuousEffects = [new ContinuousEffect(
    Guid.Empty, ContinuousEffectType.ExtraLandDrop,
    (_, _) => true, ExtraLandDrops: 1)]

// Goblin Warchief: Goblin spells cost {1} less + Goblins have haste
ContinuousEffects = [
    new ContinuousEffect(Guid.Empty, ContinuousEffectType.ModifyCost,
        (_, _) => true, CostMod: -1,
        CostApplies: c => c.Subtypes.Contains("Goblin")),
    new ContinuousEffect(Guid.Empty, ContinuousEffectType.GrantKeyword,
        (card, _) => card.IsCreature && card.Subtypes.Contains("Goblin"),
        GrantedKeyword: Keyword.Haste)]
```

### 2. GameCard Changes

```csharp
// Rename existing Power/Toughness to base values
public int? BasePower { get; set; }
public int? BaseToughness { get; set; }

// Computed effective values (set by RecalculateState)
public int? EffectivePower { get; set; }
public int? EffectiveToughness { get; set; }

// Convenience — combat, display, and death checks use Effective
public int? Power => EffectivePower ?? BasePower;
public int? Toughness => EffectiveToughness ?? BaseToughness;

// New properties
public bool IsLegendary { get; init; }
public HashSet<Keyword> ActiveKeywords { get; } = new();

// Haste-aware summoning sickness
public bool HasSummoningSickness(int currentTurn) =>
    IsCreature
    && TurnEnteredBattlefield.HasValue
    && TurnEnteredBattlefield.Value >= currentTurn
    && !ActiveKeywords.Contains(Keyword.Haste);
```

All existing code that reads `Power`/`Toughness` (combat damage, death checks, display, board evaluator) continues to work unchanged because the computed properties return effective values.

### 3. Extra Land Drops (Exploration)

**Player extension:**

```csharp
public int MaxLandDrops { get; set; } = 1;
```

**GameEngine change** in `ExecuteAction` land drop check:

```csharp
// Before (hardcoded):
if (player.LandsPlayedThisTurn >= 1)

// After:
if (player.LandsPlayedThisTurn >= player.MaxLandDrops)
```

`RecalculateState()` resets `MaxLandDrops = 1 + sum of ExtraLandDrop effects` for each player. Also reset to 1 at start of turn before recalculation.

### 4. Legendary Rule

MTG rule 704.5j: If a player controls two or more legendary permanents with the same name, they choose one to keep and the rest go to the graveyard.

**CardDefinition extension:**

```csharp
public bool IsLegendary { get; init; }
```

Forwarded to `GameCard.IsLegendary` during `Create()`. Auto-detected from `TypeLine` containing "Legendary" for cards not in the registry.

**State-based action** — added to `CheckStateBasedActions()` (which becomes async):

```csharp
async Task CheckLegendaryRuleAsync(Player player, CancellationToken ct)
{
    var legendaries = player.Battlefield.Cards
        .Where(c => c.IsLegendary)
        .GroupBy(c => c.Name)
        .Where(g => g.Count() > 1);

    foreach (var group in legendaries)
    {
        var duplicates = group.ToList();
        var chosenId = await player.DecisionHandler.ChooseCard(
            duplicates,
            $"Choose which {duplicates[0].Name} to keep (legendary rule)",
            optional: false, ct);

        foreach (var card in duplicates.Where(c => c.Id != chosenId))
        {
            player.Battlefield.RemoveById(card.Id);
            player.Graveyard.Add(card);
            _state.Log($"{card.Name} is put into graveyard (legendary rule).");
        }
    }
}
```

Uses existing `ChooseCard` on `IPlayerDecisionHandler` — no new interface method. AI bot picks highest CMC (existing behavior). Interactive handler shows card selection dialog.

### 5. Fetch Lands

Fetch lands (Wooded Foothills, Windswept Heath) are activated abilities: pay 1 life, sacrifice self, search library for a land with a matching basic land type, put it onto the battlefield, shuffle.

#### FetchAbility Type

```csharp
public record FetchAbility(IReadOnlyList<string> SearchTypes);
```

```csharp
// CardDefinition
public FetchAbility? FetchAbility { get; init; }
```

#### Registration

```csharp
["Wooded Foothills"] = new(null, null, null, null, CardType.Land)
    { FetchAbility = new FetchAbility(["Mountain", "Forest"]) },
["Windswept Heath"] = new(null, null, null, null, CardType.Land)
    { FetchAbility = new FetchAbility(["Plains", "Forest"]) },
```

#### Basic Land Subtypes

Basic lands need subtypes so fetch can find them:

```csharp
["Mountain"] = new(...) { Subtypes = ["Mountain"] },
["Forest"] = new(...) { Subtypes = ["Forest"] },
["Plains"] = new(...) { Subtypes = ["Plains"] },
```

Dual lands (Karplusan Forest, Brushland) don't have basic subtypes — not fetchable. Correct per MTG rules.

#### New ActionType

```csharp
ActivateFetch  // added to ActionType enum
```

#### Engine Handling

```csharp
case ActionType.ActivateFetch:
    var fetchLand = player.Battlefield.Cards.FirstOrDefault(c => c.Id == action.CardId);
    if (fetchLand == null) break;
    if (!CardDefinitions.TryGet(fetchLand.Name, out var fetchDef)
        || fetchDef.FetchAbility == null) break;

    // Pay costs: 1 life + sacrifice
    player.AdjustLife(-1);
    player.Battlefield.RemoveById(fetchLand.Id);
    player.Graveyard.Add(fetchLand);

    // Search library for matching land
    var searchTypes = fetchDef.FetchAbility.SearchTypes;
    var eligible = player.Library.Cards
        .Where(c => c.IsLand && searchTypes.Any(t =>
            c.Subtypes.Contains(t) || c.Name.Equals(t, StringComparison.OrdinalIgnoreCase)))
        .ToList();

    if (eligible.Count > 0)
    {
        var chosenId = await player.DecisionHandler.ChooseCard(
            eligible, $"Search for a land ({string.Join(" or ", searchTypes)})",
            optional: true, ct);

        if (chosenId != null)
        {
            var land = player.Library.RemoveById(chosenId.Value);
            player.Battlefield.Add(land);
            land.TurnEnteredBattlefield = _state.TurnNumber;
            _state.Log($"{player.Name} fetches {land.Name}.");
            await ProcessTriggersAsync(GameEvent.EnterBattlefield, land, player, ct);
        }
    }

    player.Library.Shuffle();
    player.ActionHistory.Push(action);
    _state.Log($"{player.Name} sacrifices {fetchLand.Name}, searches library.");
    break;
```

### 6. OnBoardChanged — Central Wiring Point

A single method on `GameEngine` called after every board state mutation:

```csharp
async Task OnBoardChangedAsync(CancellationToken ct)
{
    RecalculateState();           // continuous effects, keywords, land drops
    await CheckStateBasedActionsAsync(ct);  // life, legendary rule
}
```

**Call sites:**
- After ETB (land drop, spell resolution, fetch land)
- After any permanent leaves (combat death, sacrifice, destroy, exile)
- After stack resolution (`ResolveTopOfStack`)
- After legendary rule removes duplicates (may cascade if removing a lord changes P/T causing deaths)

**End of turn:**
- Strip `UntilEndOfTurn` effects
- Call `RecalculateState()` to update P/T after stripping

### 7. Cost Modification During Casting

When casting a spell, compute effective cost before checking `CanPay`:

```csharp
var effectiveCost = manaCost;
var costReduction = state.ActiveEffects
    .Where(e => e.Type == ContinuousEffectType.ModifyCost && e.CostApplies?.Invoke(card) == true)
    .Sum(e => e.CostMod);

if (costReduction != 0)
{
    var newGeneric = Math.Max(0, manaCost.GenericCost + costReduction);
    effectiveCost = new ManaCost(newGeneric, manaCost.ColorRequirements);
}
```

This applies in both `ExecuteAction(PlayCard)` and `ExecuteAction(CastSpell)` paths.

### 8. AI Bot Updates

- **GetAction**: Check for untapped fetch lands before tapping regular lands. Activate fetch early to fix mana.
- **ChooseCard**: Existing logic (pick highest CMC) works for legendary rule and fetch search.
- **Combat**: Uses `Power`/`Toughness` properties which now return effective values — no change needed.

## Data Flow

```
Card enters/leaves battlefield
  → OnBoardChangedAsync()
    → RecalculateState()
      → Rebuild ActiveEffects from all battlefield permanents
      → Apply P/T modifiers (BasePower + mods = EffectivePower)
      → Rebuild ActiveKeywords per card
      → Recalculate MaxLandDrops per player
    → CheckStateBasedActionsAsync()
      → Life ≤ 0 check
      → Legendary rule (player chooses which to keep)
      → Lethal damage check
      → If any SBA fired, loop (may cascade)
```

## Cards Unlocked

| Card | Feature Used |
|------|-------------|
| Goblin King | ModifyPowerToughness (+1/+1 to Goblins) |
| Goblin Warchief | ModifyCost (-1 Goblin spells) + GrantKeyword (Haste) |
| Goblin Ringleader | Haste keyword (already has ETB trigger) |
| Exploration | ExtraLandDrop |
| Wooded Foothills | FetchAbility (Mountain/Forest) |
| Windswept Heath | FetchAbility (Plains/Forest) |
| Serra's Sanctum | IsLegendary (legendary rule) |

## Future Extensions

The continuous effect system is designed to grow:
- **Goblin Pyromancer**: `UntilEndOfTurn = true` P/T buff + delayed "destroy all Goblins" trigger
- **Sterling Grove**: `GrantKeyword(Keyword.Shroud)` to enchantments
- **Aura of Silence**: `ModifyCost` with positive `CostMod` targeting opponent
- **Opalescence**: Would need a new effect type (TypeChange) — future work

## Testing Strategy

- Unit tests for `RecalculateState()` with known board positions (lord enters, lord leaves, multiple lords stack)
- Unit tests for `HasSummoningSickness` with/without haste keyword
- Unit tests for cost reduction (Warchief reduces Goblin cost, doesn't reduce non-Goblin)
- Unit tests for extra land drops (Exploration allows 2, two Explorations allow 3)
- Unit tests for legendary rule (two copies, player chooses, loser goes to graveyard)
- Unit tests for fetch lands (sacrifice + life + search + shuffle + ETB triggers on fetched land)
- Integration test: Goblin King + 3 Goblins in combat, effective P/T used for damage
- Integration test: Warchief grants haste, creature attacks same turn
- Integration test: fetch land into Goblin King scenario (board recalculates after fetch ETB)

## File Structure

```
src/MtgDecker.Engine/
  ContinuousEffect.cs          (record + ContinuousEffectType + Keyword enums)
  FetchAbility.cs               (record)
  CardDefinition.cs             (add ContinuousEffects, IsLegendary, FetchAbility)
  GameCard.cs                   (BasePower/Toughness, Effective, IsLegendary, ActiveKeywords)
  GameEngine.cs                 (RecalculateState, OnBoardChanged, CheckLegendaryRule, ActivateFetch)
  GameState.cs                  (ActiveEffects list)
  Player.cs                     (MaxLandDrops)
  Enums/ActionType.cs           (add ActivateFetch)
  Enums/Keyword.cs              (new file)

tests/MtgDecker.Engine.Tests/
  ContinuousEffectTests.cs      (P/T mods, keywords, cost reduction, land drops)
  LegendaryRuleTests.cs         (duplicates, player choice, cascading)
  FetchLandTests.cs             (sacrifice, search, shuffle, ETB triggers)
  HasteTests.cs                 (summoning sickness bypass)
```
