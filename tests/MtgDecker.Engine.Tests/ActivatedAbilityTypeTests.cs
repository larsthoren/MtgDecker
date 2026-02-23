using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Triggers;

namespace MtgDecker.Engine.Tests;

public class ActivatedAbilityTypeTests
{
    // --- ActivatedAbilityCost defaults ---

    [Fact]
    public void ActivatedAbilityCost_Defaults_AreFalseAndNull()
    {
        var cost = new ActivatedAbilityCost();

        cost.TapSelf.Should().BeFalse();
        cost.SacrificeSelf.Should().BeFalse();
        cost.SacrificeSubtype.Should().BeNull();
        cost.ManaCost.Should().BeNull();
    }

    [Fact]
    public void ActivatedAbilityCost_WithTapSelf_SetsCorrectly()
    {
        var cost = new ActivatedAbilityCost(TapSelf: true);

        cost.TapSelf.Should().BeTrue();
        cost.SacrificeSelf.Should().BeFalse();
        cost.SacrificeSubtype.Should().BeNull();
        cost.ManaCost.Should().BeNull();
    }

    [Fact]
    public void ActivatedAbilityCost_WithSacrificeSubtype_SetsCorrectly()
    {
        var cost = new ActivatedAbilityCost(SacrificeSubtype: "Goblin");

        cost.TapSelf.Should().BeFalse();
        cost.SacrificeSelf.Should().BeFalse();
        cost.SacrificeSubtype.Should().Be("Goblin");
        cost.ManaCost.Should().BeNull();
    }

    [Fact]
    public void ActivatedAbilityCost_WithManaCost_SetsCorrectly()
    {
        var manaCost = ManaCost.Parse("{1}{B}");
        var cost = new ActivatedAbilityCost(ManaCost: manaCost);

        cost.TapSelf.Should().BeFalse();
        cost.SacrificeSelf.Should().BeFalse();
        cost.SacrificeSubtype.Should().BeNull();
        cost.ManaCost.Should().Be(manaCost);
    }

    [Fact]
    public void ActivatedAbilityCost_WithAllOptions_SetsCorrectly()
    {
        var manaCost = ManaCost.Parse("{2}{R}");
        var cost = new ActivatedAbilityCost(
            TapSelf: true,
            SacrificeSelf: true,
            SacrificeSubtype: "Artifact",
            ManaCost: manaCost);

        cost.TapSelf.Should().BeTrue();
        cost.SacrificeSelf.Should().BeTrue();
        cost.SacrificeSubtype.Should().Be("Artifact");
        cost.ManaCost.Should().Be(manaCost);
    }

    // --- ActivatedAbility record ---

    [Fact]
    public void ActivatedAbility_CreatedWithCostAndEffect()
    {
        var cost = new ActivatedAbilityCost(TapSelf: true);
        var effect = new DummyEffect();

        var ability = new ActivatedAbility(cost, effect);

        ability.Cost.Should().Be(cost);
        ability.Effect.Should().Be(effect);
        ability.TargetFilter.Should().BeNull();
        ability.CanTargetPlayer.Should().BeFalse();
    }

    [Fact]
    public void ActivatedAbility_WithTargetFilter_SetsCorrectly()
    {
        var cost = new ActivatedAbilityCost();
        var effect = new DummyEffect();
        Func<GameCard, bool> filter = c => c.IsCreature;

        var ability = new ActivatedAbility(cost, effect, TargetFilter: filter);

        ability.TargetFilter.Should().NotBeNull();
        ability.CanTargetPlayer.Should().BeFalse();
    }

    [Fact]
    public void ActivatedAbility_WithCanTargetPlayer_SetsCorrectly()
    {
        var cost = new ActivatedAbilityCost();
        var effect = new DummyEffect();

        var ability = new ActivatedAbility(cost, effect, CanTargetPlayer: true);

        ability.CanTargetPlayer.Should().BeTrue();
    }

    // --- ActionType.ActivateAbility ---

    [Fact]
    public void ActionType_ActivateAbility_Exists()
    {
        var value = ActionType.ActivateAbility;

        value.Should().BeDefined();
    }

    // --- GameAction.ActivateAbility factory ---

    [Fact]
    public void GameAction_ActivateAbility_SetsTypeAndIds()
    {
        var playerId = Guid.NewGuid();
        var cardId = Guid.NewGuid();

        var action = GameAction.ActivateAbility(playerId, cardId);

        action.Type.Should().Be(ActionType.ActivateAbility);
        action.PlayerId.Should().Be(playerId);
        action.CardId.Should().Be(cardId);
        action.TargetCardId.Should().BeNull();
        action.TargetPlayerId.Should().BeNull();
    }

    [Fact]
    public void GameAction_ActivateAbility_WithTarget_SetsTargetCardId()
    {
        var playerId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var action = GameAction.ActivateAbility(playerId, cardId, targetId: targetId);

        action.Type.Should().Be(ActionType.ActivateAbility);
        action.TargetCardId.Should().Be(targetId);
        action.TargetPlayerId.Should().BeNull();
    }

    [Fact]
    public void GameAction_ActivateAbility_WithTargetPlayer_SetsTargetPlayerId()
    {
        var playerId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var targetPlayerId = Guid.NewGuid();

        var action = GameAction.ActivateAbility(playerId, cardId, targetPlayerId: targetPlayerId);

        action.Type.Should().Be(ActionType.ActivateAbility);
        action.TargetCardId.Should().BeNull();
        action.TargetPlayerId.Should().Be(targetPlayerId);
    }

    // --- EffectContext with Target and TargetPlayerId ---

    [Fact]
    public void EffectContext_WithTarget_SetsCorrectly()
    {
        var handler = new MtgDecker.Engine.Tests.Helpers.TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);
        var state = new GameState(player, new Player(Guid.NewGuid(), "Opp", handler));
        var source = new GameCard { Name = "Source" };
        var target = new GameCard { Name = "Target" };

        var ctx = new EffectContext(state, player, source, handler) { Target = target };

        ctx.Target.Should().Be(target);
        ctx.TargetPlayerId.Should().BeNull();
    }

    [Fact]
    public void EffectContext_WithTargetPlayerId_SetsCorrectly()
    {
        var handler = new MtgDecker.Engine.Tests.Helpers.TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);
        var state = new GameState(player, new Player(Guid.NewGuid(), "Opp", handler));
        var source = new GameCard { Name = "Source" };
        var targetPlayerId = Guid.NewGuid();

        var ctx = new EffectContext(state, player, source, handler) { TargetPlayerId = targetPlayerId };

        ctx.Target.Should().BeNull();
        ctx.TargetPlayerId.Should().Be(targetPlayerId);
    }

    [Fact]
    public void EffectContext_WithoutTargets_DefaultsToNull()
    {
        var handler = new MtgDecker.Engine.Tests.Helpers.TestDecisionHandler();
        var player = new Player(Guid.NewGuid(), "Test", handler);
        var state = new GameState(player, new Player(Guid.NewGuid(), "Opp", handler));
        var source = new GameCard { Name = "Source" };

        var ctx = new EffectContext(state, player, source, handler);

        ctx.Target.Should().BeNull();
        ctx.TargetPlayerId.Should().BeNull();
    }

    // --- CardDefinition has ActivatedAbility ---

    [Fact]
    public void CardDefinition_HasActivatedAbilities_DefaultsToEmpty()
    {
        var def = new CardDefinition(null, null, null, null, CardType.Creature);

        def.ActivatedAbilities.Should().BeEmpty();
    }

    [Fact]
    public void CardDefinition_WithActivatedAbilities_SetsCorrectly()
    {
        var cost = new ActivatedAbilityCost(TapSelf: true);
        var effect = new DummyEffect();
        var ability = new ActivatedAbility(cost, effect);

        var def = new CardDefinition(null, null, null, null, CardType.Creature)
        {
            ActivatedAbilities = [ability]
        };

        def.ActivatedAbilities.Should().ContainSingle();
        def.ActivatedAbilities[0].Cost.TapSelf.Should().BeTrue();
    }

    // --- Dummy effect for testing ---

    private class DummyEffect : IEffect
    {
        public Task Execute(EffectContext context, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
