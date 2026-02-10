# MTG Rules Engine — Brainstorming Q&A

Date: 2026-02-09

## Terminology

- **v1** = First playable version (engine + tests, no UI)
- **v1.x** = Incremental additions (UI, more zones, etc.)
- **v2** = All zones fully implemented, deeper rules coverage

---

## 1. What's the end goal?

**Q:** Full game simulator, playtesting tool, or rules engine library?

**A:** Full simulator with correct rules. Will implement gradually. Starting with: all phases, the stack, priority passing, tapping lands for mana (all types), declaring attackers and blockers. Each is a whole plan in itself.

## 2. What does "playable" look like for v1?

**A:** Draw a hand, London mulligan, priority passing, tapping lands/creatures. No mana counting, life tracking, or stack. Target: ~2 weeks.

**Follow-up answers:**

- **Zones for v1:** Library, Hand, Battlefield, Graveyard — modeled properly but barebones.
- **Phases:** Untap → Upkeep → Draw → Main1 → Combat → Main2 → End. No combat substeps.
- **Turn structure:** Untap stuff, draw card, tap/untap permanents, put cards from hand to battlefield (no cost), tap creatures for attacking, pass turn. Players manage manually.
- **Mulligan:** Yes, London mulligan requires library + hand zones from v1.
- **Creatures:** Players can move cards from hand to battlefield and tap creatures (manual attacking).

## 3. Card abilities?

**A:** Deferred until stack is implemented.

## 4. Architecture

**A:** Same repo, new projects (`MtgDecker.Engine`, `MtgDecker.Engine.Tests`). Engine does NOT depend on Domain/Infrastructure. Own lightweight card representation.

- v1 card: name, image URL, tapped/untapped, type line. **Confirmed.**
- Translation: MtgDecker Card entities → GameCard objects → engine. **Confirmed.**

## 5. Card definitions for engine?

**A:** Deferred until past v1.

## 6. UI?

**A:** Engine-first. No UI for v1. New Blazor project later (v1.1+).

## 7. Formats?

**A:** 1v1 only. Legacy/Modern/Premodern later. Irrelevant until past v1.

## 8. Rules correctness?

**A:** Question for much later.

## 9. Mana?

**A:** v1 = tap lands manually, no cost checking. Full mana system later.

## 10 & 11. Claude AI?

**A:** Deferred.

## 12. Event sourcing?

**A:** Light logging now (`List<string>` game log). Architecture ready for full command sourcing (all mutations through engine mediator). Replay/serialization built later.

## 13. Async/await?

**A:** Yes — async engine from the start. `IPlayerDecisionHandler.GetAction()` is `async Task<GameAction>`. Works for tests, console, Blazor, and AI. **Confirmed.**

## 14. Testing strategy?

**A:**
- xUnit + FluentAssertions + NSubstitute (same as existing project)
- `TestDecisionHandler` with scripted action queues. **Confirmed.**
- v1: Unit tests at **component level** (TurnStateMachine, PrioritySystem individually)
- Post-v1: Integration tests (multi-turn scenarios) and GameEngine-level tests
- Post-v2: Both component and integration

## 15. Timeline?

**A:** v1 in ~2 weeks (engine + tests). Full engine = years, incremental approach.

## 16. Zones?

**A:** Zones = MTG zones (library, hand, battlefield, graveyard, stack, exile, command). Model properly but barebones for v1. v1 has 4 zones: Library, Hand, Battlefield, Graveyard. Full zones by v2.

---

## Priority System (from follow-ups)

- **v1:** Full priority — active player clicks "go" to pass priority, advances to next phase. End-turn button available. No auto-pass.
- Players can play cards (including instants) from hand at any time — manual enforcement between friends.
- Auto-pass and shortcut modes deferred to later versions.

---

## Final Clarifications (Round 3)

**A. Card play mechanics:** Option 2 — active-player-during-priority only. Only the player with priority can take actions (play cards, tap permanents). Opponent acts when they receive priority. This is closer to real MTG and sets up properly for v1.x stack implementation.

**B. Combat in v1:** Purely manual/verbal. Players verbally declare blocks, manually move cards to match, manually track combat damage and life points, manually choose attacking targets. The engine just provides the Combat phase in the sequence and grants priority during it.

**C. Test deck loading:** Builder pattern — `new DeckBuilder().AddCard("Forest", 20).AddCard("Grizzly Bears", 20).Build()`. Creates lightweight GameCard objects. No dependency on MtgDecker database or Scryfall data.

---

## All Decisions Summary

| Decision | Choice |
|---|---|
| End goal | Full simulator, built incrementally |
| v1 scope | Mulligan, phases, priority, tap/untap, manual play — no stack/mana/life |
| Architecture | `MtgDecker.Engine` + `.Tests`, independent of existing layers |
| Card model | Lightweight GameCard (name, image, tapped, type line) |
| Card play | Active-player-during-priority only — opponent acts on their priority |
| Combat (v1) | Manual/verbal — engine provides phase + priority, players manage the rest |
| Test decks | Builder pattern, no database dependency |
| Zones (v1) | Library, Hand, Battlefield, Graveyard — proper but barebones |
| Phases (v1) | Untap→Upkeep→Draw→Main1→Combat→Main2→End, no combat substeps |
| Priority (v1) | Full priority with explicit pass, end-turn button |
| UI | None for v1 — engine + tests only, engine-first approach |
| State management | Mutable with light logging, all mutations through mediator |
| Game loop | Async (`async Task`) from the start |
| Testing | Component-level unit tests, TestDecisionHandler, same stack as main project |
| Card abilities | Deferred until stack implemented |
| Mana system | Deferred — v1 is manual tap only |
| AI opponent | Deferred |
| Timeline | v1 in ~2 weeks |
