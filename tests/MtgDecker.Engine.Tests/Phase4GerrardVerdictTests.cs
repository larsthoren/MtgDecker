using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class Phase4GerrardVerdictTests
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

    private StackObject CreateVerdictSpell(GameState state, Guid casterId, Guid targetPlayerId)
    {
        var verdictCard = new GameCard { Name = "Gerrard's Verdict" };
        return new StackObject(verdictCard, casterId,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(Guid.Empty, targetPlayerId, ZoneType.None) }, 0);
    }

    [Fact]
    public async Task GerrardVerdict_TargetDiscards2Cards()
    {
        // Arrange: target has 3 cards, discards 2 (their choice)
        var (state, h1, h2) = CreateSetup();
        var land1 = new GameCard { Name = "Plains", CardTypes = CardType.Land };
        var spell1 = new GameCard { Name = "Dark Ritual", CardTypes = CardType.Instant };
        var spell2 = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        state.Player2.Hand.Add(land1);
        state.Player2.Hand.Add(spell1);
        state.Player2.Hand.Add(spell2);

        // Target player chooses which cards to discard
        h2.EnqueueCardChoice(spell1.Id);
        h2.EnqueueCardChoice(spell2.Id);

        var spellObj = CreateVerdictSpell(state, state.Player1.Id, state.Player2.Id);
        var effect = new GerrardVerdictEffect();

        // Act
        await effect.ResolveAsync(state, spellObj, h1);

        // Assert
        state.Player2.Hand.Count.Should().Be(1);
        state.Player2.Hand.Cards[0].Id.Should().Be(land1.Id);
        state.Player2.Graveyard.Count.Should().Be(2);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == spell1.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == spell2.Id);
    }

    [Fact]
    public async Task GerrardVerdict_CasterGains3PerLand()
    {
        // Arrange: target discards 2 lands -> caster gains 6 life (26 total)
        var (state, h1, h2) = CreateSetup();
        var land1 = new GameCard { Name = "Plains", CardTypes = CardType.Land };
        var land2 = new GameCard { Name = "Swamp", CardTypes = CardType.Land };
        var spell1 = new GameCard { Name = "Dark Ritual", CardTypes = CardType.Instant };
        state.Player2.Hand.Add(land1);
        state.Player2.Hand.Add(land2);
        state.Player2.Hand.Add(spell1);

        h2.EnqueueCardChoice(land1.Id);
        h2.EnqueueCardChoice(land2.Id);

        var spellObj = CreateVerdictSpell(state, state.Player1.Id, state.Player2.Id);
        var effect = new GerrardVerdictEffect();

        // Act
        await effect.ResolveAsync(state, spellObj, h1);

        // Assert
        state.Player1.Life.Should().Be(26, "2 lands discarded x 3 life each = 6 life gained");
        state.Player2.Hand.Count.Should().Be(1);
        state.Player2.Graveyard.Count.Should().Be(2);
    }

    [Fact]
    public async Task GerrardVerdict_NoLifeGain_WhenNoLandsDiscarded()
    {
        // Arrange: target discards 2 non-lands -> caster stays at 20
        var (state, h1, h2) = CreateSetup();
        var spell1 = new GameCard { Name = "Dark Ritual", CardTypes = CardType.Instant };
        var spell2 = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        var spell3 = new GameCard { Name = "Counterspell", CardTypes = CardType.Instant };
        state.Player2.Hand.Add(spell1);
        state.Player2.Hand.Add(spell2);
        state.Player2.Hand.Add(spell3);

        h2.EnqueueCardChoice(spell1.Id);
        h2.EnqueueCardChoice(spell2.Id);

        var spellObj = CreateVerdictSpell(state, state.Player1.Id, state.Player2.Id);
        var effect = new GerrardVerdictEffect();

        // Act
        await effect.ResolveAsync(state, spellObj, h1);

        // Assert
        state.Player1.Life.Should().Be(20, "no lands were discarded so no life gain");
        state.Player2.Hand.Count.Should().Be(1);
        state.Player2.Graveyard.Count.Should().Be(2);
    }

    [Fact]
    public async Task GerrardVerdict_PartialDiscard_WhenOnlyOneCard()
    {
        // Arrange: target has only 1 card, discards only 1
        var (state, h1, h2) = CreateSetup();
        var spell1 = new GameCard { Name = "Dark Ritual", CardTypes = CardType.Instant };
        state.Player2.Hand.Add(spell1);

        h2.EnqueueCardChoice(spell1.Id);

        var spellObj = CreateVerdictSpell(state, state.Player1.Id, state.Player2.Id);
        var effect = new GerrardVerdictEffect();

        // Act
        await effect.ResolveAsync(state, spellObj, h1);

        // Assert
        state.Player2.Hand.Count.Should().Be(0);
        state.Player2.Graveyard.Count.Should().Be(1);
        state.Player2.Graveyard.Cards[0].Id.Should().Be(spell1.Id);
        state.Player1.Life.Should().Be(20, "no lands discarded");
    }

    [Fact]
    public async Task GerrardVerdict_GainsLife_ForMixedDiscard()
    {
        // Arrange: target discards 1 land + 1 spell -> caster gains 3
        var (state, h1, h2) = CreateSetup();
        var land1 = new GameCard { Name = "Plains", CardTypes = CardType.Land };
        var spell1 = new GameCard { Name = "Dark Ritual", CardTypes = CardType.Instant };
        state.Player2.Hand.Add(land1);
        state.Player2.Hand.Add(spell1);

        h2.EnqueueCardChoice(land1.Id);
        h2.EnqueueCardChoice(spell1.Id);

        var spellObj = CreateVerdictSpell(state, state.Player1.Id, state.Player2.Id);
        var effect = new GerrardVerdictEffect();

        // Act
        await effect.ResolveAsync(state, spellObj, h1);

        // Assert
        state.Player1.Life.Should().Be(23, "1 land discarded x 3 life = 3 life gained");
        state.Player2.Hand.Count.Should().Be(0);
        state.Player2.Graveyard.Count.Should().Be(2);
    }
}
