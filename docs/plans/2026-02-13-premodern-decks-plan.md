# Premodern Decks Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add CardDefinitions for 8 Premodern tournament decks with supporting engine mechanics.

**Architecture:** New SpellEffect/TriggerEffect classes + Keywords + TargetFilter factories → CardDefinitions entries

**Tech Stack:** C# 14, .NET 10, xUnit + FluentAssertions

**Working directory:** `.worktrees/premodern-decks/`

---

### Task 1: New Keywords (Flying, Trample, FirstStrike, Protection)

**Files:**
- Modify: `src/MtgDecker.Engine/Enums/Keyword.cs`

**Step 1: Add new keywords to enum**

```csharp
public enum Keyword
{
    Haste,
    Shroud,
    Mountainwalk,
    Flying,
    Trample,
    FirstStrike,
    Protection,
    Swampwalk,
    Forestwalk,
    Islandwalk,
    Plainswalk,
}
```

**Step 2: Commit**

```bash
git add src/MtgDecker.Engine/Enums/Keyword.cs
git commit -m "feat(engine): add Flying, Trample, FirstStrike, Protection keywords"
```

---

### Task 2: New SpellEffects — Destruction & Removal

**Files:**
- Create: `src/MtgDecker.Engine/Effects/DestroyCreatureEffect.cs`
- Create: `src/MtgDecker.Engine/Effects/DestroyPermanentEffect.cs`
- Create: `src/MtgDecker.Engine/Effects/DestroyAllCreaturesEffect.cs`
- Create: `src/MtgDecker.Engine/Effects/DestroyAllLandsEffect.cs`
- Create: `src/MtgDecker.Engine/Effects/EdictEffect.cs`
- Create: `src/MtgDecker.Engine/Effects/DamageAllCreaturesAndPlayersEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/Effects/DestructionEffectsTests.cs`

**DestroyCreatureEffect** — Like SwordsToPlowshares but just destroys (goes to graveyard):
```csharp
public class DestroyCreatureEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];
        var owner = state.GetPlayer(target.PlayerId);
        var creature = owner.Battlefield.Cards.FirstOrDefault(c => c.Id == target.CardId);
        if (creature == null) return;
        owner.Battlefield.Remove(creature);
        owner.Graveyard.Add(creature);
        state.Log($"{spell.Card.Name} destroys {creature.Name}.");
    }
}
```

**DestroyPermanentEffect** — Same but for any permanent (Vindicate):
```csharp
public class DestroyPermanentEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];
        var owner = state.GetPlayer(target.PlayerId);
        var permanent = owner.Battlefield.Cards.FirstOrDefault(c => c.Id == target.CardId);
        if (permanent == null) return;
        owner.Battlefield.Remove(permanent);
        owner.Graveyard.Add(permanent);
        state.Log($"{spell.Card.Name} destroys {permanent.Name}.");
    }
}
```

**DestroyAllCreaturesEffect** — Wrath of God:
```csharp
public class DestroyAllCreaturesEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        foreach (var player in new[] { state.Player1, state.Player2 })
        {
            var creatures = player.Battlefield.Cards.Where(c => c.IsCreature).ToList();
            foreach (var creature in creatures)
            {
                player.Battlefield.Remove(creature);
                player.Graveyard.Add(creature);
            }
        }
        state.Log($"{spell.Card.Name} destroys all creatures.");
    }
}
```

**DestroyAllLandsEffect** — Armageddon:
```csharp
public class DestroyAllLandsEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        foreach (var player in new[] { state.Player1, state.Player2 })
        {
            var lands = player.Battlefield.Cards.Where(c => c.IsLand).ToList();
            foreach (var land in lands)
            {
                player.Battlefield.Remove(land);
                player.Graveyard.Add(land);
            }
        }
        state.Log($"{spell.Card.Name} destroys all lands.");
    }
}
```

**EdictEffect** — Target player sacrifices a creature:
```csharp
public class EdictEffect : SpellEffect
{
    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];
        var player = state.GetPlayer(target.PlayerId);
        var creature = player.Battlefield.Cards.FirstOrDefault(c => c.IsCreature);
        if (creature == null) return;
        player.Battlefield.Remove(creature);
        player.Graveyard.Add(creature);
        state.Log($"{player.Name} sacrifices {creature.Name}.");
    }
}
```

**DamageAllCreaturesAndPlayersEffect** — Plague Spitter / Pyroclasm:
```csharp
public class DamageAllCreaturesAndPlayersEffect : SpellEffect
{
    public int Amount { get; }
    public bool DamagePlayers { get; }

    public DamageAllCreaturesAndPlayersEffect(int amount, bool damagePlayers = false)
    {
        Amount = amount;
        DamagePlayers = damagePlayers;
    }

    public override void Resolve(GameState state, StackObject spell)
    {
        foreach (var player in new[] { state.Player1, state.Player2 })
        {
            foreach (var creature in player.Battlefield.Cards.Where(c => c.IsCreature).ToList())
                creature.DamageMarked += Amount;
            if (DamagePlayers)
                player.AdjustLife(-Amount);
        }
        state.Log($"{spell.Card.Name} deals {Amount} damage to all creatures{(DamagePlayers ? " and players" : "")}.");
    }
}
```

**Tests:** Write tests for each effect verifying creatures go to graveyard, edict picks a creature, board wipe clears all, etc.

**Commit:**
```bash
git add src/MtgDecker.Engine/Effects/ tests/MtgDecker.Engine.Tests/Effects/
git commit -m "feat(engine): add destruction, edict, and board wipe spell effects"
```

---

### Task 3: New SpellEffects — Discard, Mana, Life

**Files:**
- Create: `src/MtgDecker.Engine/Effects/DiscardEffect.cs`
- Create: `src/MtgDecker.Engine/Effects/AddManaSpellEffect.cs`
- Create: `src/MtgDecker.Engine/Effects/GainLifeEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/Effects/DiscardAndManaEffectsTests.cs`

**DiscardEffect** — Target player discards (Duress = noncreature/nonland; Cabal Therapy = named card):
```csharp
public class DiscardEffect : SpellEffect
{
    public int Count { get; }
    public Func<GameCard, bool>? Filter { get; }

    public DiscardEffect(int count = 1, Func<GameCard, bool>? filter = null)
    {
        Count = count;
        Filter = filter;
    }

    public override void Resolve(GameState state, StackObject spell)
    {
        if (spell.Targets.Count == 0) return;
        var target = spell.Targets[0];
        var player = state.GetPlayer(target.PlayerId);
        var candidates = Filter != null
            ? player.Hand.Cards.Where(Filter).ToList()
            : player.Hand.Cards.ToList();
        // Discard up to Count cards (engine picks first available for simplicity)
        for (int i = 0; i < Count && candidates.Count > 0; i++)
        {
            var card = candidates[0];
            candidates.RemoveAt(0);
            player.Hand.Remove(card);
            player.Graveyard.Add(card);
            state.Log($"{player.Name} discards {card.Name}.");
        }
    }
}
```

**AddManaSpellEffect** — Dark Ritual adds {B}{B}{B}:
```csharp
public class AddManaSpellEffect : SpellEffect
{
    public ManaColor Color { get; }
    public int Amount { get; }

    public AddManaSpellEffect(ManaColor color, int amount)
    {
        Color = color;
        Amount = amount;
    }

    public override void Resolve(GameState state, StackObject spell)
    {
        var caster = state.GetPlayer(spell.ControllerId);
        for (int i = 0; i < Amount; i++)
            caster.ManaPool.Add(Color);
        state.Log($"{spell.Card.Name} adds {Amount} {Color} mana.");
    }
}
```

**GainLifeEffect** — for Absorb, etc.:
```csharp
public class GainLifeEffect : SpellEffect
{
    public int Amount { get; }

    public GainLifeEffect(int amount) => Amount = amount;

    public override void Resolve(GameState state, StackObject spell)
    {
        var caster = state.GetPlayer(spell.ControllerId);
        caster.AdjustLife(Amount);
        state.Log($"{caster.Name} gains {Amount} life. ({caster.Life} life)");
    }
}
```

**Tests & Commit:**
```bash
git commit -m "feat(engine): add discard, add-mana, and gain-life spell effects"
```

---

### Task 4: New TargetFilter factories + Continuous Effect types

**Files:**
- Modify: `src/MtgDecker.Engine/TargetFilter.cs`
- Modify: `src/MtgDecker.Engine/ContinuousEffect.cs` (add new ContinuousEffectType values)

**New TargetFilter factories:**
```csharp
public static TargetFilter NonBlackCreature() => new((card, zone) =>
    zone == ZoneType.Battlefield && card.IsCreature && !card.Colors.Contains(ManaColor.Black));

public static TargetFilter CreatureWithCMCAtMost(int maxCmc) => new((card, zone) =>
    zone == ZoneType.Battlefield && card.IsCreature && (card.ManaCostValue ?? 0) <= maxCmc);

public static TargetFilter AnyPermanent() => new((card, zone) => zone == ZoneType.Battlefield);

public static TargetFilter NonCreatureNonLand() => new((card, zone) =>
    zone == ZoneType.Battlefield && !card.IsCreature && !card.IsLand);
```

**New ContinuousEffectTypes:**
- `PreventLifeGain` — Sulfuric Vortex
- `DealDamageOnUpkeep` — Sulfuric Vortex (3 damage to each player)

**Commit:**
```bash
git commit -m "feat(engine): add new TargetFilter factories and continuous effect types"
```

---

### Task 5: New TriggerEffects — Upkeep damage, discard, draw+life

**Files:**
- Create: `src/MtgDecker.Engine/Triggers/Effects/DamageAllPlayersEffect.cs` (trigger version)
- Create: `src/MtgDecker.Engine/Triggers/Effects/EachPlayerDiscardsEffect.cs`
- Create: `src/MtgDecker.Engine/Triggers/Effects/RackDamageEffect.cs`
- Create: `src/MtgDecker.Engine/Triggers/Effects/DrawAndLoseLifeEffect.cs`
- Create: `src/MtgDecker.Engine/Triggers/Effects/DamageAllCreaturesEffect.cs` (trigger version)
- Test: `tests/MtgDecker.Engine.Tests/Triggers/UpkeepTriggerEffectsTests.cs`

These are `IEffect` implementations (trigger effects, not spell effects):

**DamageAllPlayersTriggerEffect** — Sulfuric Vortex / Plague Spitter upkeep:
```csharp
public class DamageAllPlayersTriggerEffect : IEffect
{
    public int Amount { get; }
    public DamageAllPlayersTriggerEffect(int amount) => Amount = amount;

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        foreach (var player in new[] { context.State.Player1, context.State.Player2 })
            player.AdjustLife(-Amount);
        context.State.Log($"{context.Source.Name} deals {Amount} damage to each player.");
        return Task.CompletedTask;
    }
}
```

**EachPlayerDiscardsEffect** — Bottomless Pit upkeep:
```csharp
public class EachPlayerDiscardsEffect : IEffect
{
    public int Count { get; }
    public EachPlayerDiscardsEffect(int count = 1) => Count = count;

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        foreach (var player in new[] { context.State.Player1, context.State.Player2 })
        {
            for (int i = 0; i < Count && player.Hand.Cards.Count > 0; i++)
            {
                var card = player.Hand.Cards[^1]; // discard last card (random-ish)
                player.Hand.Remove(card);
                player.Graveyard.Add(card);
                context.State.Log($"{player.Name} discards {card.Name}.");
            }
        }
        return Task.CompletedTask;
    }
}
```

**RackDamageEffect** — The Rack: deals max(0, 3 - handsize) to opponent:
```csharp
public class RackDamageEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var opponent = context.State.GetOpponent(context.Controller);
        int damage = Math.Max(0, 3 - opponent.Hand.Cards.Count);
        if (damage > 0)
        {
            opponent.AdjustLife(-damage);
            context.State.Log($"The Rack deals {damage} damage to {opponent.Name}. ({opponent.Hand.Cards.Count} cards in hand)");
        }
        return Task.CompletedTask;
    }
}
```

**DrawAndLoseLifeEffect** — Phyrexian Arena:
```csharp
public class DrawAndLoseLifeEffect : IEffect
{
    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        context.Controller.DrawCards(1);
        context.Controller.AdjustLife(-1);
        context.State.Log($"{context.Controller.Name} draws a card and loses 1 life from Phyrexian Arena.");
        return Task.CompletedTask;
    }
}
```

**DamageAllCreaturesTriggerEffect** — Plague Spitter upkeep (1 damage to all creatures):
```csharp
public class DamageAllCreaturesTriggerEffect : IEffect
{
    public int Amount { get; }
    public bool IncludePlayers { get; }

    public DamageAllCreaturesTriggerEffect(int amount, bool includePlayers = false)
    {
        Amount = amount;
        IncludePlayers = includePlayers;
    }

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        foreach (var player in new[] { context.State.Player1, context.State.Player2 })
        {
            foreach (var creature in player.Battlefield.Cards.Where(c => c.IsCreature))
                creature.DamageMarked += Amount;
            if (IncludePlayers)
                player.AdjustLife(-Amount);
        }
        context.State.Log($"{context.Source.Name} deals {Amount} to all creatures{(IncludePlayers ? " and players" : "")}.");
        return Task.CompletedTask;
    }
}
```

**Commit:**
```bash
git commit -m "feat(engine): add upkeep trigger effects (Rack, Arena, Pit, Plague Spitter)"
```

---

### Task 6: Man-land ActivatedAbility support

**Files:**
- Create: `src/MtgDecker.Engine/Triggers/Effects/BecomeCreatureEffect.cs`
- Test: `tests/MtgDecker.Engine.Tests/Triggers/ManLandTests.cs`

Man-lands have an activated ability that makes them a creature until end of turn. We already have `ContinuousEffectType.BecomeCreature` but need a trigger effect that adds an until-end-of-turn continuous effect to the game state.

```csharp
public class BecomeCreatureEffect : IEffect
{
    public int Power { get; }
    public int Toughness { get; }
    public string[] Subtypes { get; }

    public BecomeCreatureEffect(int power, int toughness, params string[] subtypes)
    {
        Power = power;
        Toughness = toughness;
        Subtypes = subtypes;
    }

    public Task Execute(EffectContext context, CancellationToken ct = default)
    {
        var effect = new ContinuousEffect(
            context.Source.Id,
            ContinuousEffectType.BecomeCreature,
            (card, _) => card.Id == context.Source.Id,
            PowerMod: Power,
            ToughnessMod: Toughness,
            UntilEndOfTurn: true);
        context.State.ContinuousEffects.Add(effect);
        context.State.Log($"{context.Source.Name} becomes a {Power}/{Toughness} creature until end of turn.");
        return Task.CompletedTask;
    }
}
```

Update Mishra's Factory, add Treetop Village, Faerie Conclave, Spawning Pool definitions using this effect.

**Commit:**
```bash
git commit -m "feat(engine): add man-land activation (BecomeCreatureEffect)"
```

---

### Task 7: Shared cards — basic lands, mana, common removal

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/CardDefinitionRegistryTests.cs` (add coverage)

Add these commonly-shared cards:

**Basic lands:**
- `Swamp` — Fixed(ManaColor.Black)

**Mana:**
- `Dark Ritual` — {B} Instant, AddManaSpellEffect(Black, 3)

**Dual/Pain lands:**
- `Caves of Koilos` — Choice(Colorless, White, Black)
- `Llanowar Wastes` — Choice(Colorless, Black, Green)
- `Battlefield Forge` — Choice(Colorless, Red, White)
- `Tainted Field` — Choice(White, Black) (simplified - skip "only if you control Swamp" condition)
- `Coastal Tower` — Choice(White, Blue)
- `Skycloud Expanse` — Choice(White, Blue)
- `Flooded Strand` — FetchAbility(["Plains", "Island"])
- `Adarkar Wastes` — Choice(Colorless, White, Blue)
- `Gemstone Mine` — Choice(all 5 colors) (simplified — skip counter depletion)
- `City of Brass` — Choice(all 5 colors) (simplified — skip damage)

**Fetch lands:**
- `Bloodstained Mire` (if needed)

**Common removal:**
- `Disenchant` — {1}{W} Instant, destroy target artifact/enchantment (same as Naturalize but white)
- `Vindicate` — {1}{W}{B} Sorcery, destroy target permanent
- `Smother` — {1}{B} Instant, destroy target creature with CMC ≤ 3
- `Snuff Out` — {3}{B} Instant, destroy target nonblack creature (simplified — skip alt cost)
- `Diabolic Edict` — {1}{B} Instant, EdictEffect
- `Wrath of God` — {2}{W}{W} Sorcery, DestroyAllCreaturesEffect

**Common discard:**
- `Duress` — {B} Sorcery, target player discards noncreature/nonland
- `Cabal Therapy` — {B} Sorcery, DiscardEffect (simplified — skip naming/flashback)
- `Gerrard's Verdict` — {W}{B} Sorcery, target player discards 2

**Commit:**
```bash
git commit -m "feat(engine): add shared Premodern cards (lands, removal, discard, mana)"
```

---

### Task 8: Deck 13 — Mono Black Control cards

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/CardDefinitions/MonoBlackControlTests.cs`

Cards to add (deck-specific):
- `Bane of the Living` — {2}{B}{B} 4/3 Zombie (simplified — skip Morph)
- `Plague Spitter` — {2}{B} 2/2, upkeep: 1 damage to all creatures and players
- `Withered Wretch` — {B}{B} 2/2 Zombie, activated: {1}: exile card from graveyard (simplified to vanilla for now)
- `Funeral Charm` — {B} Instant, target player discards (simplified — just discard mode)
- `Bottomless Pit` — {1}{B}{B} Enchantment, upkeep: each player discards
- `The Rack` — {1} Artifact, upkeep: deal 3-handsize damage to opponent
- `Cursed Scroll` — {1} Artifact (simplified — treat as vanilla artifact, activated: {3}, tap: deal 2)
- `Powder Keg` — {2} Artifact (simplified — vanilla for now)
- `Cabal Pit` — Land, tap for {B} (simplified — skip Threshold ability)
- `Dust Bowl` — Land, tap for {C}, activated: {3}+tap+sac land: destroy nonbasic (simplified to just mana)
- `Mishra's Factory` — already exists but update with man-land activation

**Commit:**
```bash
git commit -m "feat(engine): add Mono Black Control card definitions"
```

---

### Task 9: Deck 01 — Sligh (RDW) cards

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`

Cards to add:
- `Ball Lightning` — {R}{R}{R} 6/1, Haste, Trample (simplified — skip sacrifice at EOT)
- `Grim Lavamancer` — {R} 1/1, activated: tap + exile 2 from graveyard: deal 2 (simplified — tap: deal 1)
- `Jackal Pup` — {R} 2/1 Hound
- `Incinerate` — {1}{R} Instant, deal 3 damage to creature or player
- `Shock` — {R} Instant, deal 2 damage to creature or player
- `Sulfuric Vortex` — {1}{R}{R} Enchantment, upkeep: deal 2 to each player
- `Barbarian Ring` — Land, tap for {R} (simplified — skip Threshold)

**Commit:**
```bash
git commit -m "feat(engine): add Sligh (RDW) card definitions"
```

---

### Task 10: Deck 03 — Mono Black Aggro cards

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`

Cards to add:
- `Hypnotic Specter` — {1}{B}{B} 2/2, Flying, combat damage: target player discards random
- `Nantuko Shade` — {B}{B} 2/1, activated: {B}: +1/+1 until EOT (simplified — vanilla 2/1)
- `Ravenous Rats` — {1}{B} 1/1, ETB: target opponent discards
- `Graveborn Muse` — {2}{B}{B} 3/3, upkeep: draw cards = Zombies, lose that much life
- `Skeletal Scrying` — {X}{B} Instant, draw X (simplified — treat as draw 2 for {2}{B})
- `Spawning Pool` — Land, tap for {B}, man-land: {1}{B}: becomes 1/1 Skeleton

**Commit:**
```bash
git commit -m "feat(engine): add Mono Black Aggro card definitions"
```

---

### Task 11: Deck 09 — Deadguy Ale cards

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`

Cards to add:
- `Exalted Angel` — {4}{W}{W} 4/5, Flying, lifelink (simplified — Flying + vanilla)
- `Knight of Stromgald` — {B}{B} 2/1, Protection from White (simplified — vanilla 2/1)
- `Phyrexian Rager` — {2}{B} 2/2, ETB: draw 1 card, lose 1 life
- `Phyrexian Arena` — {1}{B}{B} Enchantment, upkeep: draw 1, lose 1 life

**Commit:**
```bash
git commit -m "feat(engine): add Deadguy Ale card definitions"
```

---

### Task 12: Deck 10 — Landstill cards

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`

Cards to add:
- `Absorb` — {W}{U}{U} Instant, counter target spell + gain 3 life
- `Fact or Fiction` — {3}{U} Instant, draw 3 (simplified — skip pile selection)
- `Impulse` — {1}{U} Instant, look at top 4, draw 1 (simplified — draw 1)
- `Mana Leak` — {1}{U} Instant, counter target spell (simplified — hard counter)
- `Prohibit` — {1}{U} Instant, counter target spell CMC ≤ 2 (simplified — hard counter)
- `Decree of Justice` — {X}{X}{2}{W}{W} Sorcery, create X 4/4 Angel tokens (simplified — skip cycling mode)
- `Humility` — {2}{W}{W} Enchantment, all creatures are 1/1 with no abilities
- `Standstill` — {1}{U} Enchantment (simplified — vanilla enchantment)
- `Phyrexian Furnace` — {1} Artifact (simplified — vanilla)
- `Powder Keg` — already added in Task 8
- `Faerie Conclave` — Land, tap for {U}, man-land: {1}{U}: becomes 2/1 Flying Faerie
- `Dust Bowl` — Land, tap for {C} (simplified)

**Commit:**
```bash
git commit -m "feat(engine): add Landstill card definitions"
```

---

### Task 13: Deck 11 — Oath of Druids cards

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`

Cards to add:
- `Terravore` — {1}{G}{G} */* Lhurgoyf, P/T = lands in all graveyards (simplified — 3/3)
- `Call of the Herd` — {2}{G} Sorcery, create 3/3 Elephant token
- `Cataclysm` — {2}{W}{W} Sorcery, each player keeps 1 creature/land/artifact/enchantment (simplified — DestroyAllCreaturesEffect)
- `Deep Analysis` — {3}{U} Sorcery, draw 2 cards
- `Funeral Pyre` — {W} Instant (simplified — vanilla)
- `Quiet Speculation` — {1}{U} Sorcery (simplified — vanilla, needs Flashback mechanic)
- `Ray of Revelation` — {1}{W} Instant, destroy target enchantment
- `Reckless Charge` — {R} Sorcery, target creature gets +3/+0 and Haste until EOT (simplified — damage 3)
- `Volcanic Spray` — {1}{R} Sorcery, 1 damage to all creatures without Flying (simplified — 1 damage to all creatures)
- `Mox Diamond` — {0} Artifact (simplified — vanilla artifact)
- `Oath of Druids` — {1}{G} Enchantment (simplified — vanilla enchantment, very complex trigger)
- `Treetop Village` — Land, tap for {G}, man-land: {1}{G}: becomes 3/3 Ape with Trample
- `Treva's Ruins` — Land, Choice(White, Blue, Green) (simplified — skip bounce)
- `Darigaaz's Caldera` — Land, Choice(Black, Red, Green) (simplified — skip bounce)

**Commit:**
```bash
git commit -m "feat(engine): add Oath of Druids card definitions"
```

---

### Task 14: Deck 06 — Terrageddon cards

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`

Cards to add:
- `Mother of Runes` — {W} 1/1, activated: Tap: target creature gains protection (simplified — vanilla 1/1)
- `Nimble Mongoose` — {G} 1/1, Shroud (simplified — skip Threshold 3/3)
- `Armageddon` — {3}{W} Sorcery, destroy all lands
- `Zuran Orb` — {0} Artifact (simplified — vanilla)
- `Mox Diamond` — already added in Task 13

**Commit:**
```bash
git commit -m "feat(engine): add Terrageddon card definitions"
```

---

### Task 15: Deck 05 — Elves cards

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`

Cards to add:
- `Llanowar Elves` — {G} 1/1 Elf, tap: add {G}
- `Fyndhorn Elves` — {G} 1/1 Elf, tap: add {G}
- `Priest of Titania` — {1}{G} 1/1 Elf, tap: add {G} for each Elf (Dynamic mana)
- `Quirion Ranger` — {G} 1/1 Elf (simplified — skip bounce-land ability)
- `Wirewood Symbiote` — {G} 1/1 Insect (simplified — skip bounce-elf ability)
- `Multani's Acolyte` — {G}{G} 2/1 Elf, ETB: draw 2 (simplified — skip Echo)
- `Deranged Hermit` — {3}{G}{G} 1/1 Elf, ETB: create 4 1/1 Squirrel tokens (simplified — skip Echo, skip pump)
- `Wall of Blossoms` — {1}{G} 0/4 Wall, ETB: draw 1
- `Wall of Roots` — {1}{G} 0/5 Wall (simplified — skip mana ability with -0/-1)
- `Ravenous Baloth` — {2}{G}{G} 4/4 Beast, sac: gain 4 life
- `Caller of the Claw` — {2}{G} 2/2, ETB: create Bear tokens (simplified — vanilla)
- `Masticore` — {4} 4/4 Artifact Creature (simplified — skip upkeep/abilities)
- `Nantuko Vigilante` — {3}{G} 3/2 (simplified — skip Morph)
- `Yavimaya Granger` — {2}{G} 2/2 Elf, ETB: search for basic land (simplified — skip Echo)
- `Anger` — {3}{R} 2/2 (simplified — skip graveyard haste grant)
- `Squee, Goblin Nabob` — {2}{R} 1/1 Goblin, Legendary (simplified — skip graveyard return)
- `Survival of the Fittest` — {1}{G} Enchantment (simplified — vanilla enchantment, very complex)
- `Gaea's Cradle` — Legendary Land, tap: add {G} for each creature you control (Dynamic mana)

**Commit:**
```bash
git commit -m "feat(engine): add Elves card definitions"
```

---

### Task 16: Run full test suite and verify

**Step 1:** Run all engine tests
```bash
dotnet test tests/MtgDecker.Engine.Tests/ -v q
```

**Step 2:** Run all app tests
```bash
dotnet test tests/MtgDecker.Domain.Tests/ -v q
dotnet test tests/MtgDecker.Application.Tests/ -v q
dotnet test tests/MtgDecker.Infrastructure.Tests/ -v q
```

**Step 3:** Build web project
```bash
dotnet build src/MtgDecker.Web/ -v q
```

All must pass with 0 failures.
