using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Phase7ThresholdTests
{
    private static (GameState state, Player p1, Player p2,
        TestDecisionHandler h1, TestDecisionHandler h2) Setup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", h1);
        var p2 = new Player(Guid.NewGuid(), "P2", h2);
        var state = new GameState(p1, p2);
        return (state, p1, p2, h1, h2);
    }

    private static void FillGraveyard(Player player, int count)
    {
        for (int i = 0; i < count; i++)
            player.Graveyard.Add(new GameCard { Name = $"GraveFiller{i}" });
    }

    // === Nimble Mongoose threshold ===

    [Fact]
    public void NimbleMongoose_WithThreshold_Gets2_2Bonus()
    {
        CardDefinitions.TryGet("Nimble Mongoose", out var def);

        var threshold = def!.ContinuousEffects.FirstOrDefault(e =>
            e.Type == ContinuousEffectType.ModifyPowerToughness);
        threshold.Should().NotBeNull("Nimble Mongoose should have a threshold P/T buff");
        threshold!.PowerMod.Should().Be(2);
        threshold.ToughnessMod.Should().Be(2);
    }

    [Fact]
    public void NimbleMongoose_Threshold_AppliesWhenGraveyardHas7()
    {
        CardDefinitions.TryGet("Nimble Mongoose", out var def);

        var threshold = def!.ContinuousEffects.First(e =>
            e.Type == ContinuousEffectType.ModifyPowerToughness);

        var (_, p1, _, _, _) = Setup();
        FillGraveyard(p1, 7);
        var mongoose = new GameCard { Name = "Nimble Mongoose" };

        threshold.Applies(mongoose, p1).Should().BeTrue("7 cards in graveyard = threshold met");
    }

    [Fact]
    public void NimbleMongoose_Threshold_DoesNotApplyWhen6InGraveyard()
    {
        CardDefinitions.TryGet("Nimble Mongoose", out var def);

        var threshold = def!.ContinuousEffects.First(e =>
            e.Type == ContinuousEffectType.ModifyPowerToughness);

        var (_, p1, _, _, _) = Setup();
        FillGraveyard(p1, 6);
        var mongoose = new GameCard { Name = "Nimble Mongoose" };

        threshold.Applies(mongoose, p1).Should().BeFalse("6 cards < 7 = threshold not met");
    }

    [Fact]
    public void NimbleMongoose_StillHasShroud()
    {
        CardDefinitions.TryGet("Nimble Mongoose", out var def);

        def!.ContinuousEffects.Should().Contain(e =>
            e.Type == ContinuousEffectType.GrantKeyword
            && e.GrantedKeyword == Keyword.Shroud);
    }

    // === Barbarian Ring threshold ===

    [Fact]
    public void BarbarianRing_HasThresholdActivatedAbility()
    {
        CardDefinitions.TryGet("Barbarian Ring", out var def);

        def!.ActivatedAbilities.Should().NotBeEmpty();
        def.ActivatedAbilities[0].Condition.Should().NotBeNull("requires threshold");
        def.ActivatedAbilities[0].Cost.SacrificeSelf.Should().BeTrue();
        def.ActivatedAbilities[0].Cost.TapSelf.Should().BeTrue();
        def.ActivatedAbilities[0].Effect.Should().BeOfType<DealDamageEffect>();
    }

    [Fact]
    public void BarbarianRing_ThresholdCondition_Met()
    {
        CardDefinitions.TryGet("Barbarian Ring", out var def);

        var (_, p1, _, _, _) = Setup();
        FillGraveyard(p1, 7);

        def!.ActivatedAbilities[0].Condition!(p1).Should().BeTrue();
    }

    [Fact]
    public void BarbarianRing_ThresholdCondition_NotMet()
    {
        CardDefinitions.TryGet("Barbarian Ring", out var def);

        var (_, p1, _, _, _) = Setup();
        FillGraveyard(p1, 6);

        def!.ActivatedAbilities[0].Condition!(p1).Should().BeFalse();
    }

    [Fact]
    public async Task BarbarianRing_CannotActivate_WithoutThreshold()
    {
        var (state, p1, _, h1, _) = Setup();

        var ring = GameCard.Create("Barbarian Ring", "Land", null);
        p1.Battlefield.Add(ring);
        p1.ManaPool.Add(ManaColor.Red);

        FillGraveyard(p1, 5); // below threshold

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;
        var engine = new GameEngine(state);

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, ring.Id));

        // Should not activate — condition not met
        state.Stack.Should().BeEmpty("threshold not met, ability should not activate");
        ring.IsTapped.Should().BeFalse("should not have tapped");
    }

    [Fact]
    public async Task BarbarianRing_CanActivate_WithThreshold()
    {
        var (state, p1, p2, h1, _) = Setup();

        var ring = GameCard.Create("Barbarian Ring", "Land", null);
        p1.Battlefield.Add(ring);
        p1.ManaPool.Add(ManaColor.Red);

        FillGraveyard(p1, 7); // threshold met

        state.ActivePlayer = p1;
        state.CurrentPhase = Phase.MainPhase1;
        state.TurnNumber = 2;
        var engine = new GameEngine(state);

        // Target player
        h1.EnqueueTarget(new TargetInfo(Guid.Empty, p2.Id, ZoneType.None) { PlayerId = p2.Id });

        await engine.ExecuteAction(GameAction.ActivateAbility(p1.Id, ring.Id, targetPlayerId: p2.Id));

        // Should activate — condition met
        state.Stack.Should().HaveCount(1, "ability should be on the stack");
    }

    // === Cabal Pit threshold ===

    [Fact]
    public void CabalPit_HasThresholdActivatedAbility()
    {
        CardDefinitions.TryGet("Cabal Pit", out var def);

        def!.ActivatedAbilities.Should().NotBeEmpty();
        def.ActivatedAbilities[0].Condition.Should().NotBeNull("requires threshold");
        def.ActivatedAbilities[0].Cost.SacrificeSelf.Should().BeTrue();
        def.ActivatedAbilities[0].Cost.TapSelf.Should().BeTrue();
        def.ActivatedAbilities[0].Effect.Should().BeOfType<WeakenTargetEffect>();
    }

    // === WeakenTargetEffect ===

    [Fact]
    public async Task WeakenTargetEffect_AppliesMinusPT_UntilEndOfTurn()
    {
        var (state, p1, _, h1, _) = Setup();

        var creature = new GameCard { Name = "Bear", CardTypes = CardType.Creature,
            BasePower = 3, BaseToughness = 3 };
        p1.Battlefield.Add(creature);

        var source = new GameCard { Name = "Cabal Pit" };
        var context = new EffectContext(state, p1, source, h1) { Target = creature };

        var effect = new WeakenTargetEffect(-2, -2);
        await effect.Execute(context);

        state.ActiveEffects.Should().ContainSingle(e =>
            e.Type == ContinuousEffectType.ModifyPowerToughness
            && e.PowerMod == -2
            && e.ToughnessMod == -2
            && e.UntilEndOfTurn == true);
    }

    [Fact]
    public async Task WeakenTargetEffect_NoTarget_DoesNothing()
    {
        var (state, p1, _, h1, _) = Setup();

        var source = new GameCard { Name = "Cabal Pit" };
        var context = new EffectContext(state, p1, source, h1); // no target

        var effect = new WeakenTargetEffect(-2, -2);
        await effect.Execute(context);

        state.ActiveEffects.Should().BeEmpty();
    }

    // === ActivatedAbility Condition engine enforcement ===

    [Fact]
    public void ActivatedAbility_Condition_IsOptionalParameter()
    {
        // Existing abilities without condition should still work
        var ability = new ActivatedAbility(
            new ActivatedAbilityCost(TapSelf: true),
            new AddManaEffect(ManaColor.Green));

        ability.Condition.Should().BeNull();
    }
}
