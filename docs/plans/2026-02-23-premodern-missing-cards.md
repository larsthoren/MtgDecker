# Premodern Missing Cards Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement the 88 missing cards from the 16 Premodern deck lists so every deck is fully playable. (Whipcorder deferred — requires morph subsystem.)

**Architecture:** Each card gets a CardDefinition entry in CardDefinitions.cs. Cards needing new effects get a SpellEffect or IEffect class. Cards needing new engine mechanics get those mechanics first. TDD throughout — every card gets at least one test verifying its core behavior. Implementation agents MUST verify each card's Oracle text against Scryfall API before implementing.

**Tech Stack:** C# 14, .NET 10, xUnit + FluentAssertions, MtgDecker.Engine

---

## Key Design Decisions

1. **Multiple activated abilities per card**: Change `CardDefinition.ActivatedAbility` from `ActivatedAbility?` to `IReadOnlyList<ActivatedAbility>`. Update all existing cards and the execution logic in GameEngine.cs. This is prerequisite work in Task 0.
2. **Portent delayed draw**: Implement properly with a delayed trigger that draws at the beginning of the next turn's upkeep.
3. **Whipcorder**: SKIP entirely (morph is a huge subsystem). Total cards: 88 instead of 89.
4. **Regeneration**: Implement properly — {G}: create regeneration shield. When would be destroyed, instead: tap, remove from combat, remove all damage.
5. **Carpet of Flowers**: Implement properly with main phase mana trigger tracking "used this turn".

---

## Scryfall-Verified Card Reference

Every card below was verified against the Scryfall API on 2026-02-23. Implementation agents MUST re-verify any card where they are uncertain about Oracle text by querying: `https://api.scryfall.com/cards/named?exact=<card-name>`

---

## Task Overview

| Task | Theme | Cards | New Engine Work |
|------|-------|-------|-----------------|
| 0 | Multiple Activated Abilities Refactor | 0 | Change ActivatedAbility to list, update all existing cards + engine |
| 1 | Lands + Vanilla Creatures | 2 | None |
| 2 | Simple Removal Spells | 10 | 3 new SpellEffects |
| 3 | Simple Draw/Filter Spells | 8 | 4 new SpellEffects |
| 4 | Simple Enchantments (Static) | 8 | 2 new ContinuousEffects |
| 5 | Creatures with Activated Abilities | 7 | 3 new IEffects (Whipcorder removed) |
| 6 | Enchantments with Triggers | 10 | 5 new IEffects |
| 7 | Alternate Cost Spells | 6 | AlternateCost extensions |
| 8 | Flashback + Echo Cards | 5 | None (patterns exist) |
| 9 | Shadow Keyword + Soltari | 5 | Shadow keyword + ProtectionFromBlack/Red |
| 10 | Choose Name/Type + Hate Cards | 4 | ChooseCreatureType/ChooseName on IPlayerDecisionHandler |
| 11 | Madness Mechanic + Enablers | 5 | Madness subsystem |
| 12 | Remaining Complex Cards | 18 | Kicker, Storm, Stifle, Regeneration, CoP, Split card, Rebel search, Plainscycling |

---

### Task 1: Lands + Vanilla Creatures

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/CardDefinitionTests.cs` (or appropriate test file)

**Cards (2):**

| Card | Cost | Type | P/T | Oracle | Decks |
|------|------|------|-----|--------|-------|
| Yavimaya Coast | — | Land | — | {T}: Add {C}. {T}: Add {G} or {U}. Deals 1 damage to you. | 04-madness |
| Savannah Lions | {W} | Creature — Cat | 2/1 | *(vanilla)* | 08-white-weenie |

**Implementation notes:**
- Yavimaya Coast: Use `ManaAbility.Pain([ManaColor.Green, ManaColor.Blue])` — same pattern as Karplusan Forest, Llanowar Wastes, etc.
- Savannah Lions: Vanilla creature, just ManaCost + P/T + Subtypes.

**Step 1:** Write tests verifying Yavimaya Coast produces G or U mana with 1 damage, and Savannah Lions is a 2/1 for {W}.
**Step 2:** Run tests, confirm failure.
**Step 3:** Add CardDefinition entries.
**Step 4:** Run tests, confirm pass.
**Step 5:** Commit: `feat(engine): add Yavimaya Coast and Savannah Lions`

---

### Task 0: Multiple Activated Abilities Refactor

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinition.cs` (change `ActivatedAbility?` → `IReadOnlyList<ActivatedAbility>`)
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (update all code that reads ActivatedAbility — action menu, execution, AI)
- Modify: `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs` (update AI ability selection)
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs` (update all existing cards from `ActivatedAbility = X` to `ActivatedAbilities = [X]`)
- Modify: `src/MtgDecker.Engine/GameCard.cs` (if it references ActivatedAbility)
- Test: `tests/MtgDecker.Engine.Tests/` (existing tests must pass — refactor only, no behavior change)

**Goal:** Change `CardDefinition.ActivatedAbility` from a single optional value to a list, so cards can have unlimited activated abilities (e.g., Thornscape Apprentice has 2, future cards may have more).

**Step 1:** Rename `ActivatedAbility` to `ActivatedAbilities` (type `IReadOnlyList<ActivatedAbility>`, default `[]`).
**Step 2:** Find ALL references to `ActivatedAbility` in the engine codebase and update them to iterate the list. Key locations:
- GameEngine action menu generation (show all abilities)
- GameEngine ability execution (player chooses which ability)
- AiBotDecisionHandler ability selection
- Any test helpers
**Step 3:** Update ALL existing CardDefinition entries from `ActivatedAbility = new(...)` to `ActivatedAbilities = [new(...)]`.
**Step 4:** Run full test suite — all 1248 tests must pass. No behavior change, pure refactor.
**Step 5:** Commit: `refactor(engine): change ActivatedAbility to list for multi-ability support`

---

### Task 2: Simple Removal Spells

**Files:**
- Create: `src/MtgDecker.Engine/Effects/BlueElementalBlastEffect.cs`
- Create: `src/MtgDecker.Engine/Effects/ExileEnchantmentEffect.cs`
- Create: `src/MtgDecker.Engine/Effects/DestroyAllColorEffect.cs`
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/`

**Cards (10):**

| Card | Cost | Type | Oracle (verified) | Decks |
|------|------|------|-------------------|-------|
| Red Elemental Blast | {R} | Instant | Choose one — Counter target blue spell. Destroy target blue permanent. | 01,02,06,07 |
| Blue Elemental Blast | {U} | Instant | Choose one — Counter target red spell. Destroy target red permanent. | 04,10,12,16 |
| Hydroblast | {U} | Instant | Choose one — Counter target spell if it's red. Destroy target permanent if it's red. | 10,14 |
| Annul | {U} | Instant | Counter target artifact or enchantment spell. | 04,10,11,14 |
| Erase | {W} | Instant | Exile target enchantment. | 10 |
| Perish | {2}{B} | Sorcery | Destroy all green creatures. They can't be regenerated. | 09 |
| Anarchy | {2}{R}{R} | Sorcery | Destroy all white permanents. | 01 |
| Simoon | {R}{G} | Instant | Simoon deals 1 damage to each creature target opponent controls. | 06 |
| Crumble | {G} | Instant | Destroy target artifact. It can't be regenerated. That artifact's controller gains life equal to its mana value. | 05 |
| Tranquil Domain | {1}{G} | Instant | Destroy all non-Aura enchantments. | 02,04,05,06 |

**Implementation notes:**
- **Red/Blue Elemental Blast**: PyroblastEffect already handles "counter blue OR destroy blue". Red Elemental Blast is functionally identical to Pyroblast. Blue Elemental Blast needs the same pattern but for red. Hydroblast is functionally the same as Blue Elemental Blast (the "if it's red" vs "target red" difference is cosmetic in the engine — both check color). Reuse PyroblastEffect for REB. Create a `BlueElementalBlastEffect` for BEB/Hydroblast (same pattern reversed).
- **Annul**: Use CounterSpellEffect with a TargetFilter that checks CardType.Artifact | CardType.Enchantment.
- **Erase**: New `ExileEnchantmentEffect` — like NaturalizeEffect but exiles instead of destroys.
- **Perish**: New `DestroyAllColorEffect(ManaColor color, CardType type)` — destroys all permanents of a color. Use for Perish (green creatures) and Anarchy (white permanents).
- **Simoon**: DamageAllCreaturesEffect already exists — check if it supports "opponent only" targeting. May need a variant or parameter.
- **Crumble**: Target artifact, destroy, controller gains life = CMC. New effect or combine existing.
- **Tranquil Domain**: Destroy all enchantments that aren't Auras. New mass-destroy effect with filter.

**Step 1:** Write tests for each card's core behavior.
**Step 2:** Run tests, confirm failure.
**Step 3:** Create new effect classes, add CardDefinition entries.
**Step 4:** Run tests, confirm pass.
**Step 5:** Commit: `feat(engine): add 10 simple removal spells (REB, BEB, Annul, Erase, Perish, Anarchy, Simoon, Crumble, Tranquil Domain)`

---

### Task 3: Simple Draw/Filter Spells

**Files:**
- Create: `src/MtgDecker.Engine/Effects/CarefulStudyEffect.cs`
- Create: `src/MtgDecker.Engine/Effects/PeekEffect.cs`
- Create: `src/MtgDecker.Engine/Effects/AccumulatedKnowledgeEffect.cs`
- Create: `src/MtgDecker.Engine/Effects/PortentEffect.cs`
- Create: `src/MtgDecker.Engine/Effects/EnlightenedTutorEffect.cs`
- Create: `src/MtgDecker.Engine/Effects/FranticSearchEffect.cs`
- Create: `src/MtgDecker.Engine/Effects/PriceOfProgressEffect.cs`
- Create: `src/MtgDecker.Engine/Effects/EarthquakeEffect.cs`
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/`

**Cards (8):**

| Card | Cost | Type | Oracle (verified) | Decks |
|------|------|------|-------------------|-------|
| Careful Study | {U} | Sorcery | Draw two cards, then discard two cards. | 04 |
| Peek | {U} | Instant | Look at target player's hand. Draw a card. | 14 |
| Accumulated Knowledge | {1}{U} | Instant | Draw a card, then draw cards equal to the number of cards named Accumulated Knowledge in all graveyards. | 12,14 |
| Portent | {U} | Sorcery | Look at the top three cards of target player's library, then put them back in any order. You may have that player shuffle. Draw a card at the beginning of the next turn's upkeep. | 14 |
| Enlightened Tutor | {W} | Instant | Search your library for an artifact or enchantment card, reveal it, then shuffle and put that card on top. | 08,12 |
| Frantic Search | {2}{U} | Instant | Draw two cards, then discard two cards. Untap up to three lands. | 04,16 |
| Price of Progress | {1}{R} | Instant | Price of Progress deals damage to each player equal to twice the number of nonbasic lands that player controls. | 01 |
| Earthquake | {X}{R} | Sorcery | Earthquake deals X damage to each creature without flying and each player. | 07 |

**Implementation notes:**
- **Careful Study**: Like Deep Analysis but sorcery-speed draw 2 + discard 2. Need ChooseCard for discard selection.
- **Peek**: Look at opponent's hand (RevealCards from decision handler?), draw 1. May need a `RevealHandEffect`.
- **Accumulated Knowledge**: Count cards named "Accumulated Knowledge" in ALL graveyards (both players). Draw 1 + that count.
- **Portent**: RearrangeTopEffect(3) exists. The delayed draw ("draw at beginning of next turn's upkeep") is new — needs delayed trigger or flag. **ASK USER**: Simplify to just draw immediately? Or implement delayed draw?
- **Enlightened Tutor**: SearchLibraryToTopEffect exists for searching by CardType. Need variant that searches for artifact OR enchantment.
- **Frantic Search**: Draw 2, discard 2, then untap up to 3 lands. The "untap lands" part is new.
- **Price of Progress**: Count nonbasic lands per player, deal 2x that damage. Need to identify basic vs nonbasic lands (by checking if land subtypes contain only basic land types).
- **Earthquake**: X-cost spell. DamageNonflyingCreaturesAndPlayersEffect already exists — check if it supports variable damage. May need to pass X from the mana payment.

**X-cost note**: The engine needs to know the value of X from the mana payment. Check how Decree of Justice handles X costs — it uses DecreeOfJusticeEffect which reads the mana paid. Same pattern for Earthquake and Flash of Insight.

**Step 1:** Write tests for each card.
**Step 2:** Run tests, confirm failure.
**Step 3:** Create new effect classes, add CardDefinition entries.
**Step 4:** Run tests, confirm pass.
**Step 5:** Commit: `feat(engine): add 8 draw/filter/damage spells`

---

### Task 4: Simple Enchantments (Static Effects)

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Modify: `src/MtgDecker.Engine/ContinuousEffect.cs` (may need new ContinuousEffectType values)
- Test: `tests/MtgDecker.Engine.Tests/`

**Cards (8):**

| Card | Cost | Type | Oracle (verified) | Decks |
|------|------|------|-------------------|-------|
| Crusade | {W}{W} | Enchantment | White creatures get +1/+1. | 08 |
| Absolute Law | {1}{W} | Enchantment | All creatures have protection from red. | 08 |
| Worship | {3}{W} | Enchantment | If you control a creature, damage that would reduce your life total to less than 1 reduces it to 1 instead. | 12 |
| Sphere of Resistance | {2} | Artifact | Spells cost {1} more to cast. | 06 |
| Chill | {1}{U} | Enchantment | Red spells cost {2} more to cast. | 04 |
| Gloom | {2}{B} | Enchantment | White spells cost {3} more to cast. Activated abilities of white enchantments cost {3} more to activate. | 03,13 |
| Null Rod | {2} | Artifact | Activated abilities of artifacts can't be activated. | 12 |
| Cursed Totem | {2} | Artifact | Activated abilities of creatures can't be activated. | 06 |

**Implementation notes:**
- **Crusade**: ContinuousEffect with ModifyPowerToughness. Filter: `card.ManaCost?.ColorRequirements.ContainsKey(ManaColor.White) == true` AND card.IsCreature. Need to check how color identity works for this — Crusade affects "white creatures" meaning creatures that ARE white, not just creatures with white in cost. Check existing patterns.
- **Absolute Law**: GrantKeyword with ProtectionFromRed (new keyword needed — see Task 9). All creatures get protection from red.
- **Worship**: PreventDamageToPlayer variant — only prevents lethal damage, and only if controller controls a creature. May need new ContinuousEffectType `PreventLethalDamage` or use StateCondition on existing PreventDamageToPlayer.
- **Sphere of Resistance**: CostMod +1 for ALL spells. Uses existing ModifyCost with CostApplies = all spells.
- **Chill**: CostMod +2 for red spells. Uses existing ModifyCost with color filter.
- **Gloom**: CostMod +3 for white spells. Second ability (white enchantment activated abilities cost more) may need new engine work or can be deferred.
- **Null Rod**: New ContinuousEffectType `PreventArtifactActivation` — prevents activated abilities on artifacts. Check how existing ActivatedAbility execution works and where to intercept.
- **Cursed Totem**: Same pattern as Null Rod but for creatures.

**New Keyword enum values needed**: `ProtectionFromRed`, `ProtectionFromBlack` (for Task 9 Soltari cards too). Add them in this task since Absolute Law needs ProtectionFromRed.

**New ContinuousEffectType values potentially needed**: `PreventActivatedAbilities` (for Null Rod + Cursed Totem)

**Step 1:** Write tests for each card's static effect.
**Step 2:** Run tests, confirm failure.
**Step 3:** Add new Keywords, ContinuousEffectType values, CardDefinition entries.
**Step 4:** Run tests, confirm pass.
**Step 5:** Commit: `feat(engine): add 8 static enchantments/artifacts (Crusade, Sphere, Chill, etc.)`

---

### Task 5: Creatures with Activated Abilities

**Files:**
- Create: new IEffect classes as needed
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/`

**Cards (7):** *(Whipcorder skipped — requires morph subsystem)*

| Card | Cost | Type | P/T | Oracle (verified) | Decks |
|------|------|------|-----|-------------------|-------|
| True Believer | {W}{W} | Creature — Human Cleric | 2/2 | You have shroud. | 08 |
| Nova Cleric | {W} | Creature — Human Cleric | 1/2 | {2}{W}, {T}, Sacrifice: Destroy all enchantments. | 08 |
| Thornscape Apprentice | {G} | Creature — Human Wizard | 1/1 | {R}, {T}: Target creature gains first strike until end of turn. {W}, {T}: Tap target creature. | 11 |
| Waterfront Bouncer | {1}{U} | Creature — Merfolk Spellshaper | 1/1 | {U}, {T}, Discard a card: Return target creature to its owner's hand. | 04 |
| Wild Mongrel | {1}{G} | Creature — Dog | 2/2 | Discard a card: +1/+1 and becomes color of your choice until end of turn. | 04,07 |
| Aquamoeba | {1}{U} | Creature — Elemental Beast | 1/3 | Discard a card: Switch power and toughness until end of turn. | 04 |
| Flametongue Kavu | {3}{R} | Creature — Kavu | 4/2 | When this creature enters, it deals 4 damage to target creature. | 07 |

**Implementation notes:**
- **True Believer**: GrantPlayerShroud continuous effect — pattern already exists on the engine.
- **Nova Cleric**: ActivatedAbilities with ManaCost {2}{W}, TapSelf, SacrificeSelf. Effect: destroy all enchantments (new effect — DestroyAllEnchantmentsEffect).
- **Thornscape Apprentice**: TWO activated abilities — now supported after Task 0 refactor. Ability 1: {R}, {T} → grant first strike until EOT. Ability 2: {W}, {T} → tap target creature.
- **Waterfront Bouncer**: ActivatedAbilities with ManaCost {U}, TapSelf, DiscardCardType: null (any card). Effect: BounceTargetEffect targeting creature.
- **Wild Mongrel**: ActivatedAbilities with no mana cost, just discard. Effect: PumpSelfEffect +1/+1 until EOT. The "becomes color of your choice" part is cosmetic for our engine (color doesn't affect gameplay much outside protection). Simplify to just +1/+1.
- **Aquamoeba**: ActivatedAbilities with no mana cost, just discard. Effect: swap P/T until EOT. Needs new `SwapPowerToughnessEffect`.
- **Flametongue Kavu**: ETB trigger dealing 4 damage to target creature. Uses existing DealDamageEffect pattern with target selection.

**Step 1:** Write tests for each creature's key ability.
**Step 2:** Run tests, confirm failure.
**Step 3:** Create new effect classes, add CardDefinition entries.
**Step 4:** Run tests, confirm pass.
**Step 5:** Commit: `feat(engine): add 8 creatures with activated abilities`

---

### Task 6: Enchantments with Triggers

**Files:**
- Create: new IEffect classes as needed
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Modify: `src/MtgDecker.Engine/Triggers/TriggerCondition.cs` (new conditions)
- Modify: `src/MtgDecker.Engine/Enums/GameEvent.cs` (if needed)
- Test: `tests/MtgDecker.Engine.Tests/`

**Cards (10):**

| Card | Cost | Type | Oracle (verified) | Decks |
|------|------|------|-------------------|-------|
| Warmth | {1}{W} | Enchantment | Whenever an opponent casts a red spell, you gain 2 life. | 09,12 |
| Spiritual Focus | {1}{W} | Enchantment | Whenever a spell or ability an opponent controls causes you to discard a card, you gain 2 life and you may draw a card. | 12,16 |
| Presence of the Master | {3}{W} | Enchantment | Whenever a player casts an enchantment spell, counter it. | 09,14 |
| Sacred Ground | {1}{W} | Enchantment | Whenever a spell or ability an opponent controls causes a land to be put into your graveyard from the battlefield, return that card to the battlefield. | 15 |
| Seal of Fire | {R} | Enchantment | Sacrifice: It deals 2 damage to any target. | 07 |
| Ivory Tower | {1} | Artifact | At the beginning of your upkeep, you gain X life, where X is the number of cards in your hand minus 4. | 12 |
| Rejuvenation Chamber | {3} | Artifact | Fading 2. {T}: You gain 2 life. | 13 |
| Serenity | {1}{W} | Enchantment | At the beginning of your upkeep, destroy all artifacts and enchantments. They can't be regenerated. | 12 |
| Carpet of Flowers | {G} | Enchantment | At the beginning of each of your main phases, if you haven't added mana with this ability this turn, you may add X mana of any one color, where X is the number of Islands target opponent controls. | 15 |
| Zombie Infestation | {1}{B} | Enchantment | Discard two cards: Create a 2/2 black Zombie creature token. | 03 |

**Implementation notes:**
- **Warmth**: Trigger on opponent casting red spell → gain 2 life. Needs TriggerCondition: `OpponentCastsRedSpell` or use `AnyPlayerCastsSpell` with color filter in the effect.
- **Spiritual Focus**: Trigger when opponent's effect causes controller to discard → gain 2 + may draw. Complex trigger source tracking. May simplify to: trigger whenever controller discards a card (without checking the source).
- **Presence of the Master**: Trigger on any enchantment spell → counter it. Uses existing counter pattern.
- **Sacred Ground**: Trigger when opponent's spell/ability puts your land in GY → return to battlefield. Complex trigger source tracking.
- **Seal of Fire**: ActivatedAbility with SacrificeSelf, DealDamageEffect(2). Target: any target (creature or player). CanTargetPlayer = true.
- **Ivory Tower**: Upkeep trigger → gain max(0, hand size - 4) life. Needs IvoryTowerEffect.
- **Rejuvenation Chamber**: Fading 2 (pattern from Parallax Wave) + ActivatedAbility tap: gain 2 life.
- **Serenity**: Upkeep trigger → destroy all artifacts and enchantments. New SerenityEffect.
- **Carpet of Flowers**: Main phase mana generation based on opponent's Island count. Implement properly with main phase trigger + "used this turn" tracking. Needs new GameEvent (e.g., `MainPhaseBegin`) or hook into phase transitions. Track usage via GameCard flag reset at turn start.
- **Zombie Infestation**: ActivatedAbility — cost: discard 2 cards (no mana, no tap). Effect: CreateTokenSpellEffect for 2/2 Zombie. Need ActivatedAbilityCost with DiscardCount = 2.

**Engine limitation — ActivatedAbilityCost DiscardCount**: Current ActivatedAbilityCost only has `DiscardCardType` (for typed discard like sacrifice). Zombie Infestation needs "discard 2 cards of any type". Check if existing pattern handles this.

**Step 1-5:** Standard TDD cycle + commit.

---

### Task 7: Alternate Cost Spells

**Files:**
- Modify: `src/MtgDecker.Engine/AlternateCost.cs` (extend for new patterns)
- Create: new SpellEffect classes as needed
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (if AlternateCost needs new payment logic)
- Test: `tests/MtgDecker.Engine.Tests/`

**Cards (6):**

| Card | Cost | Type | Oracle (verified) | Decks |
|------|------|------|-------------------|-------|
| Gush | {4}{U} | Instant | You may return two Islands you control to their owner's hand rather than pay this spell's mana cost. Draw two cards. | 04,14 |
| Foil | {2}{U}{U} | Instant | You may discard an Island card and another card rather than pay this spell's mana cost. Counter target spell. | 04,14 |
| Spinning Darkness | {4}{B}{B} | Instant | You may exile the top three black cards of your graveyard rather than pay this spell's mana cost. Deals 3 damage to target nonblack creature. You gain 3 life. | 03 |
| Mogg Salvage | {2}{R} | Instant | If an opponent controls an Island and you control a Mountain, you may cast this spell without paying its mana cost. Destroy target artifact. | 02 |
| Pyrokinesis | {4}{R}{R} | Instant | You may exile a red card from your hand rather than pay this spell's mana cost. Deals 4 damage divided as you choose among any number of target creatures. | 02 |
| Gaea's Blessing | {1}{G} | Sorcery | Target player shuffles up to three target cards from their graveyard into their library. Draw a card. When this card is put into your graveyard from your library, shuffle your graveyard into your library. | 08 |

**Implementation notes:**
- **Gush**: AlternateCost needs `ReturnLandSubtype = "Island"` with count 2. Existing AlternateCost only supports returning 1 land (via `ReturnLandSubtype`). **Need to add `ReturnLandCount` to AlternateCost** (default 1 for Daze, 2 for Gush). Effect: DrawCardsEffect(2).
- **Foil**: Alt cost: discard Island card + discard another card. Existing AlternateCost doesn't support "discard specific + discard any". **Need to extend AlternateCost** with `DiscardLandSubtype` + `DiscardAnyCount`. Effect: CounterSpellEffect.
- **Spinning Darkness**: Alt cost: exile top 3 black cards from graveyard. New alt cost pattern — **ExileFromGraveyardCount + ExileFromGraveyardColor**. Need to extend AlternateCost. Effect: deal 3 to nonblack creature + gain 3 life.
- **Mogg Salvage**: Conditional free cast: if opponent has Island AND you have Mountain, cost = 0. This is a condition-based cost reduction, not exactly AlternateCost. Could model as AlternateCost with `RequiresControlSubtype = "Mountain"` + new `RequiresOpponentSubtype = "Island"` + zero cost.
- **Pyrokinesis**: Alt cost: exile red card from hand. Same as Force of Will pattern (`ExileCardColor = ManaColor.Red`). But the effect is "4 damage divided" — needs DividedDamageEffect.
- **Gaea's Blessing**: Normal effect: shuffle up to 3 from GY to library + draw. Self-trigger: when milled, shuffle GY into library. Needs new GraveyardAbility or trigger on mill event.

**AlternateCost extensions needed:**
```csharp
public record AlternateCost(
    int LifeCost = 0,
    ManaColor? ExileCardColor = null,
    string? ReturnLandSubtype = null,
    int ReturnLandCount = 1,              // NEW: for Gush (2 Islands)
    string? SacrificeLandSubtype = null,
    int SacrificeLandCount = 0,
    string? RequiresControlSubtype = null,
    string? RequiresOpponentSubtype = null,  // NEW: for Mogg Salvage
    int DiscardAnyCount = 0,              // NEW: for Foil
    string? DiscardLandSubtype = null,    // NEW: for Foil (discard Island)
    int ExileFromGraveyardCount = 0,      // NEW: for Spinning Darkness
    ManaColor? ExileFromGraveyardColor = null); // NEW: for Spinning Darkness
```

**Step 1-5:** Standard TDD cycle. Test alternate cost payment paths thoroughly.

---

### Task 8: Flashback + Echo Cards

**Files:**
- Create: new SpellEffect classes as needed
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/`

**Cards (5):**

| Card | Cost | Type | Oracle (verified) | Decks |
|------|------|------|-------------------|-------|
| Roar of the Wurm | {6}{G} | Sorcery | Create a 6/6 green Wurm creature token. Flashback {3}{G}. | 04 |
| Krosan Reclamation | {1}{G} | Instant | Target player shuffles up to two target cards from their graveyard into their library. Flashback {1}{G}. | 11 |
| Flash of Insight | {X}{1}{U} | Instant | Look at the top X cards. Put one into your hand, rest on bottom. Flashback—{1}{U}, Exile X blue cards from your graveyard. | 14 |
| Radiant's Dragoons | {3}{W} | Creature — Human Soldier | 2/5. Echo {3}{W}. ETB: gain 5 life. | 09 |
| Attunement | {2}{U} | Enchantment | Return to owner's hand: Draw three cards, then discard four cards. | 16 |

**Implementation notes:**
- **Roar of the Wurm**: FlashbackCost(ManaCost.Parse("{3}{G}")). Effect: CreateTokenSpellEffect for 6/6 green Wurm.
- **Krosan Reclamation**: FlashbackCost(ManaCost.Parse("{1}{G}")). Effect: shuffle up to 2 from GY to library. New `ShuffleFromGraveyardEffect`.
- **Flash of Insight**: X-cost + flashback with exile blue cards. FlashbackCost currently only supports ManaCost, LifeCost, SacrificeCreature. **Need to extend FlashbackCost** to support exile-from-graveyard. Effect: ImpulseEffect variant for X cards.
- **Radiant's Dragoons**: EchoCost(ManaCost.Parse("{3}{W}")). ETB trigger: GainLifeEffect(5). Echo pattern already exists.
- **Attunement**: ActivatedAbility — cost: return self to hand (not sacrifice). Effect: draw 3, discard 4. Returning self to hand as a cost is new — **check if ActivatedAbilityCost can handle "return self to hand"**. May need `ReturnSelfToHand = true`.

**FlashbackCost extension for Flash of Insight:**
```csharp
public record FlashbackCost(
    ManaCost? ManaCost = null,
    int LifeCost = 0,
    bool SacrificeCreature = false,
    int ExileBlueCardsFromGraveyard = 0);  // NEW: for Flash of Insight
```

**Step 1-5:** Standard TDD cycle.

---

### Task 9: Shadow Keyword + Soltari Creatures + Protection Colors

**Files:**
- Modify: `src/MtgDecker.Engine/Enums/Keyword.cs` (add Shadow, ProtectionFromBlack, ProtectionFromRed)
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (enforce Shadow in combat blocking, enforce new protection keywords)
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/`

**Cards (5):**

| Card | Cost | Type | P/T | Oracle (verified) | Decks |
|------|------|------|-----|-------------------|-------|
| Soltari Foot Soldier | {W} | Creature — Soltari Soldier | 1/1 | Shadow | 08 |
| Soltari Monk | {W}{W} | Creature — Soltari Monk Cleric | 2/1 | Protection from black. Shadow. | 08 |
| Soltari Priest | {W}{W} | Creature — Soltari Cleric | 2/1 | Protection from red. Shadow. | 08 |
| Soltari Champion | {2}{W} | Creature — Soltari Soldier | 2/2 | Shadow. Whenever attacks, other creatures you control get +1/+1 until EOT. | 08 |
| Xantid Swarm | {G} | Creature — Insect | 0/1 | Flying. Whenever attacks, defending player can't cast spells this turn. | 15 |

**New keywords:**
```csharp
Shadow,           // Can only block/be blocked by creatures with shadow
ProtectionFromBlack,
ProtectionFromRed,
```

**Shadow enforcement in combat** (GameEngine.cs, in ChooseBlockers validation):
```csharp
// Shadow: can only block or be blocked by creatures with shadow
if (attackerCard.ActiveKeywords.Contains(Keyword.Shadow)
    && !blockerCard.ActiveKeywords.Contains(Keyword.Shadow))
{
    _state.Log($"{blockerCard.Name} cannot block {attackerCard.Name} (shadow).");
    continue;
}
if (!attackerCard.ActiveKeywords.Contains(Keyword.Shadow)
    && blockerCard.ActiveKeywords.Contains(Keyword.Shadow))
{
    _state.Log($"{blockerCard.Name} cannot block {attackerCard.Name} (shadow can only block shadow).");
    continue;
}
```

**Protection enforcement**: Protection from a color prevents:
- Being blocked by creatures of that color
- Being targeted by spells of that color
- Being dealt damage by sources of that color
- Being enchanted/equipped by things of that color

For our engine, enforce at minimum: blocking prevention. Damage prevention and targeting can be added later.

**Soltari Champion**: Attack trigger → PumpAllOtherCreaturesEffect (+1/+1 until EOT to other creatures you control). New IEffect.

**Xantid Swarm**: Attack trigger → defender can't cast spells this turn. New ContinuousEffect.

**Step 1-5:** Standard TDD cycle. Heavy focus on combat blocking tests with shadow/protection.

---

### Task 10: Choose Name/Type + Hate Cards

**Files:**
- Modify: `src/MtgDecker.Engine/IPlayerDecisionHandler.cs` (add ChooseCreatureType, ChooseCardName)
- Modify: `src/MtgDecker.Engine/InteractiveDecisionHandler.cs`
- Modify: `src/MtgDecker.Engine/AI/AiBotDecisionHandler.cs`
- Modify: `src/MtgDecker.Engine/TestDecisionHandler.cs`
- Create: new effect/continuous effect classes
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/`

**Cards (4):**

| Card | Cost | Type | P/T | Oracle (verified) | Decks |
|------|------|------|-----|-------------------|-------|
| Engineered Plague | {2}{B} | Enchantment | — | As enters, choose creature type. All creatures of chosen type get -1/-1. | 03,09,13 |
| Meddling Mage | {W}{U} | Creature — Human Wizard | 2/2 | As enters, choose a nonland card name. Spells with chosen name can't be cast. | 10,11,12,14 |
| Ensnaring Bridge | {3} | Artifact | — | Creatures with power greater than the number of cards in your hand can't attack. | 13 |
| Tsabo's Web | {2} | Artifact | — | ETB: draw a card. Lands with activated abilities that aren't mana abilities don't untap during their controller's untap step. | 08,12,15,16 |

**New IPlayerDecisionHandler methods:**
```csharp
Task<string> ChooseCreatureType(string prompt, CancellationToken ct = default);
Task<string> ChooseCardName(string prompt, CancellationToken ct = default);
```

**Implementation notes:**
- **Engineered Plague**: ETB → ChooseCreatureType → store chosen type on GameCard (new property `ChosenType`?). ContinuousEffect: all creatures with that subtype get -1/-1. Check if creatures dying from -1/-1 triggers SBA.
- **Meddling Mage**: ETB → ChooseCardName → store on GameCard (`ChosenName`). ContinuousEffect or check in CastSpell: prevent casting spells with that name.
- **Ensnaring Bridge**: ContinuousEffect that modifies attack eligibility. Need to hook into GetEligibleAttackers or the attack declaration phase.
- **Tsabo's Web**: ETB → draw 1 (simple trigger). Static effect: lands with non-mana activated abilities don't untap. Needs DoesNotUntap variant with condition.

**GameCard extensions needed:**
```csharp
public string? ChosenType { get; set; }  // For Engineered Plague
public string? ChosenName { get; set; }  // For Meddling Mage
```

**Step 1-5:** Standard TDD cycle. Test that Meddling Mage prevents casting named card, Engineered Plague weakens typed creatures.

---

### Task 11: Madness Mechanic + Enablers

**Files:**
- Modify: `src/MtgDecker.Engine/CardDefinition.cs` (add MadnessCost)
- Modify: `src/MtgDecker.Engine/GameEngine.cs` (modify discard flow for madness)
- Modify: `src/MtgDecker.Engine/IPlayerDecisionHandler.cs` (add ChooseMadness)
- Modify: all decision handler implementations
- Create: new SpellEffect for Circular Logic
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/`

**Cards (5):**

| Card | Cost | Type | P/T | Oracle (verified) | Decks |
|------|------|------|-----|-------------------|-------|
| Basking Rootwalla | {G} | Creature — Lizard | 1/1 | {1}{G}: +2/+2 until EOT. Activate once/turn. Madness {0}. | 04 |
| Arrogant Wurm | {3}{G}{G} | Creature — Wurm | 4/4 | Trample. Madness {2}{G}. | 04 |
| Circular Logic | {2}{U} | Instant | Counter unless pay {1} per card in your graveyard. Madness {U}. | 04 |
| Wild Mongrel | (already in Task 5) | | | *(madness enabler via discard)* | |
| Aquamoeba | (already in Task 5) | | | *(madness enabler via discard)* | |

**Madness mechanic overview:**
When you discard a card with madness, instead of putting it into your graveyard, you exile it. Then you may cast it for its madness cost. If you don't, it goes to your graveyard.

**CardDefinition extension:**
```csharp
public ManaCost? MadnessCost { get; init; }  // If non-null, card has madness
```

**Engine changes (GameEngine.cs):**
Wherever a card is discarded (multiple locations), check if it has madness:
1. Move card to exile instead of graveyard
2. Prompt player: "Cast [card] for madness cost [cost]?"
3. If yes: cast from exile paying madness cost
4. If no: move from exile to graveyard

**Decision handler:**
```csharp
Task<bool> ChooseMadness(GameCard card, ManaCost madnessCost, CancellationToken ct = default);
```

**Circular Logic**: ConditionalCounterEffect — counter unless they pay {1} for each card in caster's graveyard. Similar to Mana Leak but dynamic amount based on GY size.

**This is the most complex new mechanic.** Implementation must carefully handle:
- Multiple discard effects (Careful Study, Wild Mongrel, Aquamoeba, opponent's discard)
- The exile → cast or GY flow
- Madness cost payment using normal CastSpell flow
- Interaction with graveyard counts (e.g., Circular Logic's counter amount should count the discarded card if it went to GY)

**Step 1-5:** Standard TDD cycle. Test: discard Basking Rootwalla to Wild Mongrel → cast for {0} → enters battlefield.

---

### Task 12: Remaining Complex Cards

This task covers cards that each need unique engine work. Some of these may require simplification decisions — **the implementing agent should ASK the user when uncertain about complex mechanics**.

**Files:**
- Various new effects and engine modifications
- Modify: `src/MtgDecker.Engine/CardDefinitions.cs`
- Test: `tests/MtgDecker.Engine.Tests/`

**Cards (18):**

#### 12a: Kicker (2 cards)
| Card | Cost | Type | Oracle (verified) | Decks |
|------|------|------|-------------------|-------|
| Overload | {R} | Instant | Kicker {2}. Destroy target artifact if MV ≤ 2. If kicked, MV ≤ 5 instead. | 01 |
| Orim's Chant | {W} | Instant | Kicker {W}. Target player can't cast spells this turn. If kicked, creatures can't attack this turn. | 08,16 |

**Need:** `CardDefinition.KickerCost` + CastSpell flow modification to optionally pay kicker. `GameCard.WasKicked` flag for effect to check.

#### 12b: Rancor (1 card)
| Card | Cost | Type | Oracle (verified) | Decks |
|------|------|------|-------------------|-------|
| Rancor | {G} | Enchantment — Aura | Enchant creature. +2/+0 and trample. When put into GY from battlefield, return to owner's hand. | 07 |

**Need:** Aura with GY → hand trigger. Check if GraveyardAbilities or a trigger on `Dies`/`LeavesBattlefield` event works.

#### 12c: Stifle (1 card)
| Card | Cost | Type | Oracle (verified) | Decks |
|------|------|------|-------------------|-------|
| Stifle | {U} | Instant | Counter target activated or triggered ability. (Mana abilities can't be targeted.) | 14 |

**Need:** Ability to target TriggeredAbilityStackObject on the stack. New TargetFilter for abilities. New CounterAbilityEffect.

#### 12d: Teferi's Response (1 card)
| Card | Cost | Type | Oracle (verified) | Decks |
|------|------|------|-------------------|-------|
| Teferi's Response | {1}{U} | Instant | Counter target spell or ability that targets a land you control. If a permanent's ability is countered this way, destroy that permanent. Draw two cards. | 10 |

**Need:** Counter spell/ability + conditional destroy + draw 2. Very complex targeting. May simplify to: counter any spell targeting your land + draw 2.

#### 12e: Brain Freeze / Storm (1 card)
| Card | Cost | Type | Oracle (verified) | Decks |
|------|------|------|-------------------|-------|
| Brain Freeze | {1}{U} | Instant | Target player mills three cards. Storm. | 14 |

**Need:** Storm mechanic — count spells cast this turn, copy spell that many times. Needs `GameState.SpellsCastThisTurn` counter + copy-on-stack logic. Only 1 storm card, so can implement specifically rather than generically.

#### 12f: Regeneration (1 card)
| Card | Cost | Type | P/T | Oracle (verified) | Decks |
|------|------|------|-----|-------------------|-------|
| River Boa | {1}{G} | Creature — Snake | 2/1 | Islandwalk. {G}: Regenerate. | 07 |

**Need:** Full regeneration implementation. {G}: create regeneration shield on this creature. When creature would be destroyed, instead: tap it, remove it from combat, remove all damage marked on it. The shield is consumed. Multiple shields can stack. Shields expire at end of turn. Needs:
- `GameCard.RegenerationShields` (int counter)
- Modify destroy logic in GameEngine to check for regen shields before destroying
- Clear shields at end of turn
- Activated ability: {G} → add 1 regeneration shield

#### 12g: Circle of Protection (2 cards)
| Card | Cost | Type | Oracle (verified) | Decks |
|------|------|------|-------------------|-------|
| Circle of Protection: Red | {1}{W} | Enchantment | {1}: Prevent next damage from red source this turn. | 06,08,10,15,16 |
| Circle of Protection: Black | {1}{W} | Enchantment | {1}: Prevent next damage from black source this turn. | 12 |

**Need:** Damage prevention shields — source-color-based. ActivatedAbility creates a prevention shield. Need to hook into damage resolution to check shields.

#### 12h: Split Card (1 card)
| Card | Cost | Type | Oracle (verified) | Decks |
|------|------|------|-------------------|-------|
| Assault // Battery | {R} // {3}{G} | Sorcery // Sorcery | Assault: deal 2 to any target. Battery: create 3/3 Elephant token. | 07 |

**Need:** Split card — choose which half to cast. Could model as Adventure (pattern exists for Brazen Borrower). Or new SplitCard mechanic.

#### 12i: Rebel Search (1 card)
| Card | Cost | Type | P/T | Oracle (verified) | Decks |
|------|------|------|-----|-------------------|-------|
| Ramosian Sergeant | {W} | Creature — Human Rebel | 1/1 | {3}, {T}: Search library for Rebel permanent card with MV ≤ 2, put onto battlefield, then shuffle. | 08,12 |

**Need:** SearchLibraryEffect variant — search by subtype + MV filter + put on battlefield (not hand).

#### 12j: Plainscycling + Graveyard Return (1 card)
| Card | Cost | Type | P/T | Oracle (verified) | Decks |
|------|------|------|-----|-------------------|-------|
| Eternal Dragon | {5}{W}{W} | Creature — Dragon Spirit | 5/5 | Flying. {3}{W}{W}: Return from GY to hand (upkeep only). Plainscycling {2}. | 12 |

**Need:** Plainscycling = CyclingCost but instead of draw, search for Plains. GraveyardAbility: activated ability from graveyard, upkeep-only.

#### 12k: Other Singleton Complex Cards (5 cards)
| Card | Cost | Type | Oracle (verified) | Decks |
|------|------|------|-------------------|-------|
| Phyrexian Dreadnought | {1} | Artifact Creature | 12/12 | Trample. ETB: sacrifice unless you sacrifice creatures with total power ≥ 12. | 14 |
| Decree of Silence | {6}{U}{U} | Enchantment | Counter opponent spells, depletion counters, sacrifice at 3. Cycling {4}{U}{U}: counter target spell. | 16 |
| Dystopia | {1}{B}{B} | Enchantment | Cumulative upkeep: pay 1 life. Each player's upkeep: sacrifice green/white permanent. | 03,13 |
| Cleansing Meditation | {1}{W}{W} | Sorcery | Destroy all enchantments. Threshold: return yours. | 08 |
| Wonder | {3}{U} | Creature — Incarnation | 2/2. Flying. In GY + control Island → your creatures have flying. | 04 |

**Implementation notes:**
- **Phyrexian Dreadnought**: ETB trigger → sacrifice creatures with total power ≥ 12, or sacrifice Dreadnought. Complex sacrifice-N-to-meet-threshold. Pairs with Stifle (counter the ETB trigger to keep the 12/12).
- **Decree of Silence**: EntersWithCounters Depletion = 3 (like Mining counters on Powder Keg). Trigger: opponent casts spell → counter + remove counter. When 0 counters → sacrifice. Cycling trigger: counter target spell.
- **Dystopia**: Cumulative upkeep — each upkeep, add age counter, pay 1 life per counter or sacrifice. Plus each player's upkeep: sacrifice green/white permanent. Two triggers. May simplify cumulative upkeep.
- **Cleansing Meditation**: Destroy all enchantments. If threshold (GY ≥ 7): return your enchantments from GY. Need to track which enchantments were yours.
- **Wonder**: GraveyardAbility granting flying to all your creatures (conditional on controlling Island). Pattern exists from `GraveyardAbilities` on CardDefinition.

**Kirtar's Desire** (from Task 6 overflow):
| Kirtar's Desire | {W} | Enchantment — Aura | Enchant creature. Can't attack. Threshold: can't block. | 08 |

**Need:** Aura preventing attack. Threshold conditional for blocking prevention. AuraTarget = Creature. ContinuousEffect: enchanted creature can't attack (+ can't block at threshold).

---

## Execution Order

Tasks should be executed in order 0→12 because:
- **Task 0 FIRST**: Refactors ActivatedAbility to a list — prerequisite for all tasks using activated abilities
- Tasks 1-3 are independent and add simple cards
- Task 4 adds Keywords (ProtectionFromRed) needed by Task 9
- Tasks 5-6 add creatures/enchantments using the refactored ability list
- Task 7-8 extends existing alternate cost / flashback patterns
- Task 9 adds Shadow + protection enforcement in combat
- Task 10 adds decision handler methods needed for naming effects
- Task 11 adds madness (depends on discard enablers from Task 5)
- Task 12 contains the most complex cards requiring various new mechanics

## Card Count

- **Total missing cards**: 89 (from cross-reference)
- **Skipped**: 1 (Whipcorder — morph deferred)
- **To implement**: 88 cards across 13 tasks (Task 0 + Tasks 1-12)

## Testing Strategy

- Every card gets at least ONE test verifying its core mechanic
- Engine infrastructure changes (shadow blocking, madness discard, choose-name, regeneration, multi-ability) get dedicated integration tests
- Use TestDecisionHandler for deterministic test scenarios
- Run full test suite (`dotnet test tests/MtgDecker.Engine.Tests/`) after each task
- Existing 1248 engine tests must continue passing

## Commit Strategy

One commit per task (13 total). Each commit message format:
- Task 0: `refactor(engine): change ActivatedAbility to list for multi-ability support`
- Tasks 1-12: `feat(engine): <task description> — N cards added`
