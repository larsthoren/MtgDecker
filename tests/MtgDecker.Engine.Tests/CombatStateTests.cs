using FluentAssertions;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class CombatStateTests
{
    [Fact]
    public void DeclareAttacker_AddsToAttackersList()
    {
        var state = new CombatState(Guid.NewGuid(), Guid.NewGuid());
        var attackerId = Guid.NewGuid();

        state.DeclareAttacker(attackerId);

        state.Attackers.Should().Contain(attackerId);
    }

    [Fact]
    public void DeclareBlocker_AssignsBlockerToAttacker()
    {
        var state = new CombatState(Guid.NewGuid(), Guid.NewGuid());
        var attackerId = Guid.NewGuid();
        var blockerId = Guid.NewGuid();
        state.DeclareAttacker(attackerId);

        state.DeclareBlocker(blockerId, attackerId);

        state.GetBlockers(attackerId).Should().Contain(blockerId);
    }

    [Fact]
    public void DeclareBlocker_MultipleBlockersOnOneAttacker()
    {
        var state = new CombatState(Guid.NewGuid(), Guid.NewGuid());
        var attackerId = Guid.NewGuid();
        var blocker1 = Guid.NewGuid();
        var blocker2 = Guid.NewGuid();
        state.DeclareAttacker(attackerId);

        state.DeclareBlocker(blocker1, attackerId);
        state.DeclareBlocker(blocker2, attackerId);

        state.GetBlockers(attackerId).Should().HaveCount(2);
        state.IsBlocked(attackerId).Should().BeTrue();
    }

    [Fact]
    public void IsBlocked_UnblockedAttacker_ReturnsFalse()
    {
        var state = new CombatState(Guid.NewGuid(), Guid.NewGuid());
        var attackerId = Guid.NewGuid();
        state.DeclareAttacker(attackerId);

        state.IsBlocked(attackerId).Should().BeFalse();
    }

    [Fact]
    public void SetBlockerOrder_SetsOrderForMultiBlock()
    {
        var state = new CombatState(Guid.NewGuid(), Guid.NewGuid());
        var attackerId = Guid.NewGuid();
        var blocker1 = Guid.NewGuid();
        var blocker2 = Guid.NewGuid();
        state.DeclareAttacker(attackerId);
        state.DeclareBlocker(blocker1, attackerId);
        state.DeclareBlocker(blocker2, attackerId);

        state.SetBlockerOrder(attackerId, new List<Guid> { blocker2, blocker1 });

        state.GetBlockerOrder(attackerId).Should().ContainInOrder(blocker2, blocker1);
    }

    [Fact]
    public void GetBlockerOrder_SingleBlocker_ReturnsThatBlocker()
    {
        var state = new CombatState(Guid.NewGuid(), Guid.NewGuid());
        var attackerId = Guid.NewGuid();
        var blockerId = Guid.NewGuid();
        state.DeclareAttacker(attackerId);
        state.DeclareBlocker(blockerId, attackerId);

        state.GetBlockerOrder(attackerId).Should().ContainSingle()
            .Which.Should().Be(blockerId);
    }
}
