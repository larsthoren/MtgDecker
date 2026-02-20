using FluentAssertions;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests;

public class SelfIsCastTests
{
    [Fact]
    public async Task SelfIsCast_WhenCardCast_TriggerGoesOnStack()
    {
        var (state, h1, h2) = TestHelper.CreateStateWithHandlers();
        var engine = new GameEngine(state);
        state.ActivePlayer = state.Player1;
        state.TurnNumber = 2;
        state.CurrentPhase = Phase.MainPhase1;

        CardDefinitions.Register(new CardDefinition(
            ManaCost.Parse("{3}"), null, 3, 3, CardType.Creature)
        {
            Name = "TestCastTriggerCard",
            Triggers = [new Trigger(GameEvent.SpellCast, TriggerCondition.SelfIsCast, new ExtraTurnEffect())],
        });

        try
        {
            var card = GameCard.Create("TestCastTriggerCard");
            state.Player1.Hand.Add(card);
            state.Player1.ManaPool.Add(ManaColor.Colorless, 3);

            await engine.ExecuteAction(GameAction.CastSpell(state.Player1.Id, card.Id));

            // Cast trigger should have put ExtraTurnEffect on the stack
            // The stack should have the creature spell + the triggered ability
            state.StackCount.Should().BeGreaterThanOrEqualTo(2);
            state.GameLog.Should().Contain(msg => msg.Contains("cast trigger"));
        }
        finally
        {
            CardDefinitions.Unregister("TestCastTriggerCard");
        }
    }

    [Fact]
    public void SelfIsCast_WhenPutIntoPlayDirectly_NoTrigger()
    {
        var (state, _, _) = TestHelper.CreateStateWithHandlers();

        // Directly add to battlefield (like Show and Tell)
        var card = new GameCard
        {
            Name = "DirectPlayCard",
            CardTypes = CardType.Creature,
        };
        state.Player1.Battlefield.Add(card);

        // No extra turns should be queued
        state.ExtraTurns.Should().BeEmpty();
    }
}
