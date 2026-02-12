using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class SpellEffectTests
{
    private GameState CreateState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        return new GameState(p1, p2);
    }

    [Fact]
    public void SwordsToPlowshares_ExilesCreature_GainsLife()
    {
        var state = CreateState();
        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");
        creature.Power = 1;
        state.Player2.Battlefield.Add(creature);

        var swords = GameCard.Create("Swords to Plowshares");
        var spell = new StackObject(swords, state.Player1.Id,
            new Dictionary<ManaColor, int> { [ManaColor.White] = 1 },
            new List<TargetInfo> { new(creature.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);

        new SwordsToPlowsharesEffect().Resolve(state, spell);

        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == creature.Id);
        state.Player2.Exile.Cards.Should().Contain(c => c.Id == creature.Id);
        state.Player2.Life.Should().Be(21);
    }

    [Fact]
    public void SwordsToPlowshares_HighPowerCreature_GainsMoreLife()
    {
        var state = CreateState();
        var creature = new GameCard { Name = "Big Creature", Power = 5, Toughness = 5, CardTypes = CardType.Creature };
        state.Player2.Battlefield.Add(creature);

        var swords = GameCard.Create("Swords to Plowshares");
        var spell = new StackObject(swords, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(creature.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);

        new SwordsToPlowsharesEffect().Resolve(state, spell);

        state.Player2.Life.Should().Be(25);
    }

    [Fact]
    public void Naturalize_DestroysEnchantment()
    {
        var state = CreateState();
        var enchantment = GameCard.Create("Wild Growth", "Enchantment");
        state.Player2.Battlefield.Add(enchantment);

        var naturalize = GameCard.Create("Naturalize");
        var spell = new StackObject(naturalize, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(enchantment.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);

        new NaturalizeEffect().Resolve(state, spell);

        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Id == enchantment.Id);
        state.Player2.Graveyard.Cards.Should().Contain(c => c.Id == enchantment.Id);
    }

    [Fact]
    public void SwordsToPlowshares_TargetGone_NoEffect()
    {
        var state = CreateState();
        var creature = GameCard.Create("Mogg Fanatic", "Creature — Goblin");

        var swords = GameCard.Create("Swords to Plowshares");
        var spell = new StackObject(swords, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(creature.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);

        new SwordsToPlowsharesEffect().Resolve(state, spell);

        state.Player2.Life.Should().Be(20);
        state.Player2.Exile.Cards.Should().BeEmpty();
    }
}
