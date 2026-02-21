using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests;

public class FindAndChooseTargetsTests
{
    private static (GameState state, GameEngine engine, Player caster, Player opponent,
        TestDecisionHandler casterHandler, TestDecisionHandler opponentHandler) CreateSetup()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Caster", h1);
        var p2 = new Player(Guid.NewGuid(), "Opponent", h2);
        var state = new GameState(p1, p2);
        var engine = new GameEngine(state);
        return (state, engine, p1, p2, h1, h2);
    }

    [Fact]
    public async Task FindsTargetsOnBothBattlefields()
    {
        var (state, engine, caster, opponent, casterHandler, _) = CreateSetup();

        var casterCreature = new GameCard
        {
            Name = "Grizzly Bears",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2
        };
        var opponentCreature = new GameCard
        {
            Name = "Hill Giant",
            CardTypes = CardType.Creature,
            BasePower = 3,
            BaseToughness = 3
        };

        caster.Battlefield.Add(casterCreature);
        opponent.Battlefield.Add(opponentCreature);

        casterHandler.EnqueueTarget(new TargetInfo(opponentCreature.Id, opponent.Id, ZoneType.Battlefield));

        var result = await engine.FindAndChooseTargetsAsync(
            TargetFilter.Creature(), caster, casterHandler, "Test Spell");

        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].CardId.Should().Be(opponentCreature.Id);
    }

    [Fact]
    public async Task ExcludesShroudTargets()
    {
        var (state, engine, caster, opponent, casterHandler, _) = CreateSetup();

        var shroudedCreature = new GameCard
        {
            Name = "Troll Ascetic",
            CardTypes = CardType.Creature,
            BasePower = 3,
            BaseToughness = 2
        };
        shroudedCreature.ActiveKeywords.Add(Keyword.Shroud);

        var normalCreature = new GameCard
        {
            Name = "Goblin Piker",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 1
        };

        opponent.Battlefield.Add(shroudedCreature);
        opponent.Battlefield.Add(normalCreature);

        casterHandler.EnqueueTarget(new TargetInfo(normalCreature.Id, opponent.Id, ZoneType.Battlefield));

        var result = await engine.FindAndChooseTargetsAsync(
            TargetFilter.Creature(), caster, casterHandler, "Lightning Bolt");

        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].CardId.Should().Be(normalCreature.Id);
    }

    [Fact]
    public async Task AddsPlayerSentinelsWhenFilterAllows()
    {
        var (state, engine, caster, opponent, casterHandler, _) = CreateSetup();

        casterHandler.EnqueueTarget(new TargetInfo(Guid.Empty, opponent.Id, ZoneType.None));

        var result = await engine.FindAndChooseTargetsAsync(
            TargetFilter.CreatureOrPlayer(), caster, casterHandler, "Lava Spike");

        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].CardId.Should().Be(Guid.Empty);
        result[0].PlayerId.Should().Be(opponent.Id);
        result[0].Zone.Should().Be(ZoneType.None);
    }

    [Fact]
    public async Task AddsStackTargetsWhenFilterAllows()
    {
        var (state, engine, caster, opponent, casterHandler, _) = CreateSetup();

        var spellCard = new GameCard
        {
            Name = "Lightning Bolt",
            CardTypes = CardType.Instant
        };
        var stackObj = new StackObject(
            spellCard, opponent.Id, new Dictionary<ManaColor, int>(),
            new List<TargetInfo>(), timestamp: 1);
        state.StackPush(stackObj);

        casterHandler.EnqueueTarget(new TargetInfo(spellCard.Id, opponent.Id, ZoneType.Stack));

        var result = await engine.FindAndChooseTargetsAsync(
            TargetFilter.Spell(), caster, casterHandler, "Counterspell");

        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].CardId.Should().Be(spellCard.Id);
        result[0].Zone.Should().Be(ZoneType.Stack);
    }

    [Fact]
    public async Task CancellationReturnsNull()
    {
        var (state, engine, caster, opponent, casterHandler, _) = CreateSetup();

        var creature = new GameCard
        {
            Name = "Grizzly Bears",
            CardTypes = CardType.Creature,
            BasePower = 2,
            BaseToughness = 2
        };
        caster.Battlefield.Add(creature);

        casterHandler.EnqueueTarget(null);

        var result = await engine.FindAndChooseTargetsAsync(
            TargetFilter.Creature(), caster, casterHandler, "Swords to Plowshares");

        result.Should().BeNull();
    }

    [Fact]
    public async Task NoLegalTargetsReturnsEmptyList()
    {
        var (state, engine, caster, opponent, casterHandler, _) = CreateSetup();

        // Empty battlefields, no stack — no legal creature targets exist

        var result = await engine.FindAndChooseTargetsAsync(
            TargetFilter.Creature(), caster, casterHandler, "Terror");

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}
