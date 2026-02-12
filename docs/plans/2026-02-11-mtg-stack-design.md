# MTG Stack Implementation Design

## Goal

Add the MTG stack mechanic to the game engine: spells go on a LIFO stack, players get response windows, instants can be cast at instant speed, and two specific instants (Swords to Plowshares, Naturalize) resolve with targeting and effects.

## Scope

- Core stack zone with StackObject representation
- LIFO resolution with priority after each cast/resolution
- Instant-speed vs sorcery-speed timing validation
- Targeting system with UI prompt and legality re-check on resolution
- SpellEffect for Swords to Plowshares and Naturalize
- Undo for unresolved casts (not for resolved spells)
- UI: target picker, stack display panel, timing error toast

## Stack Zone & Spell Objects

The stack is a shared game zone (not per-player). When a player casts a spell, a **StackObject** is created and placed on top.

**StackObject**:
- `Id` (Guid) — unique identifier
- `SourceCardId` (Guid) — the GameCard that was cast
- `ControllerId` (Guid) — the player who cast it
- `ManaCostPaid` (Dictionary<ManaColor, int>) — for undo/refund
- `Targets` (List<TargetInfo>) — what the spell targets
- `Timestamp` (int) — ordering

**GameState** gets a `Stack` property — a `List<StackObject>` acting as LIFO. Top of list = top of stack.

**Resolution**: When both players pass priority with the stack non-empty, the topmost StackObject resolves. Its effect executes, the card moves to its destination zone (Graveyard for instants/sorceries, Battlefield for permanents), and priority passes again. Repeat until stack is empty and both pass.

## Priority Changes

Current `RunPriorityAsync()` loops until both players pass, then advances the phase. With the stack:

1. After a spell is cast (placed on stack), the active player receives priority again.
2. When the active player passes, the non-active player gets priority.
3. When both pass in succession:
   - Stack non-empty → resolve top, active player gets priority again.
   - Stack empty → advance to next phase/step (current behavior).

**Instant timing**: Any time a player has priority (any phase, in response, during combat, opponent's turn).

**Sorcery timing**: Only when stack is empty, during a Main phase, and you are the active player.

**New action type**: `ActionType.CastSpell` (distinct from `PlayCard` for land drops). Land drops remain immediate and don't use the stack.

## Targeting System

When casting a targeted spell, the engine prompts for target selection before the spell goes on the stack. Uses the `TaskCompletionSource` pattern (like mana color choices).

**TargetInfo** value object:
- `CardId` (Guid) — the targeted GameCard
- `PlayerId` (Guid) — controller of the target
- `Zone` (ZoneType) — where the target is

**TargetFilter** defines legal targets per spell:
- Swords to Plowshares: any creature on the battlefield
- Naturalize: any artifact or enchantment on the battlefield

**Cast flow for targeted spells**:
1. Player clicks PLAY on an instant/sorcery
2. Engine validates timing
3. Engine validates mana
4. Engine prompts for target via `IPlayerDecisionHandler.ChooseTarget()`
5. Player selects a legal target from highlighted eligible permanents
6. Engine pays mana, creates StackObject with target info, places on stack
7. Priority passes

**On resolution**: Re-check target legality. If target is gone or no longer valid, the spell fizzles (goes to graveyard with no effect).

## Effect System

Effects are simple delegates attached to card definitions.

**SpellEffect** class with `Resolve(GameState state, StackObject spell)`.

**Swords to Plowshares**:
1. Get target creature
2. Record Power value
3. Move target from Battlefield to Exile
4. Add Power to target's controller's Life
5. Move spell card to caster's Graveyard

**Naturalize**:
1. Get target artifact/enchantment
2. Move target from Battlefield to Graveyard (destroy)
3. Move spell card to caster's Graveyard

**Permanents**: Creatures, enchantments, etc. don't need SpellEffect — they resolve by moving from stack to Battlefield (default).

**Sandbox fallback**: Cards not in CardDefinitions bypass the stack entirely and play directly to battlefield (preserved from current behavior).

## Integration with Existing Systems

**Undo**: Undo removes unresolved StackObject, refunds mana, returns card to hand. Once resolved, cannot be undone.

**Combat**: No changes. Existing combat phases grant priority, so instants work naturally during combat. Combat damage doesn't use the stack (modern rules).

**Mana**: No changes. Payment happens at cast time (already the case).

**Phase progression**: `RunPriorityAsync()` changes from "both pass → advance" to "both pass → resolve top or advance if stack empty."

**Existing tests**: CastSpellTests need updating to account for stack resolution steps. Helper like `ResolveStack()` for test readability.

## UI Changes

**Target selection**: New element in PlayerZone, like mana color picker. Eligible cards highlighted, player clicks one. Label: "Choose target for [spell name]". Ineligible cards dimmed.

**Stack display**: Small panel between player zones showing pending spells (name, caster, target). Topmost emphasized. Hidden when empty.

**Timing feedback**: Error toast when sorcery-speed cast is attempted at wrong time.

**Pass Priority**: Same button, now means "decline to respond" when stack is non-empty.

**No changes to**: Action menu, card display, game log, mana pool display, life counter, zone move buttons.
