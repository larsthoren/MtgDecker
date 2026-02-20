using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class AdventureTests
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

    // --- Record & Property Tests ---

    [Fact]
    public void AdventurePart_CanBeCreated()
    {
        var cost = ManaCost.Parse("{1}{U}");
        var effect = new PettyTheftEffect();
        var filter = TargetFilter.NonlandPermanent();
        var adventure = new AdventurePart("Petty Theft", cost, filter, effect);

        adventure.Name.Should().Be("Petty Theft");
        adventure.Cost.Should().Be(cost);
        adventure.Effect.Should().Be(effect);
        adventure.Filter.Should().Be(filter);
    }

    [Fact]
    public void CardDefinition_WithAdventure_HasAdventurePart()
    {
        CardDefinitions.TryGet("Brazen Borrower", out var def).Should().BeTrue();
        def!.Adventure.Should().NotBeNull();
        def.Adventure!.Name.Should().Be("Petty Theft");
        def.Adventure.Cost.Should().NotBeNull();
        def.Adventure.Effect.Should().BeOfType<PettyTheftEffect>();
    }

    [Fact]
    public void GameCard_IsOnAdventure_DefaultsFalse()
    {
        var card = new GameCard { Name = "Test" };
        card.IsOnAdventure.Should().BeFalse();
    }

    // --- CastAdventure Stack Tests ---

    [Fact]
    public async Task CastAdventure_PutsOnStack_WithAdventureCost()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var borrower = GameCard.Create("Brazen Borrower");
        state.Player1.Hand.Add(borrower);

        // Petty Theft costs {1}{U} — add sufficient mana
        state.Player1.ManaPool.Add(ManaColor.Blue, 1);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 1);

        // Opponent has a nonland permanent to target
        var oppCreature = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        state.Player2.Battlefield.Add(oppCreature);
        h1.EnqueueTarget(new TargetInfo(oppCreature.Id, state.Player2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastAdventure(state.Player1.Id, borrower.Id));

        state.Stack.Should().HaveCount(1);
        state.Player1.Hand.Cards.Should().NotContain(c => c.Id == borrower.Id);
    }

    [Fact]
    public async Task CastAdventure_OnResolution_GoesToExile()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var borrower = GameCard.Create("Brazen Borrower");
        state.Player1.Hand.Add(borrower);
        state.Player1.ManaPool.Add(ManaColor.Blue, 1);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 1);

        var oppCreature = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        state.Player2.Battlefield.Add(oppCreature);
        h1.EnqueueTarget(new TargetInfo(oppCreature.Id, state.Player2.Id, ZoneType.Battlefield));

        await engine.ExecuteAction(GameAction.CastAdventure(state.Player1.Id, borrower.Id));

        // Resolve
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        await engine.RunPriorityAsync();

        // Card should be in exile with IsOnAdventure = true
        state.Player1.Exile.Cards.Should().Contain(c => c.Id == borrower.Id);
        var exiledCard = state.Player1.Exile.Cards.First(c => c.Id == borrower.Id);
        exiledCard.IsOnAdventure.Should().BeTrue();
        state.Stack.Should().BeEmpty();
    }

    // --- Cast from Exile Tests ---

    [Fact]
    public async Task CastFromExile_WhenOnAdventure_AllowsCreatureCast()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        // Put Brazen Borrower in exile as if adventure already resolved
        var borrower = GameCard.Create("Brazen Borrower");
        borrower.IsOnAdventure = true;
        state.Player1.Exile.Add(borrower);

        // Add mana for creature side: {1}{U}{U}
        state.Player1.ManaPool.Add(ManaColor.Blue, 2);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 1);

        // Cast creature side from exile via normal CastSpell
        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, borrower.Id));

        // Should be on the stack
        state.Stack.Should().HaveCount(1);
        state.Player1.Exile.Cards.Should().NotContain(c => c.Id == borrower.Id);
    }

    [Fact]
    public async Task CastFromExile_EntersBattlefield_IsOnAdventureFalse()
    {
        var (engine, state, h1, h2) = CreateSetup();
        await engine.StartGameAsync();
        state.CurrentPhase = Phase.MainPhase1;

        var borrower = GameCard.Create("Brazen Borrower");
        borrower.IsOnAdventure = true;
        state.Player1.Exile.Add(borrower);

        state.Player1.ManaPool.Add(ManaColor.Blue, 2);
        state.Player1.ManaPool.Add(ManaColor.Colorless, 1);

        await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, borrower.Id));

        // Resolve
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        h1.EnqueueAction(GameAction.Pass(state.Player1.Id));
        h2.EnqueueAction(GameAction.Pass(state.Player2.Id));
        await engine.RunPriorityAsync();

        // Should be on the battlefield as a creature
        state.Player1.Battlefield.Cards.Should().Contain(c => c.Id == borrower.Id);
        var onBattlefield = state.Player1.Battlefield.Cards.First(c => c.Id == borrower.Id);
        onBattlefield.IsOnAdventure.Should().BeFalse();
        onBattlefield.IsCreature.Should().BeTrue();
    }

    // --- PettyTheftEffect Tests ---

    [Fact]
    public void PettyTheftEffect_BouncesNonlandPermanent()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        for (int i = 0; i < 10; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }
        var state = new GameState(p1, p2);

        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        p2.Battlefield.Add(creature);

        var borrower = new GameCard { Name = "Brazen Borrower" };
        var targets = new List<TargetInfo> { new(creature.Id, p2.Id, ZoneType.Battlefield) };
        var manaPaid = new Dictionary<ManaColor, int> { { ManaColor.Blue, 1 }, { ManaColor.Colorless, 1 } };
        var stackObj = new StackObject(borrower, p1.Id, manaPaid, targets, 0);

        var effect = new PettyTheftEffect();
        effect.Resolve(state, stackObj);

        p2.Battlefield.Cards.Should().NotContain(c => c.Id == creature.Id);
        p2.Hand.Cards.Should().Contain(c => c.Id == creature.Id);
    }

    [Fact]
    public void PettyTheftEffect_CannotBounceOwnPermanent()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        for (int i = 0; i < 10; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }
        var state = new GameState(p1, p2);

        // P1 owns a creature, targets their own
        var ownCreature = new GameCard { Name = "OwnBear", CardTypes = CardType.Creature };
        p1.Battlefield.Add(ownCreature);

        var borrower = new GameCard { Name = "Brazen Borrower" };
        // Target points to p1's own creature — controllerId is p1
        var targets = new List<TargetInfo> { new(ownCreature.Id, p1.Id, ZoneType.Battlefield) };
        var manaPaid = new Dictionary<ManaColor, int>();
        var stackObj = new StackObject(borrower, p1.Id, manaPaid, targets, 0);

        var effect = new PettyTheftEffect();
        effect.Resolve(state, stackObj);

        // Should NOT have bounced — still on battlefield
        p1.Battlefield.Cards.Should().Contain(c => c.Id == ownCreature.Id);
        p1.Hand.Cards.Should().NotContain(c => c.Id == ownCreature.Id);
    }

    [Fact]
    public void PettyTheftEffect_CannotBounceLand()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        for (int i = 0; i < 10; i++)
        {
            p1.Library.Add(new GameCard { Name = $"Card{i}" });
            p2.Library.Add(new GameCard { Name = $"Card{i}" });
        }
        var state = new GameState(p1, p2);

        var land = new GameCard { Name = "Island", CardTypes = CardType.Land };
        p2.Battlefield.Add(land);

        var borrower = new GameCard { Name = "Brazen Borrower" };
        var targets = new List<TargetInfo> { new(land.Id, p2.Id, ZoneType.Battlefield) };
        var manaPaid = new Dictionary<ManaColor, int>();
        var stackObj = new StackObject(borrower, p1.Id, manaPaid, targets, 0);

        var effect = new PettyTheftEffect();
        effect.Resolve(state, stackObj);

        // Land should remain on battlefield
        p2.Battlefield.Cards.Should().Contain(c => c.Id == land.Id);
        p2.Hand.Cards.Should().NotContain(c => c.Id == land.Id);
    }

    // --- Brazen Borrower Registration Tests ---

    [Fact]
    public void BrazenBorrower_Registration_HasCorrectStats()
    {
        CardDefinitions.TryGet("Brazen Borrower", out var def).Should().BeTrue();
        def!.ManaCost.Should().NotBeNull();
        def.ManaCost!.ConvertedManaCost.Should().Be(3); // {1}{U}{U}
        def.Power.Should().Be(3);
        def.Toughness.Should().Be(1);
        def.CardTypes.Should().Be(CardType.Creature);
        def.Subtypes.Should().Contain("Faerie");
        def.Subtypes.Should().Contain("Rogue");
    }

    [Fact]
    public void BrazenBorrower_Registration_HasAdventure()
    {
        CardDefinitions.TryGet("Brazen Borrower", out var def).Should().BeTrue();
        def!.Adventure.Should().NotBeNull();
        def.Adventure!.Name.Should().Be("Petty Theft");
        def.Adventure.Cost.ConvertedManaCost.Should().Be(2); // {1}{U}
        def.Adventure.Effect.Should().BeOfType<PettyTheftEffect>();
        def.Adventure.Filter.Should().NotBeNull();
    }

    [Fact]
    public void BrazenBorrower_HasFlash()
    {
        CardDefinitions.TryGet("Brazen Borrower", out var def).Should().BeTrue();
        def!.HasFlash.Should().BeTrue();
    }
}
