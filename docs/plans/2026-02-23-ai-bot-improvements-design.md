# AI Bot Improvements Design

## Goal

Transform the AI bot from "random legal moves" to a "decent casual player" that doesn't make obvious blunders. Pure heuristic approach — no search trees or Monte Carlo.

## Current Problems

1. **Casts counterspells proactively** — Daze cast in main phase without a spell on the stack
2. **No reactive play** — always passes priority as non-active player (no counters, no instant removal)
3. **Over-taps mana** — taps all lands before casting a 1-mana spell (taps one at a time without a plan)
4. **No land priority** — plays first land in hand (City of Traitors turn 1 with basics available)
5. **Attacks blindly** — sends all creatures into unfavorable blocks
6. **Poor spell sequencing** — casts highest CMC first instead of curving out

## Architecture

All changes in `AiBotDecisionHandler.cs` and `CardDefinitions.cs`. No engine changes. No new projects or interfaces.

### 1. Card Classification — SpellRole

Add `SpellRole` enum to classify cards by when/how they should be played:

```csharp
public enum SpellRole
{
    Proactive,      // Creatures, sorceries, enchantments — play in main phase
    Counterspell,   // Only cast in response to opponent's spell on stack
    InstantRemoval, // Cast reactively (opponent's combat, or end of turn)
    CombatTrick,    // Cast during combat (pump effects)
    ManaRamp,       // Play early (mana rocks, ramp spells)
}
```

Add `SpellRole Role` to `CardDefinition`. Default: `Proactive` (safe fallback for untagged cards). Tag all existing cards in the registry with appropriate roles.

**Classification rules:**
- Counterspell, Daze, Force of Will, Pyroblast → `Counterspell`
- Lightning Bolt, Swords to Plowshares, Naturalize, Chain Lightning → `InstantRemoval` (or `Proactive` for sorcery-speed removal)
- Creatures, sorceries, enchantments, planeswalkers → `Proactive`
- Mox Diamond, Chrome Mox, Sol Ring type effects → `ManaRamp`
- Giant Growth style effects → `CombatTrick`

### 2. Smart Land Selection

Instead of "first land in hand," rank lands by priority:

```
1. Basic lands matching colors needed by spells in hand
2. Dual/pain lands (flexible mana)
3. Tap lands / filter lands
4. Utility lands (Wasteland, Rishadan Port)
5. City of Traitors / Ancient Tomb (burst mana — only when needed, never turn 1 with basics)
```

Implementation: `ChooseLandToPlay(hand, player)` method that scores each land and returns the best one.

**Scoring heuristics:**
- +10 if basic land matching a color required by spells in hand
- +8 if dual land providing needed colors
- +5 for generic mana-producing land
- -20 for City of Traitors if other lands available
- -5 for lands that deal damage on tap (Ancient Tomb) if basic available
- Consider untapped-vs-tapped: untapped lands scored higher early game

### 3. Smart Mana Tapping

**Core change:** Instead of tapping one land at a time across separate GetAction calls, the bot should:

1. Decide which spell to cast first
2. Calculate exact mana needed (colored + generic)
3. Select optimal set of lands to tap
4. Tap them in sequence, then cast

**Tap selection algorithm:**
1. For each colored requirement, find a land that produces that color
2. Prefer single-color lands for colored costs (save duals for flexibility)
3. For generic costs, tap lands that produce colors you don't need
4. Stop when mana pool covers the cost — never over-tap

**Implementation:** Add `_plannedActions` queue. When deciding to cast a spell, compute the full tap sequence + cast action, enqueue them all, and return them one at a time from GetAction.

### 4. Reactive Play

**Change the non-active player logic** from "always pass" to:

```csharp
if (gameState.ActivePlayer.Id != playerId)
{
    // Check if we should react to something on the stack
    var reaction = EvaluateReaction(player, opponent, gameState);
    if (reaction != null) return reaction;
    return GameAction.Pass(playerId);
}
```

**EvaluateReaction checks:**

#### Counterspells
When opponent has a spell on the stack (`gameState.StackCount > 0`):
- Check hand for cards with `SpellRole.Counterspell`
- Evaluate whether to counter:
  - **Hard counters** (Counterspell, Force of Will): counter if opponent's spell CMC >= 3 or would destroy our creature/permanent
  - **Soft counters** (Daze): only counter if opponent has fewer untapped lands than needed to pay the extra cost. Specifically for Daze: counter only if opponent's untapped lands count equals 0 (they're fully tapped out or nearly so)
  - **Force of Will**: only if we have a blue card to exile and the threat is significant (CMC >= 4 or would be game-changing)
- If countering: check if we can pay the counter's cost (mana or alternate cost)

#### Instant Removal
During opponent's combat (before damage) or at end of turn:
- Check hand for cards with `SpellRole.InstantRemoval`
- During combat: target opponent's attacking creature if it would deal significant damage (power >= 3) or is lethal
- End of turn: remove opponent's biggest threat creature if we have the mana

### 5. Smart Attack Evaluation

Instead of "attack with everything":

```csharp
foreach (var creature in eligibleAttackers)
{
    // Always attack if lethal
    if (totalPower >= opponent.Life) { attackWithAll; break; }

    // Check if opponent can profitably block
    var canBeBlocked = opponent.Battlefield.Creatures
        .Any(b => CanBlock(b, creature) && WouldKill(b, creature));

    // Attack if:
    // - Has evasion (flying, unblockable) and opponent can't block it
    // - Opponent has no creatures
    // - Would trade favorably (our creature survives or trades evenly)
    // - Opponent is at low life (aggressive mode below 10 life)
    if (hasEvasion || noBlockers || favorableTrade || opponentLowLife)
        selectedAttackers.Add(creature);
}
```

**Evasion check:** Flying (opponent needs flyer/reach), Mountainwalk (opponent has Mountain), protection, etc.

### 6. Smart Block Evaluation

Enhance current "only block if we kill attacker" with:

- **Lethal defense:** If unblocked damage would kill us, block even if we lose the blocker
- **Multi-block:** If one blocker can't kill the attacker but two can, consider double-blocking (already supported by engine)
- **Don't chump-block** unless damage is lethal — preserve creatures for future turns

### 7. Spell Sequencing

New priority order for proactive spells:

1. Play land first (always)
2. Cast `ManaRamp` spells (Mox Diamond, etc.)
3. Activate fetch lands (if needed for colors)
4. Cast cheapest affordable `Proactive` spell that curves well:
   - Prefer playing on-curve (3-drop on turn 3 over 2-drop on turn 3)
   - If multiple options at same CMC, prefer creatures over non-creatures (board presence)
5. Hold `Counterspell`, `InstantRemoval`, `CombatTrick` — never cast proactively

## Data Flow

```
GetAction called
  ├─ Non-active player? → EvaluateReaction() → counter/remove/pass
  └─ Active player, main phase:
       ├─ _plannedActions queue not empty? → dequeue next action
       └─ Queue empty? → Plan next sequence:
            ├─ Land to play? → ChooseLandToPlay() → enqueue PlayLand
            ├─ Fetch to activate? → enqueue ActivateFetch
            ├─ Spell to cast? → PlanSpellCast():
            │    ├─ Pick best Proactive spell (sequencing rules)
            │    ├─ Calculate optimal tap set
            │    └─ Enqueue: [TapCard...] + CastSpell
            ├─ Ability to activate? → EvaluateActivatedAbilities()
            ├─ Card to cycle? → enqueue Cycle
            └─ Nothing? → Pass
```

## Testing Strategy

Each improvement area gets its own test class:
- `AiBotLandSelectionTests` — verifies land priority ranking
- `AiBotManaTappingTests` — verifies minimum tapping, correct color sources
- `AiBotReactivePlayTests` — verifies counterspell/removal timing
- `AiBotCombatTests` — verifies smart attack/block decisions
- `AiBotSpellSequencingTests` — verifies casting order

Tests use `TestDecisionHandler` for opponent + real `AiBotDecisionHandler` for the bot, with crafted board states.

## What We're NOT Doing

- No lookahead / search trees
- No opponent hand modeling
- No complex threat scoring beyond P/T comparison
- No changes to BoardEvaluator (simulation scoring stays as-is)
- No engine interface changes
- No new decision handler methods
