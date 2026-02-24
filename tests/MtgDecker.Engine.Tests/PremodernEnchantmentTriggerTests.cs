using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class PremodernEnchantmentTriggerTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler p1Handler, TestDecisionHandler p2Handler) CreateSetup()
    {
        var p1Handler = new TestDecisionHandler();
        var p2Handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "Player 1", p1Handler),
            new Player(Guid.NewGuid(), "Player 2", p2Handler));
        var engine = new GameEngine(state);
        return (engine, state, p1Handler, p2Handler);
    }

    // ─── Warmth ────────────────────────────────────────────────────────────

    #region Warmth

    [Fact]
    public void Warmth_IsRegistered()
    {
        CardDefinitions.TryGet("Warmth", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Enchantment);
        def.ManaCost!.ConvertedManaCost.Should().Be(2);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.White).WhoseValue.Should().Be(1);
    }

    [Fact]
    public void Warmth_HasOpponentCastsRedSpellTrigger()
    {
        CardDefinitions.TryGet("Warmth", out var def);
        def!.Triggers.Should().HaveCount(1);
        def.Triggers[0].Event.Should().Be(GameEvent.SpellCast);
        def.Triggers[0].Condition.Should().Be(TriggerCondition.OpponentCastsRedSpell);
    }

    [Fact]
    public async Task Warmth_OpponentCastsRedSpell_GainsLife()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        // Put Warmth on Player 1's battlefield
        var warmth = GameCard.Create("Warmth");
        state.Player1.Battlefield.Add(warmth);

        // Opponent (Player 2) casts a red spell
        var redSpell = new GameCard
        {
            Name = "Lightning Bolt",
            ManaCost = ManaCost.Parse("{R}"),
            CardTypes = CardType.Instant,
        };

        var lifeBefore = state.Player1.Life;

        // Fire SpellCast triggers for the red spell, with player 2 as active player
        state.ActivePlayer = state.Player2;
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, redSpell);

        // Resolve the trigger
        state.StackCount.Should().BeGreaterThan(0);
        await engine.ResolveAllTriggersAsync();

        state.Player1.Life.Should().Be(lifeBefore + 2);
    }

    [Fact]
    public async Task Warmth_ControllerCastsRedSpell_DoesNotTrigger()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        var warmth = GameCard.Create("Warmth");
        state.Player1.Battlefield.Add(warmth);

        // Player 1 (controller) casts a red spell
        var redSpell = new GameCard
        {
            Name = "Lightning Bolt",
            ManaCost = ManaCost.Parse("{R}"),
            CardTypes = CardType.Instant,
        };

        var lifeBefore = state.Player1.Life;
        state.ActivePlayer = state.Player1;
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, redSpell);

        state.StackCount.Should().Be(0, "controller's own red spell should not trigger Warmth");
        state.Player1.Life.Should().Be(lifeBefore);
    }

    [Fact]
    public async Task Warmth_OpponentCastsBlueSpell_DoesNotTrigger()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        var warmth = GameCard.Create("Warmth");
        state.Player1.Battlefield.Add(warmth);

        var blueSpell = new GameCard
        {
            Name = "Counterspell",
            ManaCost = ManaCost.Parse("{U}{U}"),
            CardTypes = CardType.Instant,
        };

        state.ActivePlayer = state.Player2;
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, blueSpell);

        state.StackCount.Should().Be(0, "non-red spell should not trigger Warmth");
    }

    #endregion

    // ─── Spiritual Focus ───────────────────────────────────────────────────

    #region Spiritual Focus

    [Fact]
    public void SpiritualFocus_IsRegistered()
    {
        CardDefinitions.TryGet("Spiritual Focus", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Enchantment);
        def.ManaCost!.ConvertedManaCost.Should().Be(2);
    }

    [Fact]
    public void SpiritualFocus_HasDiscardTrigger()
    {
        CardDefinitions.TryGet("Spiritual Focus", out var def);
        def!.Triggers.Should().HaveCount(1);
        def.Triggers[0].Event.Should().Be(GameEvent.DiscardCard);
        def.Triggers[0].Condition.Should().Be(TriggerCondition.OpponentCausesControllerDiscard);
    }

    [Fact]
    public async Task SpiritualFocus_OpponentCausedDiscard_GainsLifeAndMayDraw()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        var focus = GameCard.Create("Spiritual Focus");
        state.Player1.Battlefield.Add(focus);

        // Put a card in player1's library for the draw
        state.Player1.Library.AddToTop(new GameCard { Name = "DrawTarget" });

        var lifeBefore = state.Player1.Life;
        var handBefore = state.Player1.Hand.Cards.Count;

        // Queue the discard trigger — opponent caused
        state.LastDiscardCausedByPlayerId = state.Player2.Id;
        state.ActivePlayer = state.Player1;
        engine.QueueDiscardTriggers(state.Player1);

        state.StackCount.Should().Be(1);
        await engine.ResolveAllTriggersAsync();

        state.Player1.Life.Should().Be(lifeBefore + 2);
        // With TestDecisionHandler defaulting, it should draw a card
        state.Player1.Hand.Cards.Count.Should().Be(handBefore + 1);
    }

    [Fact]
    public async Task SpiritualFocus_OpponentDiscards_DoesNotTrigger()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        var focus = GameCard.Create("Spiritual Focus");
        state.Player1.Battlefield.Add(focus);

        var lifeBefore = state.Player1.Life;

        state.ActivePlayer = state.Player2;
        engine.QueueDiscardTriggers(state.Player2);

        state.StackCount.Should().Be(0, "opponent's discard should not trigger controller's Spiritual Focus");
    }

    #endregion

    // ─── Presence of the Master ────────────────────────────────────────────

    #region Presence of the Master

    [Fact]
    public void PresenceOfTheMaster_IsRegistered()
    {
        CardDefinitions.TryGet("Presence of the Master", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Enchantment);
        def.ManaCost!.ConvertedManaCost.Should().Be(4);
    }

    [Fact]
    public void PresenceOfTheMaster_HasEnchantmentCastTrigger()
    {
        CardDefinitions.TryGet("Presence of the Master", out var def);
        def!.Triggers.Should().HaveCount(1);
        def.Triggers[0].Event.Should().Be(GameEvent.SpellCast);
        def.Triggers[0].Condition.Should().Be(TriggerCondition.AnyPlayerCastsEnchantment);
    }

    [Fact]
    public async Task PresenceOfTheMaster_CountersEnchantmentSpell()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        var presence = GameCard.Create("Presence of the Master");
        state.Player1.Battlefield.Add(presence);

        // Opponent casts an enchantment spell
        var enchantmentSpell = new GameCard
        {
            Name = "Wild Growth",
            ManaCost = ManaCost.Parse("{G}"),
            CardTypes = CardType.Enchantment,
        };

        // Put the enchantment on the stack
        var stackObj = new StackObject(enchantmentSpell, state.Player2.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 1);
        state.StackPush(stackObj);

        // Fire SpellCast triggers
        state.ActivePlayer = state.Player2;
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, enchantmentSpell);

        // Should have the counter trigger on the stack
        state.StackCount.Should().BeGreaterThan(1);

        // Resolve the counter trigger (on top of stack)
        await engine.ResolveAllTriggersAsync();

        // The enchantment should be in the owner's graveyard
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Name == "Wild Growth");
    }

    [Fact]
    public async Task PresenceOfTheMaster_DoesNotCounterItself()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        var presence = GameCard.Create("Presence of the Master");
        state.Player1.Battlefield.Add(presence);

        // "Cast" another Presence of the Master — name matches source, but it's a different card
        // However, our condition is `relevantCard.Name != permanent.Name` so this should NOT trigger
        var castPresence = new GameCard
        {
            Name = "Presence of the Master",
            ManaCost = ManaCost.Parse("{3}{W}"),
            CardTypes = CardType.Enchantment,
        };

        state.ActivePlayer = state.Player2;
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, castPresence);

        // Should NOT trigger (it would counter itself)
        state.StackCount.Should().Be(0, "Presence of the Master should not trigger for another copy with the same name");
    }

    [Fact]
    public async Task PresenceOfTheMaster_DoesNotCounterNonEnchantment()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        var presence = GameCard.Create("Presence of the Master");
        state.Player1.Battlefield.Add(presence);

        var creature = new GameCard
        {
            Name = "Grizzly Bears",
            ManaCost = ManaCost.Parse("{1}{G}"),
            CardTypes = CardType.Creature,
        };

        state.ActivePlayer = state.Player2;
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.SpellCast, creature);

        state.StackCount.Should().Be(0, "non-enchantment spell should not trigger Presence of the Master");
    }

    #endregion

    // ─── Sacred Ground ─────────────────────────────────────────────────────

    #region Sacred Ground

    [Fact]
    public void SacredGround_IsRegistered()
    {
        CardDefinitions.TryGet("Sacred Ground", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Enchantment);
        def.ManaCost!.ConvertedManaCost.Should().Be(2);
    }

    [Fact]
    public void SacredGround_HasLandToGraveyardTrigger()
    {
        CardDefinitions.TryGet("Sacred Ground", out var def);
        def!.Triggers.Should().HaveCount(1);
        def.Triggers[0].Event.Should().Be(GameEvent.LeavesBattlefield);
        def.Triggers[0].Condition.Should().Be(TriggerCondition.OpponentCausesControllerLandToGraveyard);
    }

    [Fact]
    public async Task SacredGround_ReturnsLandFromGraveyard_WhenOpponentCauses()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        var sacredGround = GameCard.Create("Sacred Ground");
        state.Player1.Battlefield.Add(sacredGround);

        // A land goes to graveyard
        var land = new GameCard
        {
            Name = "Plains",
            CardTypes = CardType.Land,
            Subtypes = ["Plains"],
        };
        // Simulate the land being in graveyard (already moved there before trigger resolves)
        state.Player1.Graveyard.Add(land);

        // Opponent caused the land destruction
        state.LastLandDestroyedByPlayerId = state.Player2.Id;
        state.ActivePlayer = state.Player1;
        // Fire the trigger
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.LeavesBattlefield, land);

        state.StackCount.Should().BeGreaterThan(0);
        await engine.ResolveAllTriggersAsync();

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Plains");
        state.Player1.Graveyard.Cards.Should().NotContain(c => c.Name == "Plains");
    }

    #endregion

    // ─── Seal of Fire ──────────────────────────────────────────────────────

    #region Seal of Fire

    [Fact]
    public void SealOfFire_IsRegistered()
    {
        CardDefinitions.TryGet("Seal of Fire", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Enchantment);
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Red).WhoseValue.Should().Be(1);
    }

    [Fact]
    public void SealOfFire_HasSacrificeAbility()
    {
        CardDefinitions.TryGet("Seal of Fire", out var def);
        def!.ActivatedAbilities.Should().HaveCount(1);
        def.ActivatedAbilities[0].Cost.SacrificeSelf.Should().BeTrue();
        def.ActivatedAbilities[0].CanTargetPlayer.Should().BeTrue();
    }

    [Fact]
    public async Task SealOfFire_DealsTwo_ToCreature()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var seal = GameCard.Create("Seal of Fire");
        state.Player1.Battlefield.Add(seal);

        var target = new GameCard
        {
            Name = "Bear",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 3,
        };
        state.Player2.Battlefield.Add(target);

        // Target the creature
        p1Handler.EnqueueTarget(new TargetInfo(target.Id, state.Player2.Id, ZoneType.Battlefield));

        var action = GameAction.ActivateAbility(state.Player1.Id, seal.Id);
        state.ActivePlayer = state.Player1;
        await engine.ExecuteAction(action);

        // Seal should be sacrificed
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Name == "Seal of Fire");
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Seal of Fire");

        // Resolve the ability from the stack
        await engine.ResolveAllTriggersAsync();

        target.DamageMarked.Should().Be(2);
    }

    [Fact]
    public async Task SealOfFire_DealsTwo_ToPlayer()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var seal = GameCard.Create("Seal of Fire");
        state.Player1.Battlefield.Add(seal);

        var lifeBefore = state.Player2.Life;
        // Target the opponent via the action's TargetPlayerId
        var action = GameAction.ActivateAbility(state.Player1.Id, seal.Id, targetPlayerId: state.Player2.Id);
        state.ActivePlayer = state.Player1;
        await engine.ExecuteAction(action);
        await engine.ResolveAllTriggersAsync();

        state.Player2.Life.Should().Be(lifeBefore - 2);
    }

    #endregion

    // ─── Ivory Tower ───────────────────────────────────────────────────────

    #region Ivory Tower

    [Fact]
    public void IvoryTower_IsRegistered()
    {
        CardDefinitions.TryGet("Ivory Tower", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Artifact);
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
    }

    [Fact]
    public void IvoryTower_HasUpkeepTrigger()
    {
        CardDefinitions.TryGet("Ivory Tower", out var def);
        def!.Triggers.Should().HaveCount(1);
        def.Triggers[0].Event.Should().Be(GameEvent.Upkeep);
        def.Triggers[0].Condition.Should().Be(TriggerCondition.Upkeep);
        def.Triggers[0].Effect.Should().BeOfType<IvoryTowerEffect>();
    }

    [Fact]
    public async Task IvoryTower_GainsLife_WhenHandExceedsFour()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        var tower = GameCard.Create("Ivory Tower");
        state.Player1.Battlefield.Add(tower);

        // Put 6 cards in hand (6 - 4 = 2 life)
        for (int i = 0; i < 6; i++)
            state.Player1.Hand.Add(new GameCard { Name = $"Card{i}" });

        var lifeBefore = state.Player1.Life;
        state.ActivePlayer = state.Player1;
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.Upkeep, null);
        await engine.ResolveAllTriggersAsync();

        state.Player1.Life.Should().Be(lifeBefore + 2);
    }

    [Fact]
    public async Task IvoryTower_NoLifeGain_WhenHandFourOrLess()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        var tower = GameCard.Create("Ivory Tower");
        state.Player1.Battlefield.Add(tower);

        // Put exactly 4 cards in hand (4 - 4 = 0 life)
        for (int i = 0; i < 4; i++)
            state.Player1.Hand.Add(new GameCard { Name = $"Card{i}" });

        var lifeBefore = state.Player1.Life;
        state.ActivePlayer = state.Player1;
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.Upkeep, null);
        await engine.ResolveAllTriggersAsync();

        state.Player1.Life.Should().Be(lifeBefore, "4 cards in hand means 0 life gain");
    }

    [Fact]
    public async Task IvoryTower_EmptyHand_NoLifeGain()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        var tower = GameCard.Create("Ivory Tower");
        state.Player1.Battlefield.Add(tower);

        // Empty hand
        var lifeBefore = state.Player1.Life;
        state.ActivePlayer = state.Player1;
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.Upkeep, null);
        await engine.ResolveAllTriggersAsync();

        state.Player1.Life.Should().Be(lifeBefore, "empty hand means 0 life gain");
    }

    #endregion

    // ─── Rejuvenation Chamber ──────────────────────────────────────────────

    #region Rejuvenation Chamber

    [Fact]
    public void RejuvenationChamber_IsRegistered()
    {
        CardDefinitions.TryGet("Rejuvenation Chamber", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Artifact);
        def.ManaCost!.ConvertedManaCost.Should().Be(3);
    }

    [Fact]
    public void RejuvenationChamber_HasFading2()
    {
        CardDefinitions.TryGet("Rejuvenation Chamber", out var def);
        def!.EntersWithCounters.Should().ContainKey(CounterType.Fade).WhoseValue.Should().Be(2);
    }

    [Fact]
    public void RejuvenationChamber_HasFadingTrigger()
    {
        CardDefinitions.TryGet("Rejuvenation Chamber", out var def);
        def!.Triggers.Should().HaveCount(1);
        def.Triggers[0].Effect.Should().BeOfType<FadingUpkeepEffect>();
    }

    [Fact]
    public void RejuvenationChamber_HasTapForLifeAbility()
    {
        CardDefinitions.TryGet("Rejuvenation Chamber", out var def);
        def!.ActivatedAbilities.Should().HaveCount(1);
        def.ActivatedAbilities[0].Cost.TapSelf.Should().BeTrue();
    }

    [Fact]
    public async Task RejuvenationChamber_TapGainsTwoLife()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var chamber = GameCard.Create("Rejuvenation Chamber");
        state.Player1.Battlefield.Add(chamber);

        var lifeBefore = state.Player1.Life;
        var action = GameAction.ActivateAbility(state.Player1.Id, chamber.Id);
        state.ActivePlayer = state.Player1;
        await engine.ExecuteAction(action);
        await engine.ResolveAllTriggersAsync();

        state.Player1.Life.Should().Be(lifeBefore + 2);
        chamber.IsTapped.Should().BeTrue();
    }

    [Fact]
    public void RejuvenationChamber_GameCard_HasFadeCounters()
    {
        var chamber = GameCard.Create("Rejuvenation Chamber");
        // Apply enters-with-counters like the engine would
        var (engine, state, _, _) = CreateSetup();
        engine.ApplyEntersWithCounters(chamber);

        chamber.GetCounters(CounterType.Fade).Should().Be(2);
    }

    #endregion

    // ─── Serenity ──────────────────────────────────────────────────────────

    #region Serenity

    [Fact]
    public void Serenity_IsRegistered()
    {
        CardDefinitions.TryGet("Serenity", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Enchantment);
        def.ManaCost!.ConvertedManaCost.Should().Be(2);
    }

    [Fact]
    public void Serenity_HasUpkeepTrigger()
    {
        CardDefinitions.TryGet("Serenity", out var def);
        def!.Triggers.Should().HaveCount(1);
        def.Triggers[0].Event.Should().Be(GameEvent.Upkeep);
        def.Triggers[0].Condition.Should().Be(TriggerCondition.Upkeep);
        def.Triggers[0].Effect.Should().BeOfType<SerenityEffect>();
    }

    [Fact]
    public async Task Serenity_DestroysAllArtifactsAndEnchantments()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        var serenity = GameCard.Create("Serenity");
        state.Player1.Battlefield.Add(serenity);

        var artifact = new GameCard { Name = "Sol Ring", CardTypes = CardType.Artifact };
        state.Player2.Battlefield.Add(artifact);

        var enchantment = new GameCard { Name = "Wild Growth", CardTypes = CardType.Enchantment };
        state.Player1.Battlefield.Add(enchantment);

        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        state.Player1.Battlefield.Add(creature);

        state.ActivePlayer = state.Player1;
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.Upkeep, null);
        await engine.ResolveAllTriggersAsync();

        // Serenity destroys itself
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Name == "Serenity");
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Serenity");

        // Other enchantment destroyed
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Name == "Wild Growth");
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Wild Growth");

        // Artifact on opponent's side destroyed
        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Name == "Sol Ring");
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Name == "Sol Ring");

        // Creature should survive
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Bear");
    }

    #endregion

    // ─── Carpet of Flowers ─────────────────────────────────────────────────

    #region Carpet of Flowers

    [Fact]
    public void CarpetOfFlowers_IsRegistered()
    {
        CardDefinitions.TryGet("Carpet of Flowers", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Enchantment);
        def.ManaCost!.ConvertedManaCost.Should().Be(1);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Green).WhoseValue.Should().Be(1);
    }

    [Fact]
    public void CarpetOfFlowers_HasMainPhaseTrigger()
    {
        CardDefinitions.TryGet("Carpet of Flowers", out var def);
        def!.Triggers.Should().HaveCount(1);
        def.Triggers[0].Event.Should().Be(GameEvent.MainPhaseBeginning);
        def.Triggers[0].Condition.Should().Be(TriggerCondition.ControllerMainPhaseBeginning);
    }

    [Fact]
    public async Task CarpetOfFlowers_AddsManaBased_OnOpponentIslands()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        var carpet = GameCard.Create("Carpet of Flowers");
        state.Player1.Battlefield.Add(carpet);

        // Opponent controls 3 Islands
        for (int i = 0; i < 3; i++)
        {
            state.Player2.Battlefield.Add(new GameCard
            {
                Name = "Island",
                CardTypes = CardType.Land,
                Subtypes = ["Island"],
            });
        }

        // Choose green mana
        p1Handler.EnqueueManaColor(ManaColor.Green);

        state.ActivePlayer = state.Player1;
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.MainPhaseBeginning, null);

        state.StackCount.Should().BeGreaterThan(0);
        await engine.ResolveAllTriggersAsync();

        state.Player1.ManaPool[ManaColor.Green].Should().Be(3);
        carpet.CarpetUsedThisTurn.Should().BeTrue();
    }

    [Fact]
    public async Task CarpetOfFlowers_OncePerTurn()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        var carpet = GameCard.Create("Carpet of Flowers");
        state.Player1.Battlefield.Add(carpet);

        state.Player2.Battlefield.Add(new GameCard
        {
            Name = "Island",
            CardTypes = CardType.Land,
            Subtypes = ["Island"],
        });

        p1Handler.EnqueueManaColor(ManaColor.Green);

        state.ActivePlayer = state.Player1;
        // First main phase
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.MainPhaseBeginning, null);
        await engine.ResolveAllTriggersAsync();

        state.Player1.ManaPool[ManaColor.Green].Should().Be(1);

        // Second main phase — should not fire again
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.MainPhaseBeginning, null);
        await engine.ResolveAllTriggersAsync();

        // Still only 1 green mana (pool not cleared in test, but no new mana added)
        state.Player1.ManaPool[ManaColor.Green].Should().Be(1, "should not add mana twice in the same turn");
    }

    [Fact]
    public async Task CarpetOfFlowers_NoIslands_NoMana()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();

        var carpet = GameCard.Create("Carpet of Flowers");
        state.Player1.Battlefield.Add(carpet);

        // Opponent controls no Islands
        state.ActivePlayer = state.Player1;
        await engine.QueueBoardTriggersOnStackAsync(GameEvent.MainPhaseBeginning, null);

        // Trigger should fire but do nothing (0 Islands)
        if (state.StackCount > 0)
            await engine.ResolveAllTriggersAsync();

        state.Player1.ManaPool[ManaColor.Green].Should().Be(0);
    }

    #endregion

    // ─── Zombie Infestation ────────────────────────────────────────────────

    #region Zombie Infestation

    [Fact]
    public void ZombieInfestation_IsRegistered()
    {
        CardDefinitions.TryGet("Zombie Infestation", out var def).Should().BeTrue();
        def!.CardTypes.Should().Be(CardType.Enchantment);
        def.ManaCost!.ConvertedManaCost.Should().Be(2);
        def.ManaCost.ColorRequirements.Should().ContainKey(ManaColor.Black).WhoseValue.Should().Be(1);
    }

    [Fact]
    public void ZombieInfestation_HasDiscardTwoAbility()
    {
        CardDefinitions.TryGet("Zombie Infestation", out var def);
        def!.ActivatedAbilities.Should().HaveCount(1);
        def.ActivatedAbilities[0].Cost.DiscardCount.Should().Be(2);
        def.ActivatedAbilities[0].Cost.TapSelf.Should().BeFalse();
        def.ActivatedAbilities[0].Cost.SacrificeSelf.Should().BeFalse();
    }

    [Fact]
    public async Task ZombieInfestation_DiscardTwo_CreatesZombie()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var infestation = GameCard.Create("Zombie Infestation");
        state.Player1.Battlefield.Add(infestation);

        // Add 3 cards to hand
        var card1 = new GameCard { Name = "Card1" };
        var card2 = new GameCard { Name = "Card2" };
        var card3 = new GameCard { Name = "Card3" };
        state.Player1.Hand.Add(card1);
        state.Player1.Hand.Add(card2);
        state.Player1.Hand.Add(card3);

        // Queue discard choices
        p1Handler.EnqueueCardChoice(card1.Id);
        p1Handler.EnqueueCardChoice(card2.Id);

        var handBefore = state.Player1.Hand.Cards.Count;
        var action = GameAction.ActivateAbility(state.Player1.Id, infestation.Id);
        state.ActivePlayer = state.Player1;
        await engine.ExecuteAction(action);

        // Two cards should be discarded
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Card1");
        state.Player1.Graveyard.Cards.Should().Contain(c => c.Name == "Card2");

        // Resolve the token creation
        await engine.ResolveAllTriggersAsync();

        // Should have a 2/2 Zombie token
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Zombie" && c.IsToken);
        var zombie = state.Player1.Battlefield.Cards.First(c => c.Name == "Zombie");
        zombie.Power.Should().Be(2);
        zombie.Toughness.Should().Be(2);
        zombie.IsCreature.Should().BeTrue();
    }

    [Fact]
    public async Task ZombieInfestation_NotEnoughCards_CannotActivate()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var infestation = GameCard.Create("Zombie Infestation");
        state.Player1.Battlefield.Add(infestation);

        // Only 1 card in hand (need 2)
        state.Player1.Hand.Add(new GameCard { Name = "OnlyCard" });

        var action = GameAction.ActivateAbility(state.Player1.Id, infestation.Id);
        state.ActivePlayer = state.Player1;
        await engine.ExecuteAction(action);

        // Should not create a token
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Name == "Zombie");
    }

    [Fact]
    public async Task ZombieInfestation_DoesNotRequireTap()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var infestation = GameCard.Create("Zombie Infestation");
        state.Player1.Battlefield.Add(infestation);

        // Add cards
        var card1 = new GameCard { Name = "Card1" };
        var card2 = new GameCard { Name = "Card2" };
        var card3 = new GameCard { Name = "Card3" };
        var card4 = new GameCard { Name = "Card4" };
        state.Player1.Hand.Add(card1);
        state.Player1.Hand.Add(card2);
        state.Player1.Hand.Add(card3);
        state.Player1.Hand.Add(card4);

        // Activate first time
        p1Handler.EnqueueCardChoice(card1.Id);
        p1Handler.EnqueueCardChoice(card2.Id);
        var action = GameAction.ActivateAbility(state.Player1.Id, infestation.Id);
        state.ActivePlayer = state.Player1;
        await engine.ExecuteAction(action);
        await engine.ResolveAllTriggersAsync();

        // Activate second time (no tap required)
        p1Handler.EnqueueCardChoice(card3.Id);
        p1Handler.EnqueueCardChoice(card4.Id);
        await engine.ExecuteAction(action);
        await engine.ResolveAllTriggersAsync();

        // Should have 2 zombie tokens
        state.Player1.Battlefield.Cards.Count(c => c.Name == "Zombie" && c.IsToken).Should().Be(2);
    }

    #endregion

    // ─── GameCard.Create integration ───────────────────────────────────────

    #region GameCard.Create

    [Fact]
    public void GameCard_Create_Warmth_HasCorrectType()
    {
        var card = GameCard.Create("Warmth");
        card.CardTypes.Should().Be(CardType.Enchantment);
        card.ManaCost.Should().NotBeNull();
    }

    [Fact]
    public void GameCard_Create_IvoryTower_HasCorrectType()
    {
        var card = GameCard.Create("Ivory Tower");
        card.CardTypes.Should().Be(CardType.Artifact);
    }

    [Fact]
    public void GameCard_Create_SealOfFire_HasCorrectType()
    {
        var card = GameCard.Create("Seal of Fire");
        card.CardTypes.Should().Be(CardType.Enchantment);
    }

    [Fact]
    public void GameCard_Create_CarpetOfFlowers_HasCorrectType()
    {
        var card = GameCard.Create("Carpet of Flowers");
        card.CardTypes.Should().Be(CardType.Enchantment);
    }

    [Fact]
    public void GameCard_Create_ZombieInfestation_HasCorrectType()
    {
        var card = GameCard.Create("Zombie Infestation");
        card.CardTypes.Should().Be(CardType.Enchantment);
    }

    #endregion
}
