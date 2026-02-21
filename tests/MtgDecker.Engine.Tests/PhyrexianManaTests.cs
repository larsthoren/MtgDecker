using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class PhyrexianManaTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        for (int i = 0; i < 40; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (engine, state, h1, h2);
    }

    [Fact]
    public void Parse_SinglePhyrexianBlack_HasPhyrexianRequirement()
    {
        var cost = ManaCost.Parse("{B/P}");
        cost.PhyrexianRequirements.Should().ContainKey(ManaColor.Black);
        cost.PhyrexianRequirements[ManaColor.Black].Should().Be(1);
        cost.ColorRequirements.Should().BeEmpty();
        cost.GenericCost.Should().Be(0);
        cost.ConvertedManaCost.Should().Be(1);
    }

    [Fact]
    public void Parse_DoublePhyrexianBlack_HasCorrectCount()
    {
        var cost = ManaCost.Parse("{B/P}{B/P}");
        cost.PhyrexianRequirements[ManaColor.Black].Should().Be(2);
        cost.ConvertedManaCost.Should().Be(2);
    }

    [Fact]
    public void Parse_MixedCost_Dismember()
    {
        var cost = ManaCost.Parse("{1}{B/P}{B/P}");
        cost.GenericCost.Should().Be(1);
        cost.PhyrexianRequirements[ManaColor.Black].Should().Be(2);
        cost.ColorRequirements.Should().BeEmpty();
        cost.ConvertedManaCost.Should().Be(3);
    }

    [Fact]
    public void Parse_AllPhyrexianColors()
    {
        ManaCost.Parse("{U/P}").PhyrexianRequirements.Should().ContainKey(ManaColor.Blue);
        ManaCost.Parse("{R/P}").PhyrexianRequirements.Should().ContainKey(ManaColor.Red);
        ManaCost.Parse("{G/P}").PhyrexianRequirements.Should().ContainKey(ManaColor.Green);
        ManaCost.Parse("{W/P}").PhyrexianRequirements.Should().ContainKey(ManaColor.White);
    }

    [Fact]
    public void Parse_NormalCost_HasEmptyPhyrexianRequirements()
    {
        var cost = ManaCost.Parse("{2}{R}{R}");
        cost.PhyrexianRequirements.Should().BeEmpty();
    }

    [Fact]
    public void ToString_PhyrexianCost_OutputsCorrectFormat()
    {
        var cost = ManaCost.Parse("{1}{B/P}{B/P}");
        cost.ToString().Should().Be("{1}{B/P}{B/P}");
    }

    [Fact]
    public void ToString_SinglePhyrexian_OutputsCorrectFormat()
    {
        var cost = ManaCost.Parse("{U/P}");
        cost.ToString().Should().Be("{U/P}");
    }

    [Fact]
    public void HasPhyrexianCost_WithPhyrexian_ReturnsTrue()
    {
        ManaCost.Parse("{B/P}").HasPhyrexianCost.Should().BeTrue();
    }

    [Fact]
    public void HasPhyrexianCost_WithoutPhyrexian_ReturnsFalse()
    {
        ManaCost.Parse("{2}{R}").HasPhyrexianCost.Should().BeFalse();
    }

    [Fact]
    public void WithGenericReduction_PreservesPhyrexian()
    {
        var cost = ManaCost.Parse("{2}{B/P}");
        var reduced = cost.WithGenericReduction(1);
        reduced.GenericCost.Should().Be(1);
        reduced.PhyrexianRequirements[ManaColor.Black].Should().Be(1);
    }

    // --- ManaPool.CanPayWithPhyrexian tests ---

    [Fact]
    public void CanPayWithPhyrexian_EnoughMana_ReturnsTrue()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Black, 2);
        pool.Add(ManaColor.Colorless, 1);
        var cost = ManaCost.Parse("{1}{B/P}{B/P}");
        pool.CanPayWithPhyrexian(cost, 20).Should().BeTrue();
    }

    [Fact]
    public void CanPayWithPhyrexian_NoManaButEnoughLife_ReturnsTrue()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Colorless, 1);
        var cost = ManaCost.Parse("{1}{B/P}{B/P}");
        // Need 1 generic (have it) + 2 Phyrexian black at 2 life each = 4 life
        pool.CanPayWithPhyrexian(cost, 5).Should().BeTrue();
    }

    [Fact]
    public void CanPayWithPhyrexian_NotEnoughGenericMana_ReturnsFalse()
    {
        var pool = new ManaPool();
        // No mana at all, need {1}{B/P}{B/P} = 1 generic + 2 Phyrexian
        var cost = ManaCost.Parse("{1}{B/P}{B/P}");
        pool.CanPayWithPhyrexian(cost, 20).Should().BeFalse(); // Can't pay generic
    }

    [Fact]
    public void CanPayWithPhyrexian_LifeTooLow_ReturnsFalse()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Colorless, 1);
        var cost = ManaCost.Parse("{1}{B/P}{B/P}");
        // Need 4 life for 2 Phyrexian, but only have 3
        pool.CanPayWithPhyrexian(cost, 3).Should().BeFalse();
    }

    [Fact]
    public void CanPayWithPhyrexian_MixedPayment_ReturnsTrue()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Black, 1);
        pool.Add(ManaColor.Colorless, 1);
        var cost = ManaCost.Parse("{1}{B/P}{B/P}");
        // 1 generic (have it) + 1 black mana for 1st Phyrexian + 2 life for 2nd
        pool.CanPayWithPhyrexian(cost, 3).Should().BeTrue();
    }

    [Fact]
    public void CanPayWithPhyrexian_NonPhyrexianCost_DelegatesToCanPay()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 2);
        pool.Add(ManaColor.Colorless, 2);
        var cost = ManaCost.Parse("{2}{R}{R}");
        pool.CanPayWithPhyrexian(cost, 20).Should().BeTrue();
    }

    [Fact]
    public void CanPayWithPhyrexian_NonPhyrexianCost_NotEnough_ReturnsFalse()
    {
        var pool = new ManaPool();
        pool.Add(ManaColor.Red, 1);
        var cost = ManaCost.Parse("{2}{R}{R}");
        pool.CanPayWithPhyrexian(cost, 20).Should().BeFalse();
    }

    [Fact]
    public void CanPayWithPhyrexian_SinglePhyrexian_LifeOnly()
    {
        var pool = new ManaPool();
        var cost = ManaCost.Parse("{B/P}");
        // No mana, but enough life
        pool.CanPayWithPhyrexian(cost, 3).Should().BeTrue();
    }

    [Fact]
    public void CanPayWithPhyrexian_SinglePhyrexian_LifeExactlyEqual_ReturnsFalse()
    {
        var pool = new ManaPool();
        var cost = ManaCost.Parse("{B/P}");
        // Life exactly equal to cost (2) — strict inequality means can't pay
        pool.CanPayWithPhyrexian(cost, 2).Should().BeFalse();
    }

    // --- Integration tests: CastSpell with Phyrexian mana ---

    [Fact]
    public async Task PhyrexianCost_PayAllMana_NoLifeLost()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        // Create a card with Phyrexian cost (not in CardDefinitions)
        var card = new GameCard { Name = "TestPhyrexianCard", ManaCost = ManaCost.Parse("{1}{B/P}{B/P}"), CardTypes = CardType.Instant };
        state.Player1.Hand.Add(card);
        state.Player1.ManaPool.Add(ManaColor.Black, 2);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 1);

        // Pay both Phyrexian with mana
        h1.EnqueuePhyrexianPayment(true);  // 1st {B/P} = pay mana
        h1.EnqueuePhyrexianPayment(true);  // 2nd {B/P} = pay mana

        var startLife = state.Player1.Life;
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, card.Id));

        state.Player1.Life.Should().Be(startLife);
        state.StackCount.Should().BeGreaterThanOrEqualTo(1);
        state.Player1.ManaPool.Total.Should().Be(0); // All mana used
    }

    [Fact]
    public async Task PhyrexianCost_PayAllLife_Pays4Life()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var card = new GameCard { Name = "TestPhyrexianCard", ManaCost = ManaCost.Parse("{1}{B/P}{B/P}"), CardTypes = CardType.Instant };
        state.Player1.Hand.Add(card);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 1); // Only generic mana, no black

        // No black mana — should auto-pay life without prompting
        var startLife = state.Player1.Life;
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, card.Id));

        state.Player1.Life.Should().Be(startLife - 4);
        state.StackCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task PhyrexianCost_MixedPayment_1Mana1Life()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var card = new GameCard { Name = "TestPhyrexianCard", ManaCost = ManaCost.Parse("{1}{B/P}{B/P}"), CardTypes = CardType.Instant };
        state.Player1.Hand.Add(card);
        state.Player1.ManaPool.Add(ManaColor.Black, 1);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 1);

        h1.EnqueuePhyrexianPayment(true);   // 1st {B/P} = pay mana
        h1.EnqueuePhyrexianPayment(false);  // 2nd {B/P} = pay life

        var startLife = state.Player1.Life;
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, card.Id));

        state.Player1.Life.Should().Be(startLife - 2);
        state.StackCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task PhyrexianCost_SingleSymbol_PayLife()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var card = new GameCard { Name = "TestPhyrexianSingle", ManaCost = ManaCost.Parse("{B/P}"), CardTypes = CardType.Instant };
        state.Player1.Hand.Add(card);
        // No mana at all

        var startLife = state.Player1.Life;
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, card.Id));

        state.Player1.Life.Should().Be(startLife - 2);
        state.StackCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task PhyrexianCost_NotEnoughManaOrLife_Rejected()
    {
        var (engine, state, h1, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;
        state.ActivePlayer = state.Player1;

        var card = new GameCard { Name = "TestPhyrexianCard", ManaCost = ManaCost.Parse("{1}{B/P}{B/P}"), CardTypes = CardType.Instant };
        state.Player1.Hand.Add(card);
        // No mana, no generic — can pay Phyrexian with life but no generic mana
        // Life is 20, so Phyrexian is fine, but {1} generic can't be paid

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, card.Id));

        state.Player1.Hand.Cards.Should().Contain(c => c.Id == card.Id); // Still in hand
    }
}
