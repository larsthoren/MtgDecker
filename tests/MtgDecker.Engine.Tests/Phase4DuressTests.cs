using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class Phase4DuressTests
{
    private TestDecisionHandler _h1 = null!;
    private TestDecisionHandler _h2 = null!;
    private Player _p1 = null!;
    private Player _p2 = null!;
    private GameState _state = null!;

    private void Setup()
    {
        _h1 = new TestDecisionHandler();
        _h2 = new TestDecisionHandler();
        _p1 = new Player(Guid.NewGuid(), "P1", _h1);
        _p2 = new Player(Guid.NewGuid(), "P2", _h2);
        _state = new GameState(_p1, _p2);
    }

    private StackObject CreateDuressSpell()
    {
        var duress = new GameCard { Name = "Duress" };
        return new StackObject(duress, _p1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(Guid.Empty, _p2.Id, ZoneType.None) }, 0);
    }

    [Fact]
    public async Task Duress_CasterChoosesCardToDiscard()
    {
        // Arrange
        Setup();
        var instant = new GameCard { Name = "Dark Ritual", CardTypes = CardType.Instant };
        var sorcery = new GameCard { Name = "Ponder", CardTypes = CardType.Sorcery };
        _p2.Hand.Add(instant);
        _p2.Hand.Add(sorcery);

        // Caster picks the sorcery specifically
        _h1.EnqueueCardChoice(sorcery.Id);

        var spell = CreateDuressSpell();
        var effect = new DuressEffect();

        // Act
        await effect.ResolveAsync(_state, spell, _h1);

        // Assert — sorcery was discarded, instant remains
        _p2.Hand.Cards.Should().NotContain(c => c.Id == sorcery.Id);
        _p2.Graveyard.Cards.Should().Contain(c => c.Id == sorcery.Id);
        _p2.Hand.Cards.Should().Contain(c => c.Id == instant.Id);
    }

    [Fact]
    public async Task Duress_FiltersOutCreaturesAndLands()
    {
        // Arrange
        Setup();
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        var land = new GameCard { Name = "Swamp", CardTypes = CardType.Land };
        var instant = new GameCard { Name = "Lightning Bolt", CardTypes = CardType.Instant };
        _p2.Hand.Add(creature);
        _p2.Hand.Add(land);
        _p2.Hand.Add(instant);

        // Caster picks the instant (the only eligible card)
        _h1.EnqueueCardChoice(instant.Id);

        var spell = CreateDuressSpell();
        var effect = new DuressEffect();

        // Act
        await effect.ResolveAsync(_state, spell, _h1);

        // Assert — only instant was discarded; creature and land remain
        _p2.Hand.Cards.Should().NotContain(c => c.Id == instant.Id);
        _p2.Graveyard.Cards.Should().Contain(c => c.Id == instant.Id);
        _p2.Hand.Cards.Should().Contain(c => c.Id == creature.Id);
        _p2.Hand.Cards.Should().Contain(c => c.Id == land.Id);
    }

    [Fact]
    public async Task Duress_DoesNothing_WhenNoEligibleCards()
    {
        // Arrange
        Setup();
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        var land = new GameCard { Name = "Forest", CardTypes = CardType.Land };
        _p2.Hand.Add(creature);
        _p2.Hand.Add(land);

        var spell = CreateDuressSpell();
        var effect = new DuressEffect();

        // Act
        await effect.ResolveAsync(_state, spell, _h1);

        // Assert — no cards discarded, hand unchanged
        _p2.Hand.Count.Should().Be(2);
        _p2.Graveyard.Count.Should().Be(0);
        _state.GameLog.Should().Contain(m => m.Contains("No eligible cards"));
    }

    [Fact]
    public async Task Duress_DiscardsChosenCard_ToGraveyard()
    {
        // Arrange
        Setup();
        var enchantment = new GameCard { Name = "Wild Growth", CardTypes = CardType.Enchantment };
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        _p2.Hand.Add(enchantment);
        _p2.Hand.Add(creature);

        _h1.EnqueueCardChoice(enchantment.Id);

        var spell = CreateDuressSpell();
        var effect = new DuressEffect();

        // Act
        await effect.ResolveAsync(_state, spell, _h1);

        // Assert — enchantment moved from hand to graveyard
        _p2.Hand.Cards.Should().NotContain(c => c.Id == enchantment.Id);
        _p2.Graveyard.Cards.Should().Contain(c => c.Id == enchantment.Id);
        _p2.Hand.Cards.Should().Contain(c => c.Id == creature.Id);
        _state.GameLog.Should().Contain(m => m.Contains("discards") && m.Contains("Wild Growth") && m.Contains("Duress"));
    }
}
