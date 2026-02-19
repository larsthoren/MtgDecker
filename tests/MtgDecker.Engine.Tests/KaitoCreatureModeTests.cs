using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class KaitoCreatureModeTests : IDisposable
{
    private const string KaitoName = "Kaito, Bane of Nightmares";

    // StateCondition: only applies during the controller's turn
    // (active player has this card on their battlefield)
    private static readonly Func<GameState, bool> KaitoCreatureCondition =
        s => s.ActivePlayer.Battlefield.Cards.Any(c => c.Name == KaitoName);

    // Applies: only to cards named Kaito with loyalty > 0
    private static readonly Func<GameCard, Player, bool> KaitoApplies =
        (card, _) => card.Name == KaitoName && card.Loyalty > 0;

    public KaitoCreatureModeTests()
    {
        // Register a test CardDefinition for Kaito with creature-mode effects
        CardDefinitions.Register(new CardDefinition(
            ManaCost: null,
            ManaAbility: null,
            Power: null,
            Toughness: null,
            CardTypes: CardType.Planeswalker)
        {
            Name = KaitoName,
            IsLegendary = true,
            StartingLoyalty = 4,
            ContinuousEffects =
            [
                // BecomeCreature: 3/4 during controller's turn with loyalty > 0
                new ContinuousEffect(
                    Guid.Empty, ContinuousEffectType.BecomeCreature,
                    KaitoApplies,
                    SetPower: 3, SetToughness: 4,
                    Layer: EffectLayer.Layer4_TypeChanging,
                    ApplyToSelf: true,
                    StateCondition: KaitoCreatureCondition),
                // GrantKeyword: Hexproof during creature mode
                new ContinuousEffect(
                    Guid.Empty, ContinuousEffectType.GrantKeyword,
                    KaitoApplies,
                    GrantedKeyword: Keyword.Hexproof,
                    Layer: EffectLayer.Layer6_AbilityAddRemove,
                    StateCondition: KaitoCreatureCondition),
            ],
        });
    }

    public void Dispose()
    {
        CardDefinitions.Unregister(KaitoName);
    }

    [Fact]
    public void Kaito_DuringControllerTurn_WithLoyalty_IsCreature()
    {
        var (engine, state, p1, _) = CreateGame();
        state.ActivePlayer = p1;

        var kaito = new GameCard
        {
            Name = KaitoName,
            CardTypes = CardType.Planeswalker,
        };
        kaito.AddCounters(CounterType.Loyalty, 4);
        p1.Battlefield.Add(kaito);

        engine.RecalculateState();

        kaito.IsCreature.Should().BeTrue();
        kaito.IsPlaneswalker.Should().BeTrue();
        kaito.Power.Should().Be(3);
        kaito.Toughness.Should().Be(4);
    }

    [Fact]
    public void Kaito_DuringOpponentTurn_IsNotCreature()
    {
        var (engine, state, p1, p2) = CreateGame();
        state.ActivePlayer = p2; // Opponent's turn

        var kaito = new GameCard
        {
            Name = KaitoName,
            CardTypes = CardType.Planeswalker,
        };
        kaito.AddCounters(CounterType.Loyalty, 4);
        p1.Battlefield.Add(kaito);

        engine.RecalculateState();

        kaito.IsCreature.Should().BeFalse();
        kaito.IsPlaneswalker.Should().BeTrue();
        kaito.Power.Should().BeNull();
    }

    [Fact]
    public void Kaito_WithZeroLoyalty_IsNotCreature()
    {
        var (engine, state, p1, _) = CreateGame();
        state.ActivePlayer = p1;

        var kaito = new GameCard
        {
            Name = KaitoName,
            CardTypes = CardType.Planeswalker,
        };
        // No loyalty counters — Applies lambda requires Loyalty > 0
        p1.Battlefield.Add(kaito);

        engine.RecalculateState();

        kaito.IsCreature.Should().BeFalse();
    }

    [Fact]
    public void Kaito_CreatureMode_GrantsHexproof()
    {
        var (engine, state, p1, _) = CreateGame();
        state.ActivePlayer = p1;

        var kaito = new GameCard
        {
            Name = KaitoName,
            CardTypes = CardType.Planeswalker,
        };
        kaito.AddCounters(CounterType.Loyalty, 4);
        p1.Battlefield.Add(kaito);

        engine.RecalculateState();

        kaito.ActiveKeywords.Should().Contain(Keyword.Hexproof);
    }

    [Fact]
    public void Kaito_OpponentTurn_NoHexproof()
    {
        var (engine, state, p1, p2) = CreateGame();
        state.ActivePlayer = p2; // Opponent's turn

        var kaito = new GameCard
        {
            Name = KaitoName,
            CardTypes = CardType.Planeswalker,
        };
        kaito.AddCounters(CounterType.Loyalty, 4);
        p1.Battlefield.Add(kaito);

        engine.RecalculateState();

        kaito.ActiveKeywords.Should().NotContain(Keyword.Hexproof);
    }

    [Fact]
    public void Kaito_CreatureMode_PreservesExistingTypes()
    {
        // Kaito should be both a Planeswalker and Creature during creature mode
        var (engine, state, p1, _) = CreateGame();
        state.ActivePlayer = p1;

        var kaito = new GameCard
        {
            Name = KaitoName,
            CardTypes = CardType.Planeswalker,
        };
        kaito.AddCounters(CounterType.Loyalty, 4);
        p1.Battlefield.Add(kaito);

        engine.RecalculateState();

        kaito.IsPlaneswalker.Should().BeTrue();
        kaito.IsCreature.Should().BeTrue();
        // EffectiveCardTypes should have both flags
        kaito.EffectiveCardTypes.Should().NotBeNull();
        kaito.EffectiveCardTypes!.Value.HasFlag(CardType.Planeswalker).Should().BeTrue();
        kaito.EffectiveCardTypes!.Value.HasFlag(CardType.Creature).Should().BeTrue();
    }

    [Fact]
    public void Kaito_TurnChange_TogglesCreatureMode()
    {
        // During controller's turn: creature. During opponent's turn: not creature.
        var (engine, state, p1, p2) = CreateGame();

        var kaito = new GameCard
        {
            Name = KaitoName,
            CardTypes = CardType.Planeswalker,
        };
        kaito.AddCounters(CounterType.Loyalty, 4);
        p1.Battlefield.Add(kaito);

        // Controller's turn
        state.ActivePlayer = p1;
        engine.RecalculateState();
        kaito.IsCreature.Should().BeTrue("Kaito should be a creature during controller's turn");
        kaito.ActiveKeywords.Should().Contain(Keyword.Hexproof);

        // Opponent's turn
        state.ActivePlayer = p2;
        engine.RecalculateState();
        kaito.IsCreature.Should().BeFalse("Kaito should not be a creature during opponent's turn");
        kaito.ActiveKeywords.Should().NotContain(Keyword.Hexproof);
    }

    [Fact]
    public void StateCondition_Null_DoesNotAffectExistingEffects()
    {
        // Verify that the StateCondition parameter defaulting to null
        // doesn't break existing effects that don't use it.
        // This uses a direct UntilEndOfTurn effect to survive RecalculateState.
        var (engine, state, p1, _) = CreateGame();
        state.ActivePlayer = p1;

        var creature = new GameCard
        {
            Name = "Test Creature",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2,
        };
        p1.Battlefield.Add(creature);

        // UntilEndOfTurn effect without StateCondition (null by default)
        // survives RecalculateState clearing
        state.ActiveEffects.Add(new ContinuousEffect(
            Guid.NewGuid(), ContinuousEffectType.ModifyPowerToughness,
            (card, _) => card.IsCreature,
            PowerMod: 1, ToughnessMod: 1,
            UntilEndOfTurn: true,
            Layer: EffectLayer.Layer7c_ModifyPT));

        engine.RecalculateState();

        creature.Power.Should().Be(3);
        creature.Toughness.Should().Be(3);
    }

    private static (GameEngine engine, GameState state, Player p1, Player p2) CreateGame()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, p1, p2);
    }
}
