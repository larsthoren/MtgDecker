# Activated & Triggered Abilities Design

**Goal:** Implement activated abilities (sacrifice, tap, mana costs) and triggered abilities (combat damage, attacks, cast triggers, upkeep) for 16 cards across the Goblins and Enchantress decks.

**Scope:** 9 cards with activated abilities, 7 cards with triggered abilities.

---

## Section 1: Activated Ability Architecture

Generic framework for abilities that require paying a cost (sacrifice, tap, mana) to produce an effect.

### New Types

```csharp
public record ActivatedAbilityCost(
    bool TapSelf = false,
    bool SacrificeSelf = false,
    string? SacrificeSubtype = null,   // sacrifice another creature with this subtype
    ManaCost? ManaCost = null);

public record ActivatedAbility(
    ActivatedAbilityCost Cost,
    IEffect Effect,
    Func<GameCard, bool>? TargetFilter = null,
    bool CanTargetPlayer = false);
```

### New ActionType

```csharp
ActivateAbility  // in ActionType enum
```

`GameAction.ActivateAbility(playerId, cardId, targetId?, targetPlayerId?)` factory method.

### Engine Flow

1. Player chooses `ActivateAbility` action with source card ID
2. Engine looks up `CardDefinition.ActivatedAbility`
3. Validates cost can be paid (untapped if TapSelf, has sacrificeable creature if SacrificeSubtype, enough mana)
4. Pays cost (tap, sacrifice, spend mana)
5. Resolves effect immediately (not on stack) via `IEffect.Execute(context)`
6. Calls `OnBoardChangedAsync()` after resolution

### EffectContext Extensions

Add `Target` (GameCard?) and `TargetPlayerId` (Guid?) fields to EffectContext so effects know their target.

---

## Section 2: Trigger System Extensions

### New TriggerConditions

```csharp
public enum TriggerCondition
{
    Self,                        // existing — ETB self
    AnyCreatureDies,            // existing enum value — Goblin Sharpshooter untap
    ControllerCastsEnchantment, // new — Argothian Enchantress, Enchantress's Presence
    SelfDealsCombatDamage,      // new — Goblin Lackey
    SelfAttacks,                // new — Goblin Piledriver
    Upkeep,                     // new — Mirri's Guile, Sylvan Library
}
```

### Extending ProcessTriggersAsync

Currently only handles `TriggerCondition.Self` for ETB. Extended signature:

```csharp
async Task ProcessTriggersAsync(GameEvent evt, Guid? sourceCardId = null,
    GameCard? relevantCard = null)
```

Iterates all permanents on active player's battlefield, checks each trigger's Condition against the event, executes matching effects.

### Delayed Triggers

For Goblin Pyromancer's "destroy all Goblins at end of turn":

```csharp
public record DelayedTrigger(GameEvent FireOn, IEffect Effect, Guid ControllerId);

// On GameState:
public List<DelayedTrigger> DelayedTriggers { get; } = [];
```

Delayed triggers are checked and fired during the end step, then removed.

### GameEvents Used

`Dies`, `SpellCast`, `CombatDamageDealt`, `BeginCombat`, `Upkeep`, `EndStep` — most already exist in the enum. Wire ProcessTriggersAsync calls at appropriate points in the game loop.

---

## Section 3: New IEffect Implementations

### For Activated Abilities

| Effect Class | Used By | Behavior |
|---|---|---|
| `DealDamageEffect(int amount)` | Mogg Fanatic, Siege-Gang Commander, Sharpshooter | Deal damage to target creature or player |
| `AddManaEffect(ManaColor color)` | Skirk Prospector | Add 1 mana to controller's pool |
| `DestroyTargetEffect` | Goblin Tinkerer, Seal of Cleansing | Destroy targeted permanent |
| `SearchLibraryByTypeEffect(CardType type)` | Sterling Grove | Search library for card of matching type |
| `TapTargetEffect` | Rishadan Port | Tap target land |
| `DestroyTargetLandEffect` | Wasteland | Destroy target nonbasic land |

### For Triggered Abilities

| Effect Class | Used By | Behavior |
|---|---|---|
| `PutCreatureFromHandEffect(string subtype)` | Goblin Lackey | Choose matching creature from hand, put onto battlefield |
| `PiledriverPumpEffect` | Goblin Piledriver | Count other attacking Goblins, add UntilEndOfTurn +2/+0 per |
| `PyromancerEffect` | Goblin Pyromancer | All Goblins +2/+0 UntilEndOfTurn + delayed end-of-turn destroy all Goblins |
| `DrawCardEffect` | Argothian Enchantress, Enchantress's Presence | Draw 1 card |
| `RearrangeTopEffect(int count)` | Mirri's Guile | Look at top N, choose 1 for top (simplified) |
| `SylvanLibraryEffect` | Sylvan Library | Draw 2 extra, choose cards to put back (4 life per kept) |

All effects use existing `EffectContext` pattern with `context.State`, `context.Controller`, `context.DecisionHandler`.

---

## Section 4: Card Registrations

### Sacrifice-for-Damage
- **Mogg Fanatic** — ActivatedAbility: sacrifice self -> deal 1 damage to target creature/player
- **Siege-Gang Commander** — ActivatedAbility: {1}{R} + sacrifice a Goblin -> deal 2 damage to target creature/player (keeps existing ETB token trigger)
- **Goblin Sharpshooter** — ActivatedAbility: tap -> deal 1 damage to target creature/player. Trigger: AnyCreatureDies -> untap self

### Sacrifice-for-Utility
- **Skirk Prospector** — ActivatedAbility: sacrifice a Goblin -> add {R}
- **Goblin Tinkerer** — ActivatedAbility: sacrifice self -> destroy target artifact
- **Seal of Cleansing** — ActivatedAbility: sacrifice self -> destroy target artifact or enchantment
- **Sterling Grove** — ActivatedAbility: {1}, sacrifice self -> search library for enchantment
- **Wasteland** — ActivatedAbility: tap + sacrifice self -> destroy target nonbasic land

### Tap Ability (No Sacrifice)
- **Rishadan Port** — ActivatedAbility: {1}, tap -> tap target land

### Combat Triggers
- **Goblin Lackey** — Trigger: SelfDealsCombatDamage -> put Goblin from hand onto battlefield
- **Goblin Piledriver** — Trigger: SelfAttacks -> +2/+0 per other attacking Goblin (UntilEndOfTurn)

### ETB + Delayed Trigger
- **Goblin Pyromancer** — Trigger: ETB -> all Goblins +2/+0 UntilEndOfTurn + delayed end-of-turn destroy all Goblins

### Cast Triggers
- **Argothian Enchantress** — Trigger: ControllerCastsEnchantment -> draw a card
- **Enchantress's Presence** — Trigger: ControllerCastsEnchantment -> draw a card

### Upkeep Triggers
- **Mirri's Guile** — Trigger: Upkeep -> rearrange top 3 cards of library
- **Sylvan Library** — Trigger: Upkeep -> Sylvan Library draw/pay life effect

---

## Section 5: AI Bot Updates + Testing

### AI Bot Priority Order in GetAction

1. Play a land (existing)
2. Activate fetch lands (existing)
3. **Activate sacrifice abilities** — damage abilities vs low-toughness creatures or low life; sacrifice for mana (Skirk Prospector) when it enables a cast
4. **Activate tap abilities** — Rishadan Port: tap opponent's land during their upkeep
5. Cast spells (existing)

Basic heuristics only: sacrifice Mogg Fanatic/Sharpshooter when they can kill a creature, use Skirk Prospector when holding a castable spell needing 1 more mana.

### Testing Strategy

Each card gets a dedicated test class:

- **Activated abilities:** cost payment verification (mana spent, creature sacrificed, self tapped), effect resolution (damage dealt, mana added, target destroyed), failure cases (can't pay cost, no valid target)
- **Triggered abilities:** trigger fires on correct event, effect executes correctly, doesn't fire on wrong events
- **Integration tests:** Sharpshooter untap chain, Pyromancer full cycle (ETB pump -> end of turn destroy), Enchantress draw on enchantment cast
- **AI bot:** activates abilities in appropriate situations

Estimated ~60-80 new tests.
