# Tier 1 Mechanics & New Decks — Design Document

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement three core spell mechanics (damage, card draw, counterspells) and register two new Legacy decks (Burn, UR Delver) that showcase them.

**Architecture:** Extend the existing SpellEffect system with 6 new effect classes. Add player targeting and stack targeting to the engine. Register ~30 new cards across 2 decks.

**Tech Stack:** MtgDecker.Engine (C# 14, .NET 10), xUnit + FluentAssertions

---

## Overview

### Current State
- 38 cards registered across 2 Legacy decks (Goblins, Enchantress)
- 2 spell effects (SwordsToPlowsharesEffect, NaturalizeEffect)
- 3 trigger effects (SearchLibrary, RevealAndFilter, CreateTokens)
- Stack mechanic with targeting for battlefield creatures
- Sandbox fallback for unregistered cards

### What We're Adding
1. **Damage spells** — Deal N damage to creatures or players
2. **Card draw spells** — Simple draw, Brainstorm (draw 3 / put 2 back), Ponder (look 3 / shuffle or keep / draw 1), Preordain (scry 2 / draw 1)
3. **Counterspells** — Counter target spell on the stack
4. **Burn deck** — ~15 unique cards, primarily damage spells
5. **UR Delver deck** — ~18 unique cards, card draw + counters + damage

---

## Engine Changes

### 1. Player Targeting

**Problem:** TargetInfo currently only targets cards (CardId, PlayerId, Zone). Damage spells need to target players.

**Solution:** Convention-based — `TargetInfo(Guid.Empty, playerId, ZoneType.None)` means "targeting the player directly."

**Changes:**
- `TargetFilter` — Add factory methods:
  - `TargetFilter.CreatureOrPlayer()` — for Lightning Bolt etc.
  - `TargetFilter.Player()` — for Lava Spike (player-only)
- `TargetFilter.Matches()` — needs to handle player targets (Guid.Empty CardId)
- `IPlayerDecisionHandler.ChooseTarget()` — decision handler presents "Target Player" button alongside creature list
- `InteractiveDecisionHandler` — target picker UI shows player option
- `TestDecisionHandler` — support enqueueing player-target TargetInfo

### 2. Damage Resolution & State-Based Actions

**Problem:** Damage is marked on creatures (`GameCard.DamageMarked`) but nothing checks lethality.

**Solution:** Add state-based action checks after spell/effect resolution.

**Changes:**
- `GameEngine` — Add `CheckStateBasedActions()` method:
  - Creatures with `DamageMarked >= Toughness` → move to graveyard
  - Players with `Life <= 0` → lose game (log, don't enforce yet)
  - Called after every spell resolution and combat damage
- `DamageEffect` — New SpellEffect subclass:
  - Constructor: `DamageEffect(int amount, bool canTargetCreature = true, bool canTargetPlayer = true)`
  - Resolve: If target is creature → add to DamageMarked. If target is player → reduce Life.
  - After resolving, engine calls CheckStateBasedActions()

### 3. Stack Targeting (Counterspells)

**Problem:** TargetFilter and ChooseTarget only work for battlefield cards. Counterspells target spells on the stack.

**Solution:** Extend targeting to support stack objects.

**Changes:**
- `TargetFilter` — Add `TargetFilter.Spell()` factory:
  - Matches: StackObjects on the stack (not the counterspell itself)
  - Predicate filters stack objects instead of battlefield cards
- `TargetInfo` — When Zone is `ZoneType.Stack`, CardId refers to the stack object's card ID
- `GameEngine.CastSpell` — When spell has `TargetFilter.Spell()`, present stack objects as targets
- `CounterSpellEffect` — New SpellEffect:
  - On resolve: find target on stack, remove it, move card to graveyard
  - If target already resolved/left stack → fizzle
- `IPlayerDecisionHandler.ChooseTarget()` — already generic enough, just needs stack items passed as eligible targets
- UI: target picker shows stack items when targeting spells

### 4. Library Top Manipulation

**Problem:** Brainstorm needs to put cards on TOP of library. Currently only `Zone.AddToBottom()` exists.

**Changes:**
- `Zone` — Add `AddToTop(GameCard card)` method (insert at index 0, or end depending on convention)
- Verify `Zone` ordering: index 0 = top of library (drawn first)
- Used by BrainstormEffect to put 2 cards back on top

### 5. Choose Cards From Hand

**Problem:** Brainstorm needs "choose 2 cards from your hand to put back." Current ChooseCard picks from a provided list, but we need multi-card selection.

**Solution:** Reuse `ChooseCard` called multiple times with updated hand state, OR add a new `ChooseMultipleCards` method.

**Preferred approach:** Call `ChooseCard` twice for Brainstorm (simpler, no new interface method):
1. First call: "Choose a card to put on top of your library (1/2)" — shows hand
2. Second call: "Choose a card to put on top of your library (2/2)" — shows hand minus first choice

---

## New SpellEffect Classes

### DamageEffect
```csharp
public class DamageEffect : SpellEffect
{
    public int Amount { get; }
    public bool CanTargetCreature { get; }
    public bool CanTargetPlayer { get; }

    public override void Resolve(GameState state, StackObject spell)
    {
        var target = spell.Targets[0];
        if (target.CardId == Guid.Empty)
        {
            // Player target — reduce life
            var player = state.GetPlayer(target.PlayerId);
            player.Life -= Amount;
        }
        else
        {
            // Creature target — mark damage
            var card = state.FindCard(target.CardId, target.Zone);
            card.DamageMarked += Amount;
        }
        // Move spell to graveyard
        // State-based actions checked by engine after resolution
    }
}
```

### CounterSpellEffect
```csharp
public class CounterSpellEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        var target = spell.Targets[0];
        var targetSpell = state.Stack.FirstOrDefault(s => s.Card.Id == target.CardId);
        if (targetSpell != null)
        {
            state.Stack.Remove(targetSpell);
            // Move countered card to owner's graveyard
            state.GetPlayer(targetSpell.ControllerId).Graveyard.Add(targetSpell.Card);
        }
        // Move counterspell to graveyard
    }
}
```

### BrainstormEffect
```csharp
public class BrainstormEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct)
    {
        var player = state.GetPlayer(spell.ControllerId);

        // Draw 3
        for (int i = 0; i < 3; i++)
            player.DrawCard();

        // Put 2 back on top (one at a time via ChooseCard)
        for (int i = 0; i < 2; i++)
        {
            var cardId = await handler.ChooseCard(
                player.Hand.Cards, $"Put a card on top of library ({i+1}/2)",
                optional: false, ct);
            var card = player.Hand.Remove(cardId.Value);
            player.Library.AddToTop(card);
        }
    }
}
```

### PonderEffect
```csharp
public class PonderEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct)
    {
        var player = state.GetPlayer(spell.ControllerId);

        // Reveal top 3
        var top3 = player.Library.PeekTop(3);

        // Show cards and ask: shuffle or keep?
        // Use RevealCards to show, then ChooseCard with "Shuffle library?" prompt
        await handler.RevealCards(top3, top3, "Top 3 cards of your library", ct);

        // Ask shuffle decision (use ChooseCard with null = shuffle, card = keep order)
        var shuffleChoice = await handler.ChooseCard(
            Array.Empty<GameCard>(), "Shuffle your library?",
            optional: true, ct); // null = yes shuffle, any = no

        if (shuffleChoice == null)
        {
            player.Library.Shuffle();
        }
        // else: cards stay in current order

        // Draw 1
        player.DrawCard();
    }
}
```

### PreordainEffect
```csharp
public class PreordainEffect : SpellEffect
{
    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct)
    {
        var player = state.GetPlayer(spell.ControllerId);

        // Look at top 2
        var top2 = player.Library.PeekTop(2);

        // For each: keep on top or put to bottom
        // Show all, let player choose which to keep (optional = can skip to bottom all)
        await handler.RevealCards(top2, Array.Empty<GameCard>(),
            "Scry 2: choose cards to keep on top", ct);

        // Choose cards to keep on top (0, 1, or 2)
        foreach (var card in top2)
        {
            var keep = await handler.ChooseCard(
                new[] { card }, $"Keep {card.Name} on top?",
                optional: true, ct);
            if (keep == null)
            {
                player.Library.Remove(card);
                player.Library.AddToBottom(card);
            }
        }

        // Draw 1
        player.DrawCard();
    }
}
```

**Note:** BrainstormEffect, PonderEffect, and PreordainEffect need async resolution. The current `SpellEffect.Resolve()` is synchronous. We need to add an async variant:

```csharp
public abstract class SpellEffect
{
    // Existing sync resolve for simple effects
    public virtual void Resolve(GameState state, StackObject spell) { }

    // New async resolve for interactive effects (draw spells)
    public virtual Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct)
    {
        Resolve(state, spell);
        return Task.CompletedTask;
    }

    public bool IsAsync { get; protected init; }
}
```

The engine calls `ResolveAsync` for all effects — sync effects fall through to the sync `Resolve` via the default implementation.

---

## Card Definitions

### Burn Deck

```
Lightning Bolt      {R}       Instant   DamageEffect(3, creature+player)
Chain Lightning      {R}       Sorcery   DamageEffect(3, creature+player)
Lava Spike          {R}       Sorcery   DamageEffect(3, playerOnly)
Rift Bolt           {1}{R}    Sorcery   DamageEffect(3, creature+player)  [suspend deferred]
Fireblast           {4}{R}{R} Instant   DamageEffect(4, creature+player)  [alt cost deferred]
Goblin Guide        {R}       Creature 2/2                                [haste deferred]
Monastery Swiftspear {R}      Creature 1/2                                [haste+prowess deferred]
Eidolon of Great Revel {R}{R} Creature 2/2 Enchantment                   [trigger deferred]
Flame Rift          {1}{R}    Sorcery   DamageAllPlayersEffect(4)         [deals 4 to each player]
Searing Blood       {R}{R}    Instant   DamageEffect(2, creatureOnly)     [simplified from Searing Blaze]
Mountain            -         Land      ManaAbility.Fixed(Red)            [already registered]
```

### UR Delver Deck

```
Brainstorm          {U}       Instant   BrainstormEffect
Ponder              {U}       Sorcery   PonderEffect
Preordain           {U}       Sorcery   PreordainEffect
Lightning Bolt      {R}       Instant   (shared with Burn)
Counterspell        {U}{U}    Instant   CounterSpellEffect                TargetFilter.Spell()
Daze                {1}{U}    Instant   CounterSpellEffect                [alt cost deferred]
Force of Will       {3}{U}{U} Instant   CounterSpellEffect                [alt cost deferred]
Delver of Secrets   {U}       Creature 1/1                                [flip deferred]
Murktide Regent     {5}{U}{U} Creature 3/3                                [delve deferred]
Dragon's Rage Channeler {R}   Creature 1/1                                [surveil deferred]
Volcanic Island     -         Land      ManaAbility.Choice(Blue, Red)
Scalding Tarn       -         Land      (no mana ability)                  [fetch deferred]
Island              -         Land      ManaAbility.Fixed(Blue)
Mountain            -         Land      (shared)
Wasteland           -         Land      (already registered, no ability)
Mystic Sanctuary    -         Land      ManaAbility.Fixed(Blue)           [ETB trigger deferred]
```

---

## Decision Handler Changes

### ChooseTarget (extended)

When a spell has `TargetFilter.Spell()`, the eligible targets are stack objects (not battlefield cards). The handler is already generic — we just need to pass the right list.

For player targeting (`CreatureOrPlayer`), the UI needs to show:
- Each eligible creature as a button (existing)
- "Target [PlayerName]" buttons for each eligible player (new)

### No New Interface Methods Needed

All card draw effects can be implemented using existing handler methods:
- `ChooseCard` — for Brainstorm put-back, Ponder keep/bottom, Preordain scry decisions
- `RevealCards` — for showing top cards (Ponder, Preordain)
- Draw is engine-internal (no player decision needed)

---

## State-Based Actions

New `CheckStateBasedActions()` method on `GameEngine`, called after:
- Every spell/effect resolution
- Combat damage assignment
- Any life total change

Checks:
1. **Creature lethality**: `DamageMarked >= Toughness` → destroy (move to graveyard, fire Dies trigger)
2. **Player life**: `Life <= 0` → mark as lost (log for now)
3. **Zero toughness**: Creature with 0 toughness → destroy

Damage marked on creatures clears at end of turn (cleanup step) — needs end-of-turn hook.

---

## Testing Strategy

Each new effect gets:
1. Unit tests for the effect class (resolve with mock state)
2. Integration tests via GameEngine (cast spell, verify resolution)
3. Edge cases: fizzle (target removed), counter a counter, damage to lethal

Estimated: ~40-60 new engine tests.

---

## Scope Boundaries (Deferred)

These are explicitly NOT in scope:
- **Alternate costs**: Force of Will pitch, Daze bounce, Fireblast sacrifice
- **Haste/Keywords**: Goblin Guide, Swiftspear
- **Prowess**: Monastery Swiftspear
- **Suspend**: Rift Bolt
- **Flip/Transform**: Delver of Secrets
- **Delve**: Murktide Regent
- **Fetch lands**: Scalding Tarn, Wooded Foothills
- **Pump effects**: Deferred to Tier 2 (Infect deck)

These cards are registered with correct mana costs and stat blocks so they're playable — their special abilities just don't trigger.
