using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class ActivatedTriggeredIntegrationTests
{
    [Fact]
    public async Task Sharpshooter_Untap_Chain_Kills_Multiple_1Toughness_Creatures()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var shooter = GameCard.Create("Goblin Sharpshooter", "Creature — Goblin");
        p1.Battlefield.Add(shooter);

        var bird1 = new GameCard { Name = "Bird1", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        var bird2 = new GameCard { Name = "Bird2", CardTypes = CardType.Creature, BasePower = 1, BaseToughness = 1 };
        p2.Battlefield.Add(bird1);
        p2.Battlefield.Add(bird2);

        // Tap Sharpshooter to kill bird1
        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, shooter.Id, targetId: bird1.Id));
        // Triggers now queue on the stack — resolve them so the untap trigger fires
        await engine.ResolveAllTriggersAsync();

        // bird1 should die from SBA, triggering AnyCreatureDies -> untap Sharpshooter
        shooter.IsTapped.Should().BeFalse("Sharpshooter should untap when bird1 dies");

        // Now tap again to kill bird2
        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, shooter.Id, targetId: bird2.Id));
        await engine.ResolveAllTriggersAsync();

        shooter.IsTapped.Should().BeFalse("Sharpshooter should untap when bird2 dies");
        p2.Battlefield.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task Enchantress_Draws_When_Enchantment_Cast()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var enchantress = GameCard.Create("Argothian Enchantress", "Creature — Human Druid");
        p1.Battlefield.Add(enchantress);

        var drawTarget = new GameCard { Name = "DrawnCard" };
        p1.Library.Add(drawTarget);

        // Cast an enchantment
        var enchantment = GameCard.Create("Wild Growth", "Enchantment — Aura");
        p1.Hand.Add(enchantment);
        p1.ManaPool.Add(ManaColor.Green);

        await engine.ExecuteAction(GameAction.PlayCard(p1.Id, enchantment.Id));
        // Triggers now queue on the stack — resolve them so the draw trigger fires
        await engine.ResolveAllTriggersAsync();

        // Enchantress should have triggered and drawn a card
        p1.Hand.Cards.Should().Contain(c => c.Name == "DrawnCard");
    }

    [Fact]
    public async Task Skirk_Prospector_Sacrifice_Goblin_For_Mana()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var prospector = GameCard.Create("Skirk Prospector", "Creature — Goblin");
        var token = new GameCard { Name = "Goblin", CardTypes = CardType.Creature, Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 1, IsToken = true };
        p1.Battlefield.Add(prospector);
        p1.Battlefield.Add(token);

        handler.EnqueueCardChoice(token.Id);

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, prospector.Id));

        p1.Battlefield.Cards.Should().NotContain(c => c.Id == token.Id);
        p1.ManaPool.Available.Should().ContainKey(ManaColor.Red);
        p1.ManaPool.Available[ManaColor.Red].Should().Be(1);
    }

    [Fact]
    public async Task SealOfCleansing_Destroys_Artifact()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        var seal = GameCard.Create("Seal of Cleansing", "Enchantment");
        p1.Battlefield.Add(seal);

        var artifact = new GameCard { Name = "Sol Ring", CardTypes = CardType.Artifact };
        p2.Battlefield.Add(artifact);

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, seal.Id, targetId: artifact.Id));

        p1.Battlefield.Cards.Should().NotContain(c => c.Id == seal.Id);
        p2.Battlefield.Cards.Should().NotContain(c => c.Id == artifact.Id);
        p2.Graveyard.Cards.Should().Contain(c => c.Name == "Sol Ring");
    }

    [Fact]
    public async Task Pyromancer_Pumps_Then_Destroys_At_End_Of_Turn()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var goblin1 = new GameCard { Name = "Goblin Lackey", CardTypes = CardType.Creature, Subtypes = ["Goblin"], BasePower = 1, BaseToughness = 1 };
        p1.Battlefield.Add(goblin1);

        var pyro = GameCard.Create("Goblin Pyromancer", "Creature — Goblin");
        p1.Hand.Add(pyro);

        // 4 Mountains to tap for mana (Pyromancer costs {3}{R})
        var mountains = new List<GameCard>();
        for (int i = 0; i < 4; i++)
        {
            var mtn = GameCard.Create("Mountain", "Basic Land — Mountain");
            mtn.TurnEnteredBattlefield = 0; // entered on a previous turn, no summoning sickness
            p1.Battlefield.Add(mtn);
            mountains.Add(mtn);
        }

        for (int i = 0; i < 5; i++)
            p1.Library.Add(new GameCard { Name = $"P1Card{i}" });
        for (int i = 0; i < 5; i++)
            p2.Library.Add(new GameCard { Name = $"P2Card{i}" });

        // Main phase 1 actions: tap 4 mountains, then cast Pyromancer, then pass
        foreach (var mtn in mountains)
            handler.EnqueueAction(GameAction.TapCard(p1.Id, mtn.Id));
        handler.EnqueueAction(GameAction.PlayCard(p1.Id, pyro.Id));
        handler.EnqueueAction(GameAction.Pass(p1.Id)); // main1 done
        handler.EnqueueAction(GameAction.Pass(p1.Id)); // main2 done

        await engine.RunTurnAsync();

        // After end of turn, all Goblins should be destroyed by delayed trigger
        p1.Battlefield.Cards.Where(c => c.Subtypes.Contains("Goblin")).Should().BeEmpty();
    }
}
