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

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, forest.Id));

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

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, forest.Id));

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

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, forest1.Id));
        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, forest2.Id));

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

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, forest.Id));

        state.Player1.ManaPool.Total.Should().Be(3, "playing a land should not cost mana");
    }

    [Fact]
    public async Task PlayLand_Logs()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var forest = GameCard.Create("Forest", "Basic Land — Forest");
        state.Player1.Hand.Add(forest);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, forest.Id));

        state.GameLog.Should().Contain(m => m.Contains("land drop"));
    }

    // --- Part B: Casting Spells ---

    [Fact]
    public async Task CastSpell_DeductsManaCost()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var goblin = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        state.Player1.Hand.Add(goblin);
        state.Player1.ManaPool.Add(ManaColor.Red, 1);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, goblin.Id));

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

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, goblin.Id));

        state.Player1.Hand.Cards.Should().Contain(c => c.Id == goblin.Id);
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == goblin.Id);
    }

    [Fact]
    public async Task CastSpell_Creature_GoesToBattlefield()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var goblin = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        state.Player1.Hand.Add(goblin);
        state.Player1.ManaPool.Add(ManaColor.Red, 1);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, goblin.Id));

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == goblin.Id);
    }

    [Fact]
    public async Task CastSpell_Instant_GoesToGraveyard()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var swords = GameCard.Create("Swords to Plowshares", "Instant");
        state.Player1.Hand.Add(swords);
        state.Player1.ManaPool.Add(ManaColor.White, 1);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, swords.Id));

        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == swords.Id);
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == swords.Id);
    }

    [Fact]
    public async Task CastSpell_Sorcery_GoesToGraveyard()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var replenish = GameCard.Create("Replenish", "Sorcery");
        state.Player1.Hand.Add(replenish);
        state.Player1.ManaPool.Add(ManaColor.White, 4); // {3}{W} — need 1W + 3 generic

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, replenish.Id));

        state.Player1.Graveyard.Cards.Should().Contain(c => c.Id == replenish.Id);
        state.Player1.Battlefield.Cards.Should().NotContain(c => c.Id == replenish.Id);
    }

    [Fact]
    public async Task CastSpell_AmbiguousGeneric_PromptsPlayer()
    {
        var (engine, state, handler) = CreateSetup();
        await engine.StartGameAsync();

        // Goblin Piledriver: {1}{R} — need 1R + 1 generic
        var piledriver = GameCard.Create("Goblin Piledriver", "Creature — Goblin");
        state.Player1.Hand.Add(piledriver);
        state.Player1.ManaPool.Add(ManaColor.Red, 2);
        state.Player1.ManaPool.Add(ManaColor.Green, 1);
        // After paying {R}, pool has R=1 G=1 — ambiguous for generic {1}

        handler.EnqueueGenericPayment(new Dictionary<ManaColor, int> { { ManaColor.Green, 1 } });

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, piledriver.Id));

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == piledriver.Id);
        state.Player1.ManaPool[ManaColor.Red].Should().Be(1);
        state.Player1.ManaPool[ManaColor.Green].Should().Be(0);
    }

    [Fact]
    public async Task CastSpell_UnambiguousGeneric_AutoPays()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        // Goblin Piledriver: {1}{R} — give exactly {R}{R}: after color, only R=1 left
        var piledriver = GameCard.Create("Goblin Piledriver", "Creature — Goblin");
        state.Player1.Hand.Add(piledriver);
        state.Player1.ManaPool.Add(ManaColor.Red, 2);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, piledriver.Id));

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == piledriver.Id);
        state.Player1.ManaPool.Total.Should().Be(0);
    }

    [Fact]
    public async Task CastSpell_Logs()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var goblin = GameCard.Create("Goblin Lackey", "Creature — Goblin");
        state.Player1.Hand.Add(goblin);
        state.Player1.ManaPool.Add(ManaColor.Red, 1);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, goblin.Id));

        state.GameLog.Should().Contain(m => m.Contains("casts"));
    }

    // --- Part C: Sandbox Fallback ---

    [Fact]
    public async Task SandboxCard_NoManaCost_PlaysFreely()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var widget = new GameCard { Name = "Unknown Widget" };
        state.Player1.Hand.Add(widget);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, widget.Id));

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == widget.Id);
        state.Player1.Hand.Cards.Should().NotContain(c => c.Id == widget.Id);
    }

    [Fact]
    public async Task SandboxCard_GoesToBattlefield()
    {
        var (engine, state, _) = CreateSetup();
        await engine.StartGameAsync();

        var widget = new GameCard { Name = "Unknown Widget" };
        state.Player1.Hand.Add(widget);

        await engine.ExecuteAction(GameAction.PlayCard(state.Player1.Id, widget.Id));

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == widget.Id);
    }
}
