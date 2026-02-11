# Trigger System & ETB Effects Design

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a generic trigger/effect system so cards can have abilities that fire when game events occur, starting with ETB (enter-the-battlefield) triggers for the Goblin and Enchantress starter decks.

**Architecture:** Event-driven trigger system. The engine fires `GameEvent` values when things happen (card enters battlefield, creature dies, spell cast). Cards register `Trigger` instances in `CardDefinitions`. The engine scans for matching triggers, creates `TriggeredAbility` instances, and resolves their `IEffect` implementations.

**Tech Stack:** MtgDecker.Engine (C# 14), MudBlazor dialogs for UI interaction.

---

## Core Types

### GameEvent enum
```csharp
public enum GameEvent
{
    EnterBattlefield,   // A permanent enters the battlefield
    LeavesBattlefield,  // A permanent leaves the battlefield
    Dies,               // A creature goes from battlefield to graveyard
    SpellCast,          // A spell is cast (before resolution)
    CombatDamageDealt,  // A creature deals combat damage
    DrawCard,           // A player draws a card
    Upkeep,             // Upkeep step begins
}
```
Start with `EnterBattlefield`. Others listed for future extensibility but not implemented yet.

### Trigger
```csharp
public record Trigger(
    GameEvent Event,
    TriggerCondition Condition,
    IEffect Effect
);

public enum TriggerCondition
{
    Self,              // Triggers when this card itself is the source (ETB)
    AnyCreatureDies,   // Triggers when any creature dies
    ControllerCasts,   // Triggers when controller casts a spell matching filter
}
```

### IEffect interface
```csharp
public interface IEffect
{
    Task Execute(EffectContext context, CancellationToken ct = default);
}

public record EffectContext(
    GameState State,
    Player Controller,
    GameCard Source,
    IPlayerDecisionHandler DecisionHandler
);
```

### TriggeredAbility (runtime instance)
```csharp
public class TriggeredAbility
{
    public GameCard Source { get; }
    public Player Controller { get; }
    public Trigger Trigger { get; }
}
```

## Subtypes

### ParsedTypeLine
`CardTypeParser.Parse` returns a new record instead of just `CardType`:

```csharp
public record ParsedTypeLine(CardType Types, IReadOnlyList<string> Subtypes);
```

Parsing: split `type_line` on em dash (`—`). Left side → card types (existing logic). Right side → split by whitespace → subtypes.

Examples:
- `"Creature — Goblin"` → `(Creature, ["Goblin"])`
- `"Legendary Creature — Goblin Warrior"` → `(Creature, ["Goblin", "Warrior"])`
- `"Enchantment — Aura"` → `(Enchantment, ["Aura"])`
- `"Basic Land — Mountain"` → `(Land, ["Mountain"])`

### GameCard.Subtypes
```csharp
public IReadOnlyList<string> Subtypes { get; init; } = [];
```
Auto-populated by `GameCard.Create` overload from `ParsedTypeLine`.

## Effect Implementations

### CreateTokensEffect (Siege-Gang Commander)
Creates N token GameCards on controller's battlefield.

```csharp
public class CreateTokensEffect(
    string name, int power, int toughness, CardType cardTypes,
    IReadOnlyList<string> subtypes, int count = 1) : IEffect
```

Token properties:
- `GameCard.IsToken = true` (new property)
- Created directly on battlefield with `TurnEnteredBattlefield = current turn`
- Have summoning sickness
- Work normally in combat
- When a token leaves the battlefield, it ceases to exist (removed from all zones)

### SearchLibraryEffect (Goblin Matron)
Search controller's library for a card matching a subtype filter, put to hand.

```csharp
public class SearchLibraryEffect(string subtype, bool optional = true) : IEffect
```

Flow:
1. Filter library for cards with matching subtype
2. Call `DecisionHandler.ChooseCard(matches, prompt, optional)` for player selection
3. If chosen: remove from library, add to hand
4. Shuffle library

### RevealAndFilterEffect (Goblin Ringleader)
Reveal top N cards, matching cards go to hand, rest to bottom.

```csharp
public class RevealAndFilterEffect(int count, string subtype) : IEffect
```

Flow:
1. Take top N cards from library
2. Split into matching (subtype match) and non-matching
3. Call `DecisionHandler.RevealCards(all, matching, prompt)` so player sees result
4. Matching → hand, non-matching → bottom of library

## Decision Handler Extensions

### IPlayerDecisionHandler
```csharp
Task<Guid?> ChooseCard(IReadOnlyList<GameCard> options, string prompt,
    bool optional = false, CancellationToken ct = default);

Task RevealCards(IReadOnlyList<GameCard> cards, IReadOnlyList<GameCard> kept,
    string prompt, CancellationToken ct = default);
```

### InteractiveDecisionHandler
- TCS-based, same pattern as combat decisions
- `IsWaitingForCardChoice`, `CardChoiceOptions`, `CardChoicePrompt`
- `IsWaitingForRevealAck`, `RevealedCards`, `KeptCards`, `RevealPrompt`
- `SubmitCardChoice(Guid? cardId)`, `AcknowledgeReveal()`

### TestDecisionHandler
- Queue-based: `EnqueueCardChoice(Guid?)`, `EnqueueRevealAck()`
- Default: choose first card if available, auto-acknowledge reveals

## UI Components

### CardSelectionDialog.razor
MudBlazor dialog with two modes:

**Selection mode** (tutor):
- Grid of card images
- Click to select, click again to deselect
- "Choose" button (disabled until selection made)
- "Skip" button if optional
- Title shows prompt text

**Reveal mode** (Ringleader):
- Grid of all revealed cards
- Kept cards have green border/highlight
- Non-kept cards are dimmed
- "OK" button to acknowledge
- Title shows prompt text

### PlayerZone integration
- When `IsWaitingForCardChoice` or `IsWaitingForRevealAck`, show the dialog
- Wire through EventCallbacks from GamePage → GameBoard → PlayerZone

## Engine Integration

### ProcessTriggersAsync
New method in `GameEngine`:

```csharp
private async Task ProcessTriggersAsync(GameEvent evt, GameCard source,
    Player controller, CancellationToken ct)
```

Called after:
- A card enters the battlefield (ETB)
- Future: after creature dies, after spell cast, etc.

Trigger resolution order: active player's triggers first, then non-active player (APNAP).

### Token Lifecycle
- `GameCard.IsToken` property (bool, default false)
- When tokens leave the battlefield (die, exile, bounce), they're removed from the game entirely
- `ProcessCombatDeaths` already moves dead creatures to graveyard — add token cleanup after
- New helper: `RemoveTokenFromGame(GameCard token)` — removes from any zone

### Library Operations
- `CardZone.Shuffle()` — Fisher-Yates shuffle
- `CardZone.PeekTop(int count)` — look at top N without removing
- `CardZone.RemoveById(Guid id)` — already exists
- `CardZone.AddToBottom(GameCard card)` — add to end of list

## CardDefinitions Updates

```csharp
["Siege-Gang Commander"] = new(ManaCost.Parse("{3}{R}{R}"), null, 2, 2, CardType.Creature)
{
    Subtypes = ["Goblin"],
    Triggers = [
        new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self,
            new CreateTokensEffect("Goblin", 1, 1, CardType.Creature, ["Goblin"], count: 3))
    ]
},

["Goblin Matron"] = new(ManaCost.Parse("{2}{R}"), null, 1, 1, CardType.Creature)
{
    Subtypes = ["Goblin"],
    Triggers = [
        new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self,
            new SearchLibraryEffect("Goblin", optional: true))
    ]
},

["Goblin Ringleader"] = new(ManaCost.Parse("{3}{R}"), null, 2, 2, CardType.Creature)
{
    Subtypes = ["Goblin"],
    Triggers = [
        new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self,
            new RevealAndFilterEffect(count: 4, subtype: "Goblin"))
    ]
},
```

All other cards in both decks get `Subtypes` added to their definitions.

## Scope Boundaries

**In scope:**
- Generic trigger system (GameEvent, Trigger, IEffect, TriggeredAbility)
- Subtypes parsing and GameCard property
- Three ETB effects: CreateTokens, SearchLibrary, RevealAndFilter
- Decision handler extensions (ChooseCard, RevealCards)
- CardSelectionDialog UI component
- Token creation and lifecycle
- Library operations (shuffle, peek, add-to-bottom)
- CardDefinitions updates for all starter deck cards (subtypes + triggers)

**Out of scope (future work):**
- Static abilities (haste, lord effects, cost reduction)
- Activated abilities (tap abilities, sacrifice abilities)
- Non-ETB triggers (dies, upkeep, combat damage)
- Enchantment-specific continuous effects (Wild Growth, Exploration, Opalescence)
- The stack (being built in parallel session)
