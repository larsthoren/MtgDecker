using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Phase3MotherOfRunesTests
{
    [Fact]
    public void MotherOfRunes_HasTapAbility()
    {
        CardDefinitions.TryGet("Mother of Runes", out var def);

        def!.ActivatedAbility.Should().NotBeNull();
        def.ActivatedAbility!.Cost.TapSelf.Should().BeTrue();
        def.ActivatedAbility.Effect.Should().BeOfType<GrantProtectionEffect>();
        def.ActivatedAbility.TargetFilter.Should().NotBeNull("targets a creature");
    }

    [Fact]
    public void MotherOfRunes_TargetFilter_OnlyCreatures()
    {
        CardDefinitions.TryGet("Mother of Runes", out var def);
        var filter = def!.ActivatedAbility!.TargetFilter!;

        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        var land = new GameCard { Name = "Plains", CardTypes = CardType.Land };

        filter(creature).Should().BeTrue();
        filter(land).Should().BeFalse();
    }

    [Fact]
    public async Task GrantProtection_GrantsProtectionKeyword_UntilEOT()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        p1.Battlefield.Add(creature);

        h1.EnqueueManaColor(ManaColor.Red);

        var mother = new GameCard { Name = "Mother of Runes" };
        var context = new EffectContext(state, p1, mother, h1) { Target = creature };

        var effect = new GrantProtectionEffect();
        await effect.Execute(context);

        state.ActiveEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantKeyword
            && e.GrantedKeyword == Keyword.Protection
            && e.UntilEndOfTurn == true);
    }

    [Fact]
    public async Task GrantProtection_StoresChosenColor()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature };
        p1.Battlefield.Add(creature);

        h1.EnqueueManaColor(ManaColor.Black);

        var mother = new GameCard { Name = "Mother of Runes" };
        var context = new EffectContext(state, p1, mother, h1) { Target = creature };

        var effect = new GrantProtectionEffect();
        await effect.Execute(context);

        state.ActiveEffects.Should().Contain(e =>
            e.GrantedKeyword == Keyword.Protection
            && e.ProtectionColor == ManaColor.Black);
    }

    [Fact]
    public async Task GrantProtection_NoTarget_DoesNothing()
    {
        var h1 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        var mother = new GameCard { Name = "Mother of Runes" };
        var context = new EffectContext(state, p1, mother, h1); // no Target

        var effect = new GrantProtectionEffect();
        await effect.Execute(context);

        state.ActiveEffects.Should().BeEmpty();
    }
}
