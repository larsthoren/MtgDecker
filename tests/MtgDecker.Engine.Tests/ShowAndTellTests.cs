using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

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
