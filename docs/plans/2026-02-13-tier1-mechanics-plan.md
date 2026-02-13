# Tier 1 Mechanics Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement damage spells, card draw (Brainstorm/Ponder/Preordain), counterspells, and register Burn + UR Delver decks.

**Architecture:** Extend SpellEffect to async, add player/stack targeting, add state-based actions.

**Tech Stack:** C# 14, .NET 10, xUnit + FluentAssertions

---

### Task 1: Make SpellEffect Support Async Resolution

**Files:**
- Modify: `src/MtgDecker.Engine/Effects/SpellEffect.cs`
- Modify: `src/MtgDecker.Engine/Effects/SwordsToPlowsharesEffect.cs`
- Modify: `src/MtgDecker.Engine/Effects/NaturalizeEffect.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (ResolveTopOfStack method)
- Test: `tests/MtgDecker.Engine.Tests/Effects/SpellEffectTests.cs`

**Context:** Currently `SpellEffect.Resolve(GameState, StackObject)` is sync void. Brainstorm/Ponder need async for player decisions during resolution. Change to async virtual with sync fallback.

**Step 1: Write failing test**
```csharp
[Fact]
public async Task AsyncEffect_CanResolve_WithPlayerInteraction()
{
    // Test that an async spell effect can call decision handler during resolution
}
```

**Step 2: Modify SpellEffect base class**
```csharp
public abstract class SpellEffect
{
    public virtual void Resolve(GameState state, StackObject spell) { }

    public virtual Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct = default)
    {
        Resolve(state, spell);
        return Task.CompletedTask;
    }

    public virtual bool IsAsync => false;
}
```

**Step 3: Update GameEngine.ResolveTopOfStack to async**
- Change `ResolveTopOfStack()` to `async Task ResolveTopOfStackAsync()`
- Call `await def.Effect.ResolveAsync(state, spell, handler, ct)` instead of `def.Effect.Resolve(state, spell)`
- Pass the controller's `DecisionHandler` to the effect
- Update callers in RunPriorityAsync

**Step 4: Verify existing tests still pass**
Run: `dotnet test tests/MtgDecker.Engine.Tests/`

**Step 5: Commit**
```
feat(engine): make SpellEffect support async resolution
```

---

### Task 2: Add Zone Utility Methods

**Files:**
- Modify: `src/MtgDecker.Engine/Zone.cs`
- Test: `tests/MtgDecker.Engine.Tests/ZoneTests.cs`

**Context:** Brainstorm needs `AddToTop(card)` (which is logically the same as `Add` since top = end of list, but semantically clearer). Also need `Remove(GameCard)` for removing a specific card object.

**Step 1: Write failing tests**
```csharp
[Fact]
public void AddToTop_PlacesCardAtTopOfZone()
{
    var zone = new Zone(ZoneType.Library);
    zone.Add(card1);
    zone.AddToTop(card2);
    zone.PeekTop(1)[0].Should().Be(card2);
}

[Fact]
public void Remove_RemovesSpecificCard()
{
    var zone = new Zone(ZoneType.Hand);
    zone.Add(card1);
    zone.Add(card2);
    zone.Remove(card1).Should().BeTrue();
    zone.Count.Should().Be(1);
}
```

**Step 2: Implement**
```csharp
public void AddToTop(GameCard card) => _cards.Add(card); // Top = end of list

public bool Remove(GameCard card) => _cards.Remove(card);
```

**Step 3: Run tests, commit**
```
feat(engine): add Zone.AddToTop and Zone.Remove methods
```

---

### Task 3: Player Targeting

**Files:**
- Modify: `src/MtgDecker.Engine/TargetFilter.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (CastSpell targeting section)
- Modify: `src/MtgDecker.Engine/IPlayerDecisionHandler.cs` (ChooseTarget signature)
- Modify: `src/MtgDecker.Engine/InteractiveDecisionHandler.cs`
- Modify: `tests/MtgDecker.Engine.Tests/Helpers/TestDecisionHandler.cs`
- Test: `tests/MtgDecker.Engine.Tests/TargetFilterTests.cs`

**Context:** Damage spells target players, not just cards. Convention: `TargetInfo(Guid.Empty, playerId, ZoneType.None)` = player target.

**Step 1: Write failing tests**
```csharp
[Fact]
public void CreatureOrPlayer_MatchesCreatureOnBattlefield()
[Fact]
public void CreatureOrPlayer_MatchesPlayerTarget()
[Fact]
public void Player_DoesNotMatchCreature()
```

**Step 2: Add TargetFilter factories**
```csharp
public static TargetFilter CreatureOrPlayer() => new((card, zone) =>
    (zone == ZoneType.Battlefield && card.IsCreature) || zone == ZoneType.None);

public static TargetFilter Player() => new((card, zone) => zone == ZoneType.None);
```
Note: For player targets, we create a dummy "player" GameCard for the filter check with zone = None.

**Step 3: Update CastSpell in GameEngine**
When TargetFilter allows player targets (zone == None check), add player targets to eligible list before calling ChooseTarget. Create sentinel GameCard objects representing players.

**Step 4: Run tests, commit**
```
feat(engine): add player targeting support for damage spells
```

---

### Task 4: DamageEffect

**Files:**
- Create: `src/MtgDecker.Engine/Effects/DamageEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/Effects/DamageEffectTests.cs`

**Context:** "Deal N damage to target creature or player." If creature: add to DamageMarked. If player (Guid.Empty CardId): reduce Life.

**Step 1: Write failing tests**
```csharp
[Fact]
public void DamageEffect_DealsDamageToCreature()
[Fact]
public void DamageEffect_DealsDamageToPlayer()
[Fact]
public void DamageEffect_Fizzles_WhenTargetRemoved()
```

**Step 2: Implement DamageEffect**
```csharp
public class DamageEffect : SpellEffect
{
    public int Amount { get; }
    public bool CanTargetCreature { get; }
    public bool CanTargetPlayer { get; }

    public DamageEffect(int amount, bool canTargetCreature = true, bool canTargetPlayer = true)
    {
        Amount = amount;
        CanTargetCreature = canTargetCreature;
        CanTargetPlayer = canTargetPlayer;
    }

    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];

        if (target.CardId == Guid.Empty)
        {
            // Player target
            var player = target.PlayerId == state.Player1.Id ? state.Player1 : state.Player2;
            player.AdjustLife(-Amount);
            state.Log($"{spell.Card.Name} deals {Amount} damage to {player.Name}. ({player.Life} life)");
        }
        else
        {
            // Creature target
            var owner = target.PlayerId == state.Player1.Id ? state.Player1 : state.Player2;
            var creature = owner.Battlefield.Cards.FirstOrDefault(c => c.Id == target.CardId);
            if (creature == null) return;
            creature.DamageMarked += Amount;
            state.Log($"{spell.Card.Name} deals {Amount} damage to {creature.Name}.");
        }
    }
}
```

**Step 3: Run tests, commit**
```
feat(engine): add DamageEffect for burn spells
```

---

### Task 5: State-Based Actions

**Files:**
- Modify: `src/MtgDecker.Engine/GameEngine.cs`
- Test: `tests/MtgDecker.Engine.Tests/StateBasedActionTests.cs`

**Context:** After spell resolution, creatures with DamageMarked >= Toughness should die. Players at 0 life lose.

**Step 1: Write failing tests**
```csharp
[Fact]
public void CreatureWithLethalDamage_DiesAfterSpellResolution()
[Fact]
public void PlayerAtZeroLife_LosesGame()
[Fact]
public void DamageClears_AtEndOfTurn()
```

**Step 2: Implement CheckStateBasedActions**
```csharp
private void CheckStateBasedActions()
{
    // Check both players' battlefields
    foreach (var player in new[] { _state.Player1, _state.Player2 })
    {
        var dead = player.Battlefield.Cards
            .Where(c => c.IsCreature && c.Toughness.HasValue && c.DamageMarked >= c.Toughness.Value)
            .ToList();
        foreach (var creature in dead)
        {
            player.Battlefield.RemoveById(creature.Id);
            player.Graveyard.Add(creature);
            _state.Log($"{creature.Name} dies (lethal damage).");
        }
    }

    // Check player life
    foreach (var player in new[] { _state.Player1, _state.Player2 })
    {
        if (player.Life <= 0 && !_state.IsGameOver)
        {
            var opponent = _state.GetOpponent(player);
            _state.IsGameOver = true;
            _state.Winner = opponent.Name;
            _state.Log($"{player.Name} loses the game.");
        }
    }
}
```

**Step 3: Call CheckStateBasedActions after ResolveTopOfStack, after combat damage**

**Step 4: Add end-of-turn damage clearing** — clear DamageMarked on all creatures during cleanup.

**Step 5: Run tests, commit**
```
feat(engine): add state-based actions for lethal damage and player loss
```

---

### Task 6: DrawCardsEffect

**Files:**
- Create: `src/MtgDecker.Engine/Effects/DrawCardsEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/Effects/DrawCardsEffectTests.cs`

**Context:** Simple "draw N cards" effect. Controller draws N from library.

**Step 1: Write failing tests**
```csharp
[Fact]
public void DrawCardsEffect_DrawsNCards()
[Fact]
public void DrawCardsEffect_StopsAtEmptyLibrary()
```

**Step 2: Implement**
```csharp
public class DrawCardsEffect : SpellEffect
{
    public int Count { get; }
    public DrawCardsEffect(int count) => Count = count;

    public override void Resolve(GameState state, StackObject spell)
    {
        var player = spell.ControllerId == state.Player1.Id ? state.Player1 : state.Player2;
        for (int i = 0; i < Count; i++)
        {
            var card = player.Library.DrawFromTop();
            if (card == null) break;
            player.Hand.Add(card);
        }
        state.Log($"{player.Name} draws {Count} card(s).");
    }
}
```

**Step 3: Run tests, commit**
```
feat(engine): add DrawCardsEffect
```

---

### Task 7: BrainstormEffect

**Files:**
- Create: `src/MtgDecker.Engine/Effects/BrainstormEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/Effects/BrainstormEffectTests.cs`

**Context:** Draw 3 cards, then choose 2 cards from hand to put on top of library. Requires async resolution and ChooseCard prompts.

**Step 1: Write failing tests**
```csharp
[Fact]
public async Task BrainstormEffect_Draws3ThenPuts2Back()
[Fact]
public async Task BrainstormEffect_PutBackCardsAreOnTopOfLibrary()
```

**Step 2: Implement**
```csharp
public class BrainstormEffect : SpellEffect
{
    public override bool IsAsync => true;

    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct)
    {
        var player = spell.ControllerId == state.Player1.Id ? state.Player1 : state.Player2;

        // Draw 3
        for (int i = 0; i < 3; i++)
        {
            var card = player.Library.DrawFromTop();
            if (card != null) player.Hand.Add(card);
        }
        state.Log($"{player.Name} draws 3 cards (Brainstorm).");

        // Put 2 back on top
        for (int i = 0; i < 2; i++)
        {
            var cardId = await handler.ChooseCard(
                player.Hand.Cards,
                $"Put a card on top of your library ({i + 1}/2)",
                optional: false, ct);
            if (cardId.HasValue)
            {
                var card = player.Hand.RemoveById(cardId.Value);
                if (card != null) player.Library.AddToTop(card);
            }
        }
        state.Log($"{player.Name} puts 2 cards on top of library.");
    }
}
```

**Step 3: Ensure TestDecisionHandler's ChooseCard returns enqueued values for the put-back cards**

**Step 4: Run tests, commit**
```
feat(engine): add BrainstormEffect with interactive card selection
```

---

### Task 8: PonderEffect

**Files:**
- Create: `src/MtgDecker.Engine/Effects/PonderEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/Effects/PonderEffectTests.cs`

**Context:** Look at top 3 cards. Either shuffle library or keep them in order. Then draw 1. Uses RevealCards + ChooseCard for shuffle decision.

**Step 1: Write failing tests**
```csharp
[Fact]
public async Task PonderEffect_RevealsTop3_KeepOrder_DrawsOne()
[Fact]
public async Task PonderEffect_RevealsTop3_Shuffles_DrawsOne()
```

**Step 2: Implement**
```csharp
public class PonderEffect : SpellEffect
{
    public override bool IsAsync => true;

    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct)
    {
        var player = spell.ControllerId == state.Player1.Id ? state.Player1 : state.Player2;
        var top3 = player.Library.PeekTop(3);

        // Reveal the top 3 cards
        await handler.RevealCards(top3.ToList(), top3.ToList(),
            "Ponder: Look at the top 3 cards", ct);

        // Ask: shuffle or keep? (null = shuffle, non-null = keep order)
        var decision = await handler.ChooseCard(
            Array.Empty<GameCard>(),
            "Shuffle your library? (Choose to shuffle, Skip to keep order)",
            optional: true, ct);

        if (decision == null)
        {
            player.Library.Shuffle();
            state.Log($"{player.Name} shuffles their library (Ponder).");
        }
        else
        {
            state.Log($"{player.Name} keeps the card order (Ponder).");
        }

        // Draw 1
        var drawn = player.Library.DrawFromTop();
        if (drawn != null) player.Hand.Add(drawn);
        state.Log($"{player.Name} draws a card.");
    }
}
```

**Step 3: Run tests, commit**
```
feat(engine): add PonderEffect with reveal and shuffle decision
```

---

### Task 9: PreordainEffect

**Files:**
- Create: `src/MtgDecker.Engine/Effects/PreordainEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/Effects/PreordainEffectTests.cs`

**Context:** Scry 2 (look at top 2, put each on top or bottom), then draw 1.

**Step 1: Write failing tests**
```csharp
[Fact]
public async Task PreordainEffect_KeepsBothOnTop_DrawsOne()
[Fact]
public async Task PreordainEffect_BottomsBoth_DrawsOne()
[Fact]
public async Task PreordainEffect_KeepsOneBottomsOne_DrawsOne()
```

**Step 2: Implement**
```csharp
public class PreordainEffect : SpellEffect
{
    public override bool IsAsync => true;

    public override async Task ResolveAsync(GameState state, StackObject spell,
        IPlayerDecisionHandler handler, CancellationToken ct)
    {
        var player = spell.ControllerId == state.Player1.Id ? state.Player1 : state.Player2;
        var top2 = player.Library.PeekTop(2).ToList();

        // Remove from library temporarily
        foreach (var card in top2)
            player.Library.RemoveById(card.Id);

        // For each card: choose to keep on top or send to bottom
        var keepOnTop = new List<GameCard>();
        foreach (var card in top2)
        {
            var keep = await handler.ChooseCard(
                new[] { card },
                $"Keep {card.Name} on top? (Choose = top, Skip = bottom)",
                optional: true, ct);
            if (keep.HasValue)
                keepOnTop.Add(card);
            else
                player.Library.AddToBottom(card);
        }

        // Put kept cards back on top (in order)
        foreach (var card in keepOnTop)
            player.Library.AddToTop(card);

        state.Log($"{player.Name} scries 2 (Preordain).");

        // Draw 1
        var drawn = player.Library.DrawFromTop();
        if (drawn != null) player.Hand.Add(drawn);
        state.Log($"{player.Name} draws a card.");
    }
}
```

**Step 3: Run tests, commit**
```
feat(engine): add PreordainEffect with scry 2
```

---

### Task 10: Stack Targeting

**Files:**
- Modify: `src/MtgDecker.Engine/TargetFilter.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (CastSpell section for stack targeting)
- Test: `tests/MtgDecker.Engine.Tests/StackTargetingTests.cs`

**Context:** Counterspells target spells on the stack. Need TargetFilter.Spell() and engine support for presenting stack objects as targets.

**Step 1: Write failing tests**
```csharp
[Fact]
public void SpellFilter_MatchesStackObject()
[Fact]
public async Task CastSpell_WithSpellFilter_TargetsStackObject()
```

**Step 2: Add TargetFilter.Spell()**
```csharp
public static TargetFilter Spell() => new((card, zone) => zone == ZoneType.Stack);
```

**Step 3: Update GameEngine CastSpell**
When TargetFilter.Spell() is detected, present stack objects (excluding the counterspell itself) as targets.

Create GameCard wrappers for stack items for the ChooseTarget call, using ZoneType.Stack.

**Step 4: Run tests, commit**
```
feat(engine): add stack targeting for counterspells
```

---

### Task 11: CounterSpellEffect

**Files:**
- Create: `src/MtgDecker.Engine/Effects/CounterSpellEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/Effects/CounterSpellEffectTests.cs`

**Context:** Remove target spell from stack, move it to owner's graveyard. It never resolves.

**Step 1: Write failing tests**
```csharp
[Fact]
public void CounterSpellEffect_RemovesTargetFromStack()
[Fact]
public void CounterSpellEffect_Fizzles_WhenTargetAlreadyResolved()
[Fact]
public async Task CounterSpell_FullFlow_CastsAndCounters()
```

**Step 2: Implement**
```csharp
public class CounterSpellEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];

        var targetSpell = state.Stack.FirstOrDefault(s => s.Card.Id == target.CardId);
        if (targetSpell == null)
        {
            state.Log($"{spell.Card.Name} fizzles (target spell already resolved).");
            return;
        }

        state.Stack.Remove(targetSpell);
        var owner = targetSpell.ControllerId == state.Player1.Id ? state.Player1 : state.Player2;
        owner.Graveyard.Add(targetSpell.Card);
        state.Log($"{targetSpell.Card.Name} is countered by {spell.Card.Name}.");
    }
}
```

**Step 3: Run tests, commit**
```
feat(engine): add CounterSpellEffect
```

---

### Task 12: Register Burn Deck Cards

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/CardDefinitionRegistryTests.cs`

**Context:** Register ~10 Burn deck cards with appropriate effects and TargetFilters.

**Cards to register:**
```csharp
// === Burn deck ===
["Lightning Bolt"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Instant,
    TargetFilter.CreatureOrPlayer(), new DamageEffect(3)),
["Chain Lightning"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Sorcery,
    TargetFilter.CreatureOrPlayer(), new DamageEffect(3)),
["Lava Spike"] = new(ManaCost.Parse("{R}"), null, null, null, CardType.Sorcery,
    TargetFilter.Player(), new DamageEffect(3, canTargetCreature: false)),
["Rift Bolt"] = new(ManaCost.Parse("{1}{R}"), null, null, null, CardType.Sorcery,
    TargetFilter.CreatureOrPlayer(), new DamageEffect(3)),
["Fireblast"] = new(ManaCost.Parse("{4}{R}{R}"), null, null, null, CardType.Instant,
    TargetFilter.CreatureOrPlayer(), new DamageEffect(4)),
["Goblin Guide"] = new(ManaCost.Parse("{R}"), null, 2, 2, CardType.Creature) { Subtypes = ["Goblin"] },
["Monastery Swiftspear"] = new(ManaCost.Parse("{R}"), null, 1, 2, CardType.Creature) { Subtypes = ["Human", "Monk"] },
["Eidolon of the Great Revel"] = new(ManaCost.Parse("{R}{R}"), null, 2, 2,
    CardType.Creature | CardType.Enchantment) { Subtypes = ["Spirit"] },
["Flame Rift"] = new(ManaCost.Parse("{1}{R}"), null, null, null, CardType.Sorcery,
    Effect: new DamageAllPlayersEffect(4)),
["Searing Blood"] = new(ManaCost.Parse("{R}{R}"), null, null, null, CardType.Instant,
    TargetFilter.Creature(), new DamageEffect(2, canTargetPlayer: false)),
```

Note: Flame Rift needs a `DamageAllPlayersEffect(int amount)` that deals damage to each player. Simple variant — no targeting needed.

**Step 1: Write test verifying all cards are registered**
**Step 2: Add cards to CardDefinitions**
**Step 3: Run tests, commit**
```
feat(engine): register Burn deck cards
```

---

### Task 13: Register UR Delver Deck Cards

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/CardDefinitionRegistryTests.cs`

**Cards to register:**
```csharp
// === UR Delver deck ===
["Brainstorm"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Instant,
    Effect: new BrainstormEffect()),
["Ponder"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Sorcery,
    Effect: new PonderEffect()),
["Preordain"] = new(ManaCost.Parse("{U}"), null, null, null, CardType.Sorcery,
    Effect: new PreordainEffect()),
["Counterspell"] = new(ManaCost.Parse("{U}{U}"), null, null, null, CardType.Instant,
    TargetFilter.Spell(), new CounterSpellEffect()),
["Daze"] = new(ManaCost.Parse("{1}{U}"), null, null, null, CardType.Instant,
    TargetFilter.Spell(), new CounterSpellEffect()),
["Force of Will"] = new(ManaCost.Parse("{3}{U}{U}"), null, null, null, CardType.Instant,
    TargetFilter.Spell(), new CounterSpellEffect()),
["Delver of Secrets"] = new(ManaCost.Parse("{U}"), null, 1, 1, CardType.Creature) { Subtypes = ["Human", "Wizard"] },
["Murktide Regent"] = new(ManaCost.Parse("{5}{U}{U}"), null, 3, 3, CardType.Creature) { Subtypes = ["Dragon"] },
["Dragon's Rage Channeler"] = new(ManaCost.Parse("{R}"), null, 1, 1, CardType.Creature) { Subtypes = ["Human", "Shaman"] },
["Volcanic Island"] = new(null, ManaAbility.Choice(ManaColor.Blue, ManaColor.Red), null, null, CardType.Land),
["Scalding Tarn"] = new(null, null, null, null, CardType.Land),
["Mystic Sanctuary"] = new(null, ManaAbility.Fixed(ManaColor.Blue), null, null, CardType.Land),
["Island"] = new(null, ManaAbility.Fixed(ManaColor.Blue), null, null, CardType.Land),
```

**Step 1: Write test verifying all cards are registered**
**Step 2: Add cards to CardDefinitions**
**Step 3: Run tests, commit**
```
feat(engine): register UR Delver deck cards
```

---

### Task 14: Integration Tests

**Files:**
- Create: `tests/MtgDecker.Engine.Tests/BurnDeckIntegrationTests.cs`
- Create: `tests/MtgDecker.Engine.Tests/DelverDeckIntegrationTests.cs`

**Tests:**
- Lightning Bolt deals 3 to player, reduces life
- Lightning Bolt deals 3 to creature, creature dies from SBA
- Brainstorm full flow: draw 3, put 2 back, verify library top
- Ponder full flow: reveal 3, shuffle, draw 1
- Counterspell counters a Lightning Bolt on the stack
- Counter-war: Counterspell targets Counterspell on stack
- Damage spell fizzles when creature target dies before resolution

**Step 1: Write tests using TestDecisionHandler with enqueued actions**
**Step 2: Run tests, verify all pass**
**Step 3: Commit**
```
test(engine): add Burn and Delver deck integration tests
```

---

### Task 15: UI Updates for Player & Stack Targeting

**Files:**
- Modify: `src/MtgDecker.Web/Components/Pages/Game/PlayerZone.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/GameBoard.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/Game/StackDisplay.razor`
- Modify: `src/MtgDecker.Web/Components/Pages/GamePage.razor`

**Changes:**
1. Target picker shows "Target [PlayerName]" button when eligible targets include players
2. Target picker shows stack items when targeting spells (for counterspells)
3. Build verification (no unit tests for UI, just `dotnet build`)

**Step 1: Update target picker in PlayerZone.razor**
**Step 2: Update StackDisplay to show targetable spells when in targeting mode**
**Step 3: Build, commit**
```
feat(web): update UI for player and stack targeting
```
