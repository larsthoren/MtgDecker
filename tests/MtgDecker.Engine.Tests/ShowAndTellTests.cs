using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class ShowAndTellTests
{
    [Fact]
    public async Task ShowAndTell_BothPlayersChoosePermanent_BothEnterBattlefield()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();

        var emrakul = new GameCard { Name = "Emrakul", CardTypes = CardType.Creature, Power = 15, Toughness = 15 };
        state.Player1.Hand.Add(emrakul);

        var bear = new GameCard { Name = "Grizzly Bears", CardTypes = CardType.Creature, Power = 2, Toughness = 2 };
        state.Player2.Hand.Add(bear);

        h1.EnqueueCardChoice(emrakul.Id); // Caster chooses Emrakul
        h2.EnqueueCardChoice(bear.Id);     // Opponent chooses Bear

        var showAndTell = GameCard.Create("Show and Tell");
        var spell = new StackObject(showAndTell, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        var effect = new ShowAndTellEffect();
        await effect.ResolveAsync(state, spell, h1);

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Emrakul");
        state.Player1.Hand.Cards.Should().NotContain(c => c.Name == "Emrakul");
        state.Player2.Battlefield.Cards.Should().Contain(c => c.Name == "Grizzly Bears");
        state.Player2.Hand.Cards.Should().NotContain(c => c.Name == "Grizzly Bears");
    }

    [Fact]
    public async Task ShowAndTell_PlayerDeclinesChoosing_OnlyOtherCardEnters()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();

        var emrakul = new GameCard { Name = "Emrakul", CardTypes = CardType.Creature, Power = 15, Toughness = 15 };
        state.Player1.Hand.Add(emrakul);

        h1.EnqueueCardChoice(emrakul.Id);
        h2.EnqueueCardChoice((Guid?)null); // Opponent declines

        var showAndTell = GameCard.Create("Show and Tell");
        var spell = new StackObject(showAndTell, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        var effect = new ShowAndTellEffect();
        await effect.ResolveAsync(state, spell, h1);

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Emrakul");
        state.Player2.Battlefield.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task ShowAndTell_OnlyPermanentsEligible_InstantsNotOffered()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();

        var bolt = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        var bear = new GameCard { Name = "Bear", CardTypes = CardType.Creature, Power = 2, Toughness = 2 };
        state.Player1.Hand.Add(bolt);
        state.Player1.Hand.Add(bear);

        h1.EnqueueCardChoice(bear.Id); // Should only see Bear, not Bolt
        h2.EnqueueCardChoice((Guid?)null);

        var showAndTell = GameCard.Create("Show and Tell");
        var spell = new StackObject(showAndTell, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        var effect = new ShowAndTellEffect();
        await effect.ResolveAsync(state, spell, h1);

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Bear");
        state.Player1.Hand.Cards.Should().Contain(c => c.Name == "Lightning Bolt");
    }

    [Fact]
    public async Task ShowAndTell_NoPermanentsInHand_SkipsPlayer()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();

        // Player 1 only has instants/sorceries
        var bolt = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        state.Player1.Hand.Add(bolt);

        var bear = new GameCard { Name = "Bear", CardTypes = CardType.Creature, Power = 2, Toughness = 2 };
        state.Player2.Hand.Add(bear);
        h2.EnqueueCardChoice(bear.Id);

        var showAndTell = GameCard.Create("Show and Tell");
        var spell = new StackObject(showAndTell, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        var effect = new ShowAndTellEffect();
        await effect.ResolveAsync(state, spell, h1);

        state.Player1.Battlefield.Cards.Should().BeEmpty();
        state.Player2.Battlefield.Cards.Should().Contain(c => c.Name == "Bear");
    }

    [Fact]
    public async Task ShowAndTell_BothPlayersDecline_NothingEnters()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();

        var creature1 = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        state.Player1.Hand.Add(creature1);
        var creature2 = new GameCard { Name = "Wolf", CardTypes = CardType.Creature };
        state.Player2.Hand.Add(creature2);

        h1.EnqueueCardChoice((Guid?)null);
        h2.EnqueueCardChoice((Guid?)null);

        var showAndTell = GameCard.Create("Show and Tell");
        var spell = new StackObject(showAndTell, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        var effect = new ShowAndTellEffect();
        await effect.ResolveAsync(state, spell, h1);

        state.Player1.Battlefield.Cards.Should().BeEmpty();
        state.Player2.Battlefield.Cards.Should().BeEmpty();
    }

    [Fact]
    public async Task ShowAndTell_FiresEtbTriggers_WhenCreatureEntersBattlefield()
    {
        // Test through the engine to verify ETB triggers fire
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        state.ActivePlayer = p1;
        state.TurnNumber = 2;
        state.CurrentPhase = Phase.MainPhase1;

        // Give mana for Show and Tell ({1}{U}{U})
        p1.ManaPool.Add(ManaColor.Blue, 2);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        var showAndTell = GameCard.Create("Show and Tell");
        p1.Hand.Add(showAndTell);

        // Siege-Gang Commander has ETB: create 3 Goblin tokens
        var commander = new GameCard
        {
            Name = "Siege-Gang Commander",
            CardTypes = CardType.Creature,
            ManaCost = ManaCost.Parse("{3}{R}{R}"),
            Power = 2,
            Toughness = 2,
            Subtypes = ["Goblin"],
            Triggers = [
                new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self,
                    new CreateTokensEffect("Goblin", 1, 1, CardType.Creature, ["Goblin"], count: 3))
            ]
        };
        p1.Hand.Add(commander);

        h1.EnqueueCardChoice(commander.Id); // P1 chooses Siege-Gang
        h2.EnqueueCardChoice((Guid?)null);  // P2 declines

        await engine.ExecuteAction(GameAction.CastSpell(p1.Id, showAndTell.Id));
        await engine.ResolveAllTriggersAsync();

        // Commander should be on battlefield
        p1.Battlefield.Cards.Should().Contain(c => c.Name == "Siege-Gang Commander");

        // ETB should have fired: 3 Goblin tokens
        p1.Battlefield.Cards.Where(c => c.Name == "Goblin" && c.IsToken).Should().HaveCount(3);
    }

    [Fact]
    public async Task ShowAndTell_FiresEtbTriggers_ForBothPlayers()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        state.ActivePlayer = p1;
        state.TurnNumber = 2;
        state.CurrentPhase = Phase.MainPhase1;

        p1.ManaPool.Add(ManaColor.Blue, 2);
        p1.ManaPool.Add(ManaColor.Colorless, 1);

        var showAndTell = GameCard.Create("Show and Tell");
        p1.Hand.Add(showAndTell);

        // P1's creature with ETB
        var p1Creature = new GameCard
        {
            Name = "Siege-Gang Commander",
            CardTypes = CardType.Creature,
            Power = 2, Toughness = 2,
            Subtypes = ["Goblin"],
            Triggers = [
                new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self,
                    new CreateTokensEffect("Goblin", 1, 1, CardType.Creature, ["Goblin"], count: 3))
            ]
        };
        p1.Hand.Add(p1Creature);

        // P2's creature with ETB
        var p2Creature = new GameCard
        {
            Name = "Goblin Matron",
            CardTypes = CardType.Creature,
            Power = 1, Toughness = 1,
            Subtypes = ["Goblin"],
            Triggers = [
                new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self,
                    new CreateTokensEffect("Token", 1, 1, CardType.Creature, ["Goblin"], count: 1))
            ]
        };
        p2.Hand.Add(p2Creature);

        h1.EnqueueCardChoice(p1Creature.Id);
        h2.EnqueueCardChoice(p2Creature.Id);

        await engine.ExecuteAction(GameAction.CastSpell(p1.Id, showAndTell.Id));
        await engine.ResolveAllTriggersAsync();

        // P1 gets 3 tokens from Siege-Gang
        p1.Battlefield.Cards.Where(c => c.IsToken).Should().HaveCount(3);
        // P2 gets 1 token from their creature's ETB
        p2.Battlefield.Cards.Where(c => c.IsToken).Should().HaveCount(1);
    }

    [Fact]
    public async Task ShowAndTell_ArtifactAndEnchantmentAreValidPermanents()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();

        var artifact = new GameCard { Name = "Mox Diamond", CardTypes = CardType.Artifact };
        state.Player1.Hand.Add(artifact);

        var enchantment = new GameCard { Name = "Omniscience", CardTypes = CardType.Enchantment };
        state.Player2.Hand.Add(enchantment);

        h1.EnqueueCardChoice(artifact.Id);
        h2.EnqueueCardChoice(enchantment.Id);

        var showAndTell = GameCard.Create("Show and Tell");
        var spell = new StackObject(showAndTell, state.Player1.Id,
            new Dictionary<ManaColor, int>(), new List<TargetInfo>(), 0);

        var effect = new ShowAndTellEffect();
        await effect.ResolveAsync(state, spell, h1);

        state.Player1.Battlefield.Cards.Should().Contain(c => c.Name == "Mox Diamond");
        state.Player2.Battlefield.Cards.Should().Contain(c => c.Name == "Omniscience");
    }
}
