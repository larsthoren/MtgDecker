using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests.Triggers;

public class TriggerIntegrationTests
{
    private (GameEngine engine, GameState state, TestDecisionHandler p1Handler, TestDecisionHandler p2Handler) CreateSetup()
    {
        var p1Handler = new TestDecisionHandler();
        var p2Handler = new TestDecisionHandler();
        var state = new GameState(
            new Player(Guid.NewGuid(), "Player 1", p1Handler),
            new Player(Guid.NewGuid(), "Player 2", p2Handler));
        var engine = new GameEngine(state);
        return (engine, state, p1Handler, p2Handler);
    }

    [Fact]
    public async Task CastCreatureWithETB_TriggersEffect()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var commander = new GameCard
        {
            Name = "Siege-Gang Commander",
            CardTypes = CardType.Creature,
            ManaCost = MtgDecker.Engine.Mana.ManaCost.Parse("{3}{R}{R}"),
            Power = 2,
            Toughness = 2,
            Subtypes = ["Goblin"],
            Triggers = [
                new Trigger(GameEvent.EnterBattlefield, TriggerCondition.Self,
                    new CreateTokensEffect("Goblin", 1, 1, CardType.Creature, ["Goblin"], count: 3))
            ]
        };
        state.Player1.Hand.Add(commander);

        // Give enough mana
        state.Player1.ManaPool.Add(MtgDecker.Engine.Enums.ManaColor.Red, 5);

        // Cast it
        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, commander.Id));
        p1Handler.EnqueueAction(GameAction.Pass(state.Player1.Id));
        p2Handler.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync();

        // Commander + 3 tokens on battlefield
        state.Player1.Battlefield.Cards.Where(c => c.Name == "Siege-Gang Commander").Should().HaveCount(1);
        state.Player1.Battlefield.Cards.Where(c => c.Name == "Goblin" && c.IsToken).Should().HaveCount(3);
    }

    [Fact]
    public async Task CardWithNoTriggers_NoEffectFired()
    {
        var (engine, state, p1Handler, p2Handler) = CreateSetup();
        await engine.StartGameAsync();

        var bear = new GameCard
        {
            Name = "Grizzly Bears",
            CardTypes = CardType.Creature,
            ManaCost = MtgDecker.Engine.Mana.ManaCost.Parse("{1}{G}"),
            Power = 2,
            Toughness = 2,
        };
        state.Player1.Hand.Add(bear);
        state.Player1.ManaPool.Add(MtgDecker.Engine.Enums.ManaColor.Green, 2);

        p1Handler.EnqueueAction(GameAction.PlayCard(state.Player1.Id, bear.Id));
        p1Handler.EnqueueAction(GameAction.Pass(state.Player1.Id));
        p2Handler.EnqueueAction(GameAction.Pass(state.Player2.Id));

        await engine.RunPriorityAsync();

        state.Player1.Battlefield.Count.Should().Be(1); // Just the bear, no tokens
    }
}
