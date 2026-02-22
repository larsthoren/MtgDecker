using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class CastSpellTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler handler) CreateSetup()
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
        return (engine, state, h1);
    }

    // --- Part A: Land Drops ---

    [Fact]
    public async Task PlayLand_MovesToBattlefield()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        state.Player1.Hand.Add(forest);

        await engine.ExecuteAction(GameAction.PlayLand(state.Player1.Id, forest.Id));

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == forest.Id);
        state.Player1.Hand.Cards.Should().NotContain(c => c.Id == forest.Id);
    }

    [Fact]
    public async Task PlayLand_IncrementsLandsPlayed()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        state.Player1.Hand.Add(forest);

        await engine.ExecuteAction(GameAction.PlayLand(state.Player1.Id, forest.Id));

        state.Player1.LandsPlayedThisTurn.Should().Be(1);
    }

    [Fact]
    public async Task PlaySecondLand_Rejected()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var forest1 = GameCard.Create("Forest", "Basic Land — Forest");
        var forest2 = GameCard.Create("Forest", "Basic Land — Forest");
        state.Player1.Hand.Add(forest1);
        state.Player1.Hand.Add(forest2);

        await engine.ExecuteAction(GameAction.PlayLand(state.Player1.Id, forest1.Id));
        await engine.ExecuteAction(GameAction.PlayLand(state.Player1.Id, forest2.Id));

        state.Player1.Battlefield.Cards.Where(c => c.Name == "Forest").Should().HaveCount(1);
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == forest2.Id);
    }

    [Fact]
    public async Task PlayLand_NoManaDeducted()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        state.Player1.ManaPool.Add(ManaColor.Green, 3);
        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        state.Player1.Hand.Add(forest);

        await engine.ExecuteAction(GameAction.PlayLand(state.Player1.Id, forest.Id));

        state.Player1.ManaPool.Total.Should().Be(3, "playing a land should not cost mana");
    }

    [Fact]
    public async Task PlayLand_Logs()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        state.Player1.Hand.Add(forest);

        await engine.ExecuteAction(GameAction.PlayLand(state.Player1.Id, forest.Id));

        state.GameLog.Should().Contain(m => m.Contains("land drop"));
    }

    // --- Part B: Casting Spells ---

    [Fact]
    public async Task CastSpell_DeductsManaCost()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var goblin = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        state.Player1.Hand.Add(goblin);
        state.Player1.ManaPool.Add(ManaColor.Red, 1);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, goblin.Id));
        await engine.ResolveAllTriggersAsync();

        state.Player1.ManaPool.Total.Should().Be(0);
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == goblin.Id);
    }

    [Fact]
    public async Task CastSpell_InsufficientMana_Rejected()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var goblin = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        state.Player1.Hand.Add(goblin);
        // No mana in pool

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, goblin.Id));
        await engine.ResolveAllTriggersAsync();

        state.Player1.Hand.Cards.Should().Contain(c => c.Id == goblin.Id);
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == goblin.Id);
    }

    [Fact]
    public async Task CastSpell_Creature_GoesToBattlefield()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var goblin = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        state.Player1.Hand.Add(goblin);
        state.Player1.ManaPool.Add(ManaColor.Red, 1);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, goblin.Id));
        await engine.ResolveAllTriggersAsync();

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == goblin.Id);
    }

    [Fact]
    public async Task CastSpell_Instant_GoesToGraveyard()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // Swords to Plowshares needs a creature target
        var target = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        state.Player2.Battlefield.Add(target);

        var swords = GameCard.Create("Swords to Plowshares", "Instant");
        state.Player1.Hand.Add(swords);
        state.Player1.ManaPool.Add(ManaColor.White, 1);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, swords.Id));
        await engine.ResolveAllTriggersAsync();

        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == swords.Id);
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == swords.Id);
    }

    [Fact]
    public async Task CastSpell_Sorcery_GoesToGraveyard()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var replenish = GameCard.Create("Replenish", "Sorcery");
        state.Player1.Hand.Add(replenish);
        state.Player1.ManaPool.Add(ManaColor.White, 4); // {3}{W} — need 1W + 3 generic

        // CastSpell auto-deducts {W}, enters mid-cast for {3} generic
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, replenish.Id));
        // Pay the 3 generic with remaining White mana
        await engine.ExecuteAction(GameAction.PayManaFromPool(state.Player1.Id, ManaColor.White));
        await engine.ExecuteAction(GameAction.PayManaFromPool(state.Player1.Id, ManaColor.White));
        await engine.ExecuteAction(GameAction.PayManaFromPool(state.Player1.Id, ManaColor.White));
        await engine.ResolveAllTriggersAsync();

        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == replenish.Id);
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == replenish.Id);
    }

    [Fact]
    public async Task CastSpell_GenericCost_EntersMidCast_PayWithChosenColor()
    {
        var (engine, state, handler) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // Goblin Piledriver: {1}{R} — need 1R + 1 generic
        var piledriver = GameCard.Create("Goblin Piledriver", "Creature — Goblin");
        state.Player1.Hand.Add(piledriver);
        state.Player1.ManaPool.Add(ManaColor.Red, 2);
        state.Player1.ManaPool.Add(ManaColor.Green, 1);

        // CastSpell auto-deducts {R}, then enters mid-cast for generic {1}
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, piledriver.Id));
        state.IsMidCast.Should().BeTrue();

        // Pay the generic cost with Green mana
        await engine.ExecuteAction(GameAction.PayManaFromPool(state.Player1.Id, ManaColor.Green));
        await engine.ResolveAllTriggersAsync();

        state.IsMidCast.Should().BeFalse();
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == piledriver.Id);
        state.Player1.ManaPool[ManaColor.Red].Should().Be(1);
        state.Player1.ManaPool[ManaColor.Green].Should().Be(0);
    }

    [Fact]
    public async Task CastSpell_GenericCost_EntersMidCast_PayWithSameColor()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // Goblin Piledriver: {1}{R} — give exactly {R}{R}: after color auto-deduct, R=1 left
        var piledriver = GameCard.Create("Goblin Piledriver", "Creature — Goblin");
        state.Player1.Hand.Add(piledriver);
        state.Player1.ManaPool.Add(ManaColor.Red, 2);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, piledriver.Id));
        state.IsMidCast.Should().BeTrue();

        // Pay the generic cost with remaining Red mana
        await engine.ExecuteAction(GameAction.PayManaFromPool(state.Player1.Id, ManaColor.Red));
        await engine.ResolveAllTriggersAsync();

        state.IsMidCast.Should().BeFalse();
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == piledriver.Id);
        state.Player1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task CastSpell_Logs()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var goblin = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        state.Player1.Hand.Add(goblin);
        state.Player1.ManaPool.Add(ManaColor.Red, 1);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, goblin.Id));
        await engine.ResolveAllTriggersAsync();

        state.GameLog.Should().Contain(m => m.Contains("casts"));
    }

    // --- Part C: Unregistered Card Rejection ---

    [Fact]
    public async Task UnregisteredCard_WithoutManaCost_IsRejected()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var card = GameCard.Create("Unknown Creature", "Creature — Mystery");
        state.Player1.Hand.Add(card);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, card.Id));
        await engine.ResolveAllTriggersAsync();

        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == card.Id);
        state.Player1.Hand.Cards.Should().Contain(c => c.Id == card.Id);
        state.GameLog.Should().Contain(l => l.Contains("no mana cost defined"));
    }

    // === Task 3: Mid-cast generic payment validation ===

    [Fact]
    public async Task CastSpell_MidCast_PayManaFromPool_RejectsEmptyPool()
    {
        var (engine, state, handler) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // Goblin Piledriver costs {1}{R}
        var piledriver = GameCard.Create("Goblin Piledriver", "Creature — Goblin");
        state.Player1.Hand.Add(piledriver);
        state.Player1.ManaPool.Add(ManaColor.Red, 2);

        // CastSpell auto-deducts {R}, enters mid-cast for {1} generic
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, piledriver.Id));
        state.IsMidCast.Should().BeTrue();

        // Try to pay with Green mana (which we don't have)
        var act = () => engine.ExecuteAction(GameAction.PayManaFromPool(state.Player1.Id, ManaColor.Green));
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Still in mid-cast
        state.IsMidCast.Should().BeTrue();

        // Pay correctly with Red
        await engine.ExecuteAction(GameAction.PayManaFromPool(state.Player1.Id, ManaColor.Red));
        await engine.ResolveAllTriggersAsync();
        state.IsMidCast.Should().BeFalse();
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Goblin Piledriver");
        state.Player1.ManaPool.Total.Should().Be(0);
    }
}
