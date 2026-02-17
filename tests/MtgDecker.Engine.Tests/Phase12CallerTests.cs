using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class Phase12CallerTests
{
    [Fact]
    public void CallerOfTheClaw_HasFlashAndETBTrigger()
    {
        CardDefinitions.TryGet("Caller of the Claw", out var def).Should().BeTrue();
        def!.Triggers.Should().ContainSingle();
        def.Triggers[0].Event.Should().Be(GameEvent.EnterBattlefield);
        def.Triggers[0].Condition.Should().Be(TriggerCondition.Self);
        def.Triggers[0].Effect.Should().BeOfType<CallerOfTheClawEffect>();

        // Flash keyword via ContinuousEffect
        def.ContinuousEffects.Should().ContainSingle();
        def.ContinuousEffects[0].GrantedKeyword.Should().Be(Keyword.Flash);
    }

    [Fact]
    public async Task CallerEffect_CreaturesDiedThisTurn_CreatesBearTokens()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        p1.CreaturesDiedThisTurn = 3;

        var caller = GameCard.Create("Caller of the Claw");
        p1.Battlefield.Add(caller);

        var effect = new CallerOfTheClawEffect();
        var context = new EffectContext(state, p1, caller, handler);
        await effect.Execute(context);

        var tokens = p1.Battlefield.Cards.Where(c => c.IsToken && c.Name == "Bear").ToList();
        tokens.Should().HaveCount(3);
        tokens.Should().OnlyContain(c => c.BasePower == 2 && c.BaseToughness == 2);
    }

    [Fact]
    public async Task CallerEffect_NoCreaturesDied_NoTokens()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        p1.CreaturesDiedThisTurn = 0;

        var caller = GameCard.Create("Caller of the Claw");
        p1.Battlefield.Add(caller);

        var effect = new CallerOfTheClawEffect();
        var context = new EffectContext(state, p1, caller, handler);
        await effect.Execute(context);

        p1.Battlefield.Cards.Where(c => c.IsToken).Should().BeEmpty();
    }

    [Fact]
    public void Player_CreaturesDiedThisTurn_DefaultsToZero()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        p1.CreaturesDiedThisTurn.Should().Be(0);
    }

    [Fact]
    public async Task CallerEffect_OneCreatureDied_OneToken()
    {
        var handler = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "P1", handler);
        var p2 = new Player(Guid.NewGuid(), "P2", new TestDecisionHandler());
        var state = new GameState(p1, p2);

        p1.CreaturesDiedThisTurn = 1;

        var caller = GameCard.Create("Caller of the Claw");
        p1.Battlefield.Add(caller);

        var effect = new CallerOfTheClawEffect();
        var context = new EffectContext(state, p1, caller, handler);
        await effect.Execute(context);

        var tokens = p1.Battlefield.Cards.Where(c => c.IsToken && c.Name == "Bear").ToList();
        tokens.Should().HaveCount(1);
    }
}
