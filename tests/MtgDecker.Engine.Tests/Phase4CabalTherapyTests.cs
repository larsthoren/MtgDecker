using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class Phase4CabalTherapyTests
{
    private (GameState state, TestDecisionHandler h1, TestDecisionHandler h2) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        return (state, h1, h2);
    }

    private StackObject CreateTherapySpell(GameState state, Guid casterId, Guid targetPlayerId)
    {
        var therapyCard = new GameCard { Name = "Cabal Therapy" };
        return new StackObject(therapyCard, casterId,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new TargetInfo(Guid.Empty, targetPlayerId, ZoneType.None) },
            0);
    }

    [Fact]
    public async Task CabalTherapy_DiscardsAllCopiesOfNamedCard()
    {
        // Arrange: target hand has 3 Lightning Bolts + 1 Mountain
        var (state, h1, h2) = CreateSetup();
        var p1 = state.Player1;
        var p2 = state.Player2;

        var bolt1 = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        var bolt2 = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        var bolt3 = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        var land = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        p2.Hand.Add(bolt1);
        p2.Hand.Add(bolt2);
        p2.Hand.Add(bolt3);
        p2.Hand.Add(land);

        // Caster names "Lightning Bolt" by choosing one of the bolts
        h1.EnqueueCardChoice(bolt1.Id);

        var spell = CreateTherapySpell(state, p1.Id, p2.Id);
        var effect = new CabalTherapyEffect();

        // Act
        await effect.ResolveAsync(state, spell, h1);

        // Assert: all 3 Bolts should be discarded, Mountain remains
        p2.Hand.Count.Should().Be(1);
        p2.Hand.Cards.Should().AllSatisfy(c => c.Name.Should().Be("Mountain"));
        p2.Graveyard.Count.Should().Be(3);
        p2.Graveyard.Cards.Should().AllSatisfy(c => c.Name.Should().Be("Lightning Bolt"));
    }

    [Fact]
    public async Task CabalTherapy_DiscardsSingleCard_WhenOnlyOneCopy()
    {
        // Arrange: target hand has 1 Lightning Bolt + 1 Giant Growth + 1 Forest
        var (state, h1, h2) = CreateSetup();
        var p1 = state.Player1;
        var p2 = state.Player2;

        var bolt = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        var growth = new GameCard { Name = "Giant Growth", CardTypes = CardType.Instant };
        var forest = new GameCard { Name = "Forest", CardTypes = CardType.Land };
        p2.Hand.Add(bolt);
        p2.Hand.Add(growth);
        p2.Hand.Add(forest);

        // Caster names "Lightning Bolt"
        h1.EnqueueCardChoice(bolt.Id);

        var spell = CreateTherapySpell(state, p1.Id, p2.Id);
        var effect = new CabalTherapyEffect();

        // Act
        await effect.ResolveAsync(state, spell, h1);

        // Assert: only the 1 Bolt is discarded
        p2.Hand.Count.Should().Be(2);
        p2.Hand.Cards.Select(c => c.Name).Should().BeEquivalentTo(["Giant Growth", "Forest"]);
        p2.Graveyard.Count.Should().Be(1);
        p2.Graveyard.Cards[0].Name.Should().Be("Lightning Bolt");
    }

    [Fact]
    public async Task CabalTherapy_DoesNothing_WhenNoNonlandCards()
    {
        // Arrange: target hand is all lands
        var (state, h1, h2) = CreateSetup();
        var p1 = state.Player1;
        var p2 = state.Player2;

        var mountain = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        var forest = new GameCard { Name = "Forest", CardTypes = CardType.Land };
        p2.Hand.Add(mountain);
        p2.Hand.Add(forest);

        var spell = CreateTherapySpell(state, p1.Id, p2.Id);
        var effect = new CabalTherapyEffect();

        // Act
        await effect.ResolveAsync(state, spell, h1);

        // Assert: nothing changed
        p2.Hand.Count.Should().Be(2);
        p2.Graveyard.Count.Should().Be(0);
    }

    [Fact]
    public async Task CabalTherapy_FiltersOutLands()
    {
        // Arrange: target hand has lands + spells, only spells shown as options
        var (state, h1, h2) = CreateSetup();
        var p1 = state.Player1;
        var p2 = state.Player2;

        var bolt = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        var growth = new GameCard { Name = "Giant Growth", CardTypes = CardType.Instant };
        var mountain = new GameCard { Name = "Mountain", CardTypes = CardType.Land };
        var forest = new GameCard { Name = "Forest", CardTypes = CardType.Land };
        p2.Hand.Add(bolt);
        p2.Hand.Add(growth);
        p2.Hand.Add(mountain);
        p2.Hand.Add(forest);

        // Caster names "Lightning Bolt"
        h1.EnqueueCardChoice(bolt.Id);

        var spell = CreateTherapySpell(state, p1.Id, p2.Id);
        var effect = new CabalTherapyEffect();

        // Act
        await effect.ResolveAsync(state, spell, h1);

        // Assert: lands were not discarded (only the named card was)
        p2.Hand.Count.Should().Be(3);
        p2.Hand.Cards.Select(c => c.Name).Should().BeEquivalentTo(["Giant Growth", "Mountain", "Forest"]);
        // Only 1 card discarded (the Bolt)
        p2.Graveyard.Count.Should().Be(1);
        p2.Graveyard.Cards[0].Name.Should().Be("Lightning Bolt");
    }
}
