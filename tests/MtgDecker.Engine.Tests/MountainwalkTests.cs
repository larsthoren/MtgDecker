using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class MountainwalkTests
{
    [Fact]
    public async Task Creature_With_Mountainwalk_Cannot_Be_Blocked_When_Defender_Has_Mountain()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Attacker", handler1);
        var p2 = new Player(Guid.NewGuid(), "Defender", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var goblin = new GameCard { Name = "Walker", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        goblin.ActiveKeywords.Add(Keyword.Mountainwalk);
        goblin.TurnEnteredBattlefield = 0;
        p1.Battlefield.Add(goblin);

        var mountain = GameCard.Create("Mountain");
        p2.Battlefield.Add(mountain);

        var blocker = new GameCard { Name = "Wall", CardTypes = CardType.Creature, BasePower = 0, BaseToughness = 4 };
        p2.Battlefield.Add(blocker);

        state.TurnNumber = 2;

        handler1.EnqueueAttackers([goblin.Id]);
        handler2.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, goblin.Id } });

        await engine.RunCombatAsync(default);

        // Mountainwalk = unblockable, damage goes through
        p2.Life.Should().BeLessThan(20);
    }

    [Fact]
    public async Task Creature_With_Mountainwalk_Can_Be_Blocked_When_Defender_Has_No_Mountain()
    {
        var handler1 = new TestDecisionHandler();
        var handler2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Attacker", handler1);
        var p2 = new Player(Guid.NewGuid(), "Defender", handler2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);

        var goblin = new GameCard { Name = "Walker", CardTypes = CardType.Creature, BasePower = 2, BaseToughness = 2 };
        goblin.ActiveKeywords.Add(Keyword.Mountainwalk);
        goblin.TurnEnteredBattlefield = 0;
        p1.Battlefield.Add(goblin);

        p2.Battlefield.Add(GameCard.Create("Plains"));

        var blocker = new GameCard { Name = "Wall", CardTypes = CardType.Creature, BasePower = 0, BaseToughness = 4 };
        p2.Battlefield.Add(blocker);

        state.TurnNumber = 2;
        handler1.EnqueueAttackers([goblin.Id]);
        handler2.EnqueueBlockers(new Dictionary<Guid, Guid> { { blocker.Id, goblin.Id } });

        await engine.RunCombatAsync(default);

        // No mountain = can be blocked, no damage through
        p2.Life.Should().Be(20);
    }
}
