using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class UtilityEffectTests
{
    [Fact]
    public async Task AddAnyManaEffect_AddsChosenColor()
    {
        var (state, h1, _) = TestHelper.CreateStateWithHandlers();
        h1.EnqueueManaColor(ManaColor.Blue);

        var source = GameCard.Create("Lotus Petal");
        var ctx = new EffectContext(state, state.Player1, source, h1);

        await new AddAnyManaEffect().Execute(ctx);

        state.Player1.ManaPool[ManaColor.Blue].Should().Be(1);
    }

    [Fact]
    public void BounceTargetEffect_ReturnsToBoundsHand()
    {
        var (state, _, _) = TestHelper.CreateStateWithHandlers();

        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature, Power = 2, Toughness = 2 };
        state.Player2.Battlefield.Add(creature);

        var bounceSpell = GameCard.Create("Wipe Away");
        var spell = new StackObject(bounceSpell, state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(creature.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);

        new BounceTargetEffect().Resolve(state, spell);

        state.Player2.Battlefield.Cards.Should().NotContain(c => c.Name == "Bear");
        state.Player2.Hand.Cards.Should().Contain(c => c.Name == "Bear");
    }

    [Fact]
    public void BounceTargetEffect_UntapsBouncedCard()
    {
        var (state, _, _) = TestHelper.CreateStateWithHandlers();

        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature, IsTapped = true };
        state.Player2.Battlefield.Add(creature);

        var spell = new StackObject(GameCard.Create("Bounce"), state.Player1.Id,
            new Dictionary<ManaColor, int>(),
            new List<TargetInfo> { new(creature.Id, state.Player2.Id, ZoneType.Battlefield) }, 0);

        new BounceTargetEffect().Resolve(state, spell);

        var inHand = state.Player2.Hand.Cards.First(c => c.Name == "Bear");
        inHand.IsTapped.Should().BeFalse();
    }

    [Fact]
    public void NoncreatureSpell_MatchesNoncreatureOnStack()
    {
        var filter = TargetFilter.NoncreatureSpell();
        var sorcery = new GameCard { Name = "Ponder", CardTypes = CardType.Sorcery };
        filter.IsLegal(sorcery, ZoneType.Stack).Should().BeTrue();
    }

    [Fact]
    public void NoncreatureSpell_RejectsCreatureOnStack()
    {
        var filter = TargetFilter.NoncreatureSpell();
        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        filter.IsLegal(creature, ZoneType.Stack).Should().BeFalse();
    }

    [Fact]
    public void InstantOrSorcerySpell_MatchesInstantOnStack()
    {
        var filter = TargetFilter.InstantOrSorcerySpell();
        var instant = new GameCard { Name = "Bolt", CardTypes = CardType.Instant };
        filter.IsLegal(instant, ZoneType.Stack).Should().BeTrue();
    }

    [Fact]
    public void InstantOrSorcerySpell_RejectsEnchantmentOnStack()
    {
        var filter = TargetFilter.InstantOrSorcerySpell();
        var ench = new GameCard { Name = "Oath", CardTypes = CardType.Enchantment };
        filter.IsLegal(ench, ZoneType.Stack).Should().BeFalse();
    }
}
