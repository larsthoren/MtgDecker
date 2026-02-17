using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Phase6DecreeOfJusticeTests
{
    private GameState CreateState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        return new GameState(p1, p2);
    }

    // --- Main cast (SpellEffect) tests ---

    [Fact]
    public async Task DecreeOfJustice_MainCast_CreatesAngels_FromManaPool()
    {
        var state = CreateState();
        var h1 = new TestDecisionHandler();

        // 6 mana in pool => X = floor(6/2) = 3 Angels
        state.Player1.ManaPool.Add(ManaColor.White, 3);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 3);

        var card = new GameCard { Name = "Decree of Justice" };
        var spell = new StackObject(card, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 1);

        var effect = new DecreeOfJusticeEffect();
        await effect.ResolveAsync(state, spell, h1);

        var angels = state.Player1.Battlefield.Cards
            .Where(c => c.Name == "Angel").ToList();

        angels.Should().HaveCount(3);
        angels.Should().AllSatisfy(a =>
        {
            a.Power.Should().Be(4);
            a.Toughness.Should().Be(4);
            a.CardTypes.Should().HaveFlag(CardType.Creature);
            a.Subtypes.Should().Contain("Angel");
        });

        // Mana pool should be drained
        state.Player1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task DecreeOfJustice_MainCast_EmptyPool_CreatesZero()
    {
        var state = CreateState();
        var h1 = new TestDecisionHandler();

        // No mana in pool
        var card = new GameCard { Name = "Decree of Justice" };
        var spell = new StackObject(card, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 1);

        var effect = new DecreeOfJusticeEffect();
        await effect.ResolveAsync(state, spell, h1);

        state.Player1.Battlefield.Cards
            .Where(c => c.Name == "Angel").Should().BeEmpty();
    }

    [Fact]
    public async Task DecreeOfJustice_MainCast_OddMana_FloorsDown()
    {
        var state = CreateState();
        var h1 = new TestDecisionHandler();

        // 5 mana in pool => X = floor(5/2) = 2 Angels, 1 mana leftover
        state.Player1.ManaPool.Add(ManaColor.White, 2);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 3);

        var card = new GameCard { Name = "Decree of Justice" };
        var spell = new StackObject(card, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 1);

        var effect = new DecreeOfJusticeEffect();
        await effect.ResolveAsync(state, spell, h1);

        var angels = state.Player1.Battlefield.Cards
            .Where(c => c.Name == "Angel").ToList();

        angels.Should().HaveCount(2);

        // 5 - 4 = 1 mana leftover
        state.Player1.ManaPool.Total.Should().Be(1);
    }

    [Fact]
    public async Task DecreeOfJustice_Angels_AreTokens()
    {
        var state = CreateState();
        var h1 = new TestDecisionHandler();

        state.Player1.ManaPool.Add(ManaColor.White, 2);

        var card = new GameCard { Name = "Decree of Justice" };
        var spell = new StackObject(card, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 1);

        var effect = new DecreeOfJusticeEffect();
        await effect.ResolveAsync(state, spell, h1);

        var angel = state.Player1.Battlefield.Cards
            .Single(c => c.Name == "Angel");

        angel.IsToken.Should().BeTrue();
        angel.TurnEnteredBattlefield.Should().Be(state.TurnNumber);
    }

    // --- Cycling trigger (IEffect) tests ---

    [Fact]
    public async Task DecreeOfJustice_Cycling_CreatesSoldiers_FromManaPool()
    {
        var state = CreateState();
        var h1 = new TestDecisionHandler();
        var source = new GameCard { Name = "Decree of Justice" };

        // 3 mana in pool => 3 Soldiers
        state.Player1.ManaPool.Add(ManaColor.White, 3);

        var context = new EffectContext(state, state.Player1, source, h1);

        var effect = new DecreeOfJusticeCyclingEffect();
        await effect.Execute(context);

        var soldiers = state.Player1.Battlefield.Cards
            .Where(c => c.Name == "Soldier").ToList();

        soldiers.Should().HaveCount(3);
        soldiers.Should().AllSatisfy(s =>
        {
            s.Power.Should().Be(1);
            s.Toughness.Should().Be(1);
            s.CardTypes.Should().HaveFlag(CardType.Creature);
            s.Subtypes.Should().Contain("Soldier");
        });

        // Mana pool should be drained
        state.Player1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task DecreeOfJustice_Cycling_EmptyPool_CreatesZero()
    {
        var state = CreateState();
        var h1 = new TestDecisionHandler();
        var source = new GameCard { Name = "Decree of Justice" };

        // No mana in pool
        var context = new EffectContext(state, state.Player1, source, h1);

        var effect = new DecreeOfJusticeCyclingEffect();
        await effect.Execute(context);

        state.Player1.Battlefield.Cards
            .Where(c => c.Name == "Soldier").Should().BeEmpty();
    }

    [Fact]
    public async Task DecreeOfJustice_Soldiers_AreTokens()
    {
        var state = CreateState();
        var h1 = new TestDecisionHandler();
        var source = new GameCard { Name = "Decree of Justice" };

        state.Player1.ManaPool.Add(ManaColor.White, 1);

        var context = new EffectContext(state, state.Player1, source, h1);

        var effect = new DecreeOfJusticeCyclingEffect();
        await effect.Execute(context);

        var soldier = state.Player1.Battlefield.Cards
            .Single(c => c.Name == "Soldier");

        soldier.IsToken.Should().BeTrue();
        soldier.TurnEnteredBattlefield.Should().Be(state.TurnNumber);
    }
}
