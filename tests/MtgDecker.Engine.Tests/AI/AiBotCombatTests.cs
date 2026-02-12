using FluentAssertions;
using MtgDecker.Engine.AI;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests.AI;

public class AiBotCombatTests
{
    private readonly AiBotDecisionHandler _bot = new();

    private static GameCard MakeCreature(string name, int power, int toughness) => new()
    {
        Name = name, Power = power, Toughness = toughness, CardTypes = CardType.Creature,
    };

    [Fact]
    public async Task ChooseAttackers_AttacksWithAll()
    {
        var attackers = new List<GameCard>
        {
            MakeCreature("Bear", 2, 2),
            MakeCreature("Goblin", 1, 1),
        };
        var result = await _bot.ChooseAttackers(attackers);
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ChooseAttackers_EmptyList_ReturnsEmpty()
    {
        var result = await _bot.ChooseAttackers([]);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ChooseAttackers_ReturnsCorrectIds()
    {
        var bear = MakeCreature("Bear", 2, 2);
        var result = await _bot.ChooseAttackers([bear]);
        result.Should().ContainSingle().Which.Should().Be(bear.Id);
    }

    [Fact]
    public async Task ChooseBlockers_NoAttackers_ReturnsEmpty()
    {
        var blockers = new List<GameCard> { MakeCreature("Bear", 2, 2) };
        var result = await _bot.ChooseBlockers(blockers, []);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ChooseBlockers_FavorableTrade_Blocks()
    {
        var myBlocker = MakeCreature("Bear", 2, 2);
        var theirAttacker = MakeCreature("Big Creature", 3, 2);
        var result = await _bot.ChooseBlockers([myBlocker], [theirAttacker]);
        result.Should().ContainKey(myBlocker.Id);
        result[myBlocker.Id].Should().Be(theirAttacker.Id);
    }

    [Fact]
    public async Task ChooseBlockers_ExactTrade_Blocks()
    {
        var myBlocker = MakeCreature("Bear", 2, 2);
        var theirAttacker = MakeCreature("Other Bear", 2, 2);
        var result = await _bot.ChooseBlockers([myBlocker], [theirAttacker]);
        result.Should().ContainKey(myBlocker.Id);
    }

    [Fact]
    public async Task ChooseBlockers_UnfavorableTrade_DoesNotBlock()
    {
        var myBlocker = MakeCreature("Goblin", 1, 1);
        var theirAttacker = MakeCreature("Wall", 1, 3);
        var result = await _bot.ChooseBlockers([myBlocker], [theirAttacker]);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ChooseBlockers_UsesSmallestSufficientBlocker()
    {
        var smallBlocker = MakeCreature("Bear", 2, 2);
        var bigBlocker = MakeCreature("Dragon", 5, 5);
        var theirAttacker = MakeCreature("Attacker", 3, 2);
        var result = await _bot.ChooseBlockers([smallBlocker, bigBlocker], [theirAttacker]);
        result.Should().ContainKey(smallBlocker.Id);
        result.Should().NotContainKey(bigBlocker.Id);
    }

    [Fact]
    public async Task ChooseBlockers_PrioritizesBlockingBiggestAttackerFirst()
    {
        var bear = MakeCreature("Bear", 3, 3);
        var smallAttacker = MakeCreature("Goblin", 1, 1);
        var bigAttacker = MakeCreature("Dragon", 5, 3);
        var result = await _bot.ChooseBlockers([bear], [smallAttacker, bigAttacker]);
        // Bear should block the Dragon (biggest threat), not the Goblin
        result.Should().ContainKey(bear.Id);
        result[bear.Id].Should().Be(bigAttacker.Id);
    }

    [Fact]
    public async Task ChooseBlockers_EachBlockerUsedOnce()
    {
        var bear1 = MakeCreature("Bear 1", 2, 2);
        var bear2 = MakeCreature("Bear 2", 2, 2);
        var attacker1 = MakeCreature("Attacker 1", 3, 2);
        var attacker2 = MakeCreature("Attacker 2", 3, 2);
        var result = await _bot.ChooseBlockers([bear1, bear2], [attacker1, attacker2]);
        result.Should().HaveCount(2);
        result.Values.Distinct().Should().HaveCount(2, "each blocker should block a different attacker");
    }

    [Fact]
    public async Task ChooseBlockers_NoBlockers_ReturnsEmpty()
    {
        var theirAttacker = MakeCreature("Attacker", 3, 3);
        var result = await _bot.ChooseBlockers([], [theirAttacker]);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task OrderBlockers_OrdersByToughnessAscending()
    {
        var small = MakeCreature("Goblin", 1, 1);
        var big = MakeCreature("Bear", 2, 2);
        var result = await _bot.OrderBlockers(Guid.NewGuid(), [big, small]);
        result[0].Should().Be(small.Id);
        result[1].Should().Be(big.Id);
    }

    [Fact]
    public async Task OrderBlockers_SameToughness_PreservesOrder()
    {
        var a = MakeCreature("Bear A", 2, 2);
        var b = MakeCreature("Bear B", 2, 2);
        var result = await _bot.OrderBlockers(Guid.NewGuid(), [a, b]);
        result.Should().HaveCount(2);
        // Both have same toughness, stable sort preserves input order
        result[0].Should().Be(a.Id);
        result[1].Should().Be(b.Id);
    }

    [Fact]
    public async Task OrderBlockers_SingleBlocker_ReturnsSingleId()
    {
        var bear = MakeCreature("Bear", 2, 2);
        var result = await _bot.OrderBlockers(Guid.NewGuid(), [bear]);
        result.Should().ContainSingle().Which.Should().Be(bear.Id);
    }

    [Fact]
    public async Task OrderBlockers_ThreeBlockers_OrdersByToughness()
    {
        var tiny = MakeCreature("Goblin", 1, 1);
        var medium = MakeCreature("Bear", 2, 3);
        var large = MakeCreature("Dragon", 5, 5);
        var result = await _bot.OrderBlockers(Guid.NewGuid(), [large, tiny, medium]);
        result[0].Should().Be(tiny.Id);
        result[1].Should().Be(medium.Id);
        result[2].Should().Be(large.Id);
    }
}
