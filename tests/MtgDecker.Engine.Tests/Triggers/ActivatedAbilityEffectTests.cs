using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests.Triggers;

public class ActivatedAbilityEffectTests
{
    private (GameState state, Player player1, Player player2, TestDecisionHandler handler1, TestDecisionHandler handler2) CreateSetup()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Player 1", handler1);
        var p2 = new Player(Guid.NewGuid(), "Player 2", handler2);
        var state = new GameState(p1, p2);
        return (state, p1, p2, handler1, handler2);
    }

    // === DealDamageEffect ===

    [Fact]
    public async Task DealDamageEffect_TargetCreature_AddsDamageMarked()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var target = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        player.Battlefield.Add(target);

        var effect = new DealDamageEffect(1);
        var source = new GameCard { Name = "Mogg Fanatic" };
        var context = new EffectContext(state, player, source, handler) { Target = target };

        await effect.Execute(context);

        target.DamageMarked.Should().Be(1);
    }

    [Fact]
    public async Task DealDamageEffect_TargetCreature_Logs()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var target = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        player.Battlefield.Add(target);

        var effect = new DealDamageEffect(3);
        var source = new GameCard { Name = "Siege-Gang Commander" };
        var context = new EffectContext(state, player, source, handler) { Target = target };

        await effect.Execute(context);

        target.DamageMarked.Should().Be(3);
        state.GameLog.Should().Contain(l => l.Contains("3 damage") && l.Contains("Bear"));
    }

    [Fact]
    public async Task DealDamageEffect_TargetPlayer_ReducesLife()
    {
        var (state, player1, player2, handler, _) = CreateSetup();

        var effect = new DealDamageEffect(2);
        var source = new GameCard { Name = "Siege-Gang Commander" };
        var context = new EffectContext(state, player1, source, handler) { TargetPlayerId = player2.Id };

        await effect.Execute(context);

        player2.Life.Should().Be(18);
    }

    [Fact]
    public async Task DealDamageEffect_TargetPlayer_Logs()
    {
        var (state, player1, player2, handler, _) = CreateSetup();

        var effect = new DealDamageEffect(1);
        var source = new GameCard { Name = "Mogg Fanatic" };
        var context = new EffectContext(state, player1, source, handler) { TargetPlayerId = player2.Id };

        await effect.Execute(context);

        state.GameLog.Should().Contain(l => l.Contains("Player 2") && l.Contains("1 damage"));
    }

    [Fact]
    public async Task DealDamageEffect_NoTargetOrPlayer_DoesNothing()
    {
        var (state, player, _, handler, _) = CreateSetup();

        var effect = new DealDamageEffect(5);
        var source = new GameCard { Name = "Test" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.Life.Should().Be(20);
    }

    // === AddManaEffect ===

    [Fact]
    public async Task AddManaEffect_AddsColorToPool()
    {
        var (state, player, _, handler, _) = CreateSetup();

        var effect = new AddManaEffect(ManaColor.Red);
        var source = new GameCard { Name = "Skirk Prospector" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.ManaPool[ManaColor.Red].Should().Be(1);
    }

    [Fact]
    public async Task AddManaEffect_Logs()
    {
        var (state, player, _, handler, _) = CreateSetup();

        var effect = new AddManaEffect(ManaColor.Red);
        var source = new GameCard { Name = "Skirk Prospector" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        state.GameLog.Should().Contain(l => l.Contains("Player 1") && l.Contains("Red"));
    }

    [Fact]
    public async Task AddManaEffect_StacksWithExistingMana()
    {
        var (state, player, _, handler, _) = CreateSetup();
        player.ManaPool.Add(ManaColor.Red, 2);

        var effect = new AddManaEffect(ManaColor.Red);
        var source = new GameCard { Name = "Skirk Prospector" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.ManaPool[ManaColor.Red].Should().Be(3);
    }

    // === DestroyTargetEffect ===

    [Fact]
    public async Task DestroyTargetEffect_RemovesFromBattlefield_AddsToGraveyard()
    {
        var (state, player1, player2, handler, _) = CreateSetup();
        var target = new GameCard { Name = "Sol Ring", CardTypes = CardType.Artifact };
        player2.Battlefield.Add(target);

        var effect = new DestroyTargetEffect();
        var source = new GameCard { Name = "Goblin Tinkerer" };
        var context = new EffectContext(state, player1, source, handler) { Target = target };

        await effect.Execute(context);

        player2.Battlefield.Cards.Should().NotContain(c => c.Id == target.Id);
        player2.Graveyard.Cards.Should().Contain(c => c.Id == target.Id);
    }

    [Fact]
    public async Task DestroyTargetEffect_FindsOwnerOnPlayer1Battlefield()
    {
        var (state, player1, player2, handler, _) = CreateSetup();
        var target = new GameCard { Name = "Artifact", CardTypes = CardType.Artifact };
        player1.Battlefield.Add(target);

        var effect = new DestroyTargetEffect();
        var source = new GameCard { Name = "Seal of Cleansing" };
        var context = new EffectContext(state, player1, source, handler) { Target = target };

        await effect.Execute(context);

        player1.Battlefield.Cards.Should().NotContain(c => c.Id == target.Id);
        player1.Graveyard.Cards.Should().Contain(c => c.Id == target.Id);
    }

    [Fact]
    public async Task DestroyTargetEffect_Logs()
    {
        var (state, player1, player2, handler, _) = CreateSetup();
        var target = new GameCard { Name = "Sol Ring", CardTypes = CardType.Artifact };
        player2.Battlefield.Add(target);

        var effect = new DestroyTargetEffect();
        var source = new GameCard { Name = "Goblin Tinkerer" };
        var context = new EffectContext(state, player1, source, handler) { Target = target };

        await effect.Execute(context);

        state.GameLog.Should().Contain(l => l.Contains("Sol Ring") && l.Contains("destroyed"));
    }

    [Fact]
    public async Task DestroyTargetEffect_NullTarget_DoesNothing()
    {
        var (state, player, _, handler, _) = CreateSetup();

        var effect = new DestroyTargetEffect();
        var source = new GameCard { Name = "Test" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        state.GameLog.Should().BeEmpty();
    }

    // === TapTargetEffect ===

    [Fact]
    public async Task TapTargetEffect_TapsTarget()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var target = new GameCard { Name = "Rishadan Port", CardTypes = CardType.Land };
        player.Battlefield.Add(target);

        var effect = new TapTargetEffect();
        var source = new GameCard { Name = "Test" };
        var context = new EffectContext(state, player, source, handler) { Target = target };

        await effect.Execute(context);

        target.IsTapped.Should().BeTrue();
    }

    [Fact]
    public async Task TapTargetEffect_Logs()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var target = new GameCard { Name = "Tropical Island", CardTypes = CardType.Land };
        player.Battlefield.Add(target);

        var effect = new TapTargetEffect();
        var source = new GameCard { Name = "Rishadan Port" };
        var context = new EffectContext(state, player, source, handler) { Target = target };

        await effect.Execute(context);

        state.GameLog.Should().Contain(l => l.Contains("Tropical Island") && l.Contains("tapped"));
    }

    [Fact]
    public async Task TapTargetEffect_NullTarget_DoesNothing()
    {
        var (state, player, _, handler, _) = CreateSetup();

        var effect = new TapTargetEffect();
        var source = new GameCard { Name = "Test" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        state.GameLog.Should().BeEmpty();
    }

    // === SearchLibraryByTypeEffect ===

    [Fact]
    public async Task SearchLibraryByTypeEffect_FindsMatchingCard_AddsToHand()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var enchantment = new GameCard { Name = "Wild Growth", CardTypes = CardType.Enchantment };
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        player.Library.Add(creature);
        player.Library.Add(enchantment);
        handler.EnqueueCardChoice(enchantment.Id);

        var effect = new SearchLibraryByTypeEffect(CardType.Enchantment);
        var source = new GameCard { Name = "Sterling Grove" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.Hand.Cards.Should().Contain(c => c.Id == enchantment.Id);
        player.Library.Cards.Should().NotContain(c => c.Id == enchantment.Id);
    }

    [Fact]
    public async Task SearchLibraryByTypeEffect_NoMatches_LogsAndShuffles()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        player.Library.Add(creature);

        var effect = new SearchLibraryByTypeEffect(CardType.Enchantment);
        var source = new GameCard { Name = "Sterling Grove" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.Hand.Count.Should().Be(0);
        state.GameLog.Should().Contain(l => l.Contains("no") && l.Contains("Enchantment"));
    }

    [Fact]
    public async Task SearchLibraryByTypeEffect_PlayerDeclines_NoCardAdded()
    {
        var (state, player, _, handler, _) = CreateSetup();
        var enchantment = new GameCard { Name = "Wild Growth", CardTypes = CardType.Enchantment };
        player.Library.Add(enchantment);
        handler.EnqueueCardChoice(null);

        var effect = new SearchLibraryByTypeEffect(CardType.Enchantment);
        var source = new GameCard { Name = "Sterling Grove" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.Hand.Count.Should().Be(0);
        player.Library.Count.Should().Be(1);
    }

    [Fact]
    public async Task SearchLibraryByTypeEffect_ShufflesLibraryAfterSearch()
    {
        var (state, player, _, handler, _) = CreateSetup();
        for (int i = 0; i < 20; i++)
            player.Library.Add(new GameCard
            {
                Name = $"Card {i}",
                CardTypes = i < 5 ? CardType.Enchantment : CardType.Creature
            });
        handler.EnqueueCardChoice(player.Library.Cards[0].Id);

        var effect = new SearchLibraryByTypeEffect(CardType.Enchantment);
        var source = new GameCard { Name = "Sterling Grove" };
        var context = new EffectContext(state, player, source, handler);

        await effect.Execute(context);

        player.Library.Count.Should().Be(19);
        player.Hand.Count.Should().Be(1);
    }
}
