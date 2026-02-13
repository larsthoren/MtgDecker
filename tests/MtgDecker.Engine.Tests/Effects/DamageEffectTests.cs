using FluentAssertions;
using MtgDecker.Engine.Effects;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Mana;
using MtgDecker.Engine.Tests.Helpers;

namespace MtgDecker.Engine.Tests.Effects;

public class DamageEffectTests
{
    private static (GameState state, Player p1, Player p2) CreateGameState()
    {
        var h1 = new TestDecisionHandler();
        var h2 = new TestDecisionHandler();
        var p1 = new Player(Guid.NewGuid(), "Alice", h1);
        var p2 = new Player(Guid.NewGuid(), "Bob", h2);
        var state = new GameState(p1, p2);
        return (state, p1, p2);
    }

    private static StackObject CreateSpell(string name, Guid controllerId, List<TargetInfo> targets)
    {
        var card = GameCard.Create(name);
        return new StackObject(card, controllerId,
            new Dictionary<ManaColor, int>(), targets, 0);
    }

    [Fact]
    public void DamageEffect_DealsDamageToPlayer_ReducesLife()
    {
        // Arrange
        var (state, p1, p2) = CreateGameState();
        var effect = new DamageEffect(3);
        var playerTarget = new TargetInfo(Guid.Empty, p2.Id, ZoneType.None);
        var spell = CreateSpell("Lightning Bolt", p1.Id, new List<TargetInfo> { playerTarget });

        // Act
        effect.Resolve(state, spell);

        // Assert
        p2.Life.Should().Be(17); // 20 - 3
    }

    [Fact]
    public void DamageEffect_DealsDamageToCreature_MarksDamage()
    {
        // Arrange
        var (state, p1, p2) = CreateGameState();
        var effect = new DamageEffect(2);
        var creature = GameCard.Create("Grizzly Bears", "Creature - Bear");
        creature.Power = 2;
        creature.Toughness = 2;
        p2.Battlefield.Add(creature);
        var creatureTarget = new TargetInfo(creature.Id, p2.Id, ZoneType.Battlefield);
        var spell = CreateSpell("Shock", p1.Id, new List<TargetInfo> { creatureTarget });

        // Act
        effect.Resolve(state, spell);

        // Assert
        creature.DamageMarked.Should().Be(2);
        p2.Battlefield.Cards.Should().Contain(c => c.Id == creature.Id); // still on battlefield (SBA not run)
    }

    [Fact]
    public void DamageEffect_PlayerOnlyMode_DoesNotTargetCreatures()
    {
        // Arrange - verify the constructor flags are set correctly
        var playerOnlyEffect = new DamageEffect(3, canTargetCreature: false, canTargetPlayer: true);

        // Assert
        playerOnlyEffect.CanTargetCreature.Should().BeFalse();
        playerOnlyEffect.CanTargetPlayer.Should().BeTrue();
        playerOnlyEffect.Amount.Should().Be(3);
    }

    [Fact]
    public void DamageEffect_Fizzles_WhenCreatureTargetMissing()
    {
        // Arrange
        var (state, p1, p2) = CreateGameState();
        var effect = new DamageEffect(3);
        var creature = GameCard.Create("Grizzly Bears", "Creature - Bear");
        // Target references a creature that is NOT on the battlefield (already removed)
        var creatureTarget = new TargetInfo(creature.Id, p2.Id, ZoneType.Battlefield);
        var spell = CreateSpell("Lightning Bolt", p1.Id, new List<TargetInfo> { creatureTarget });

        // Act - should not throw, just silently fizzle
        var act = () => effect.Resolve(state, spell);

        // Assert
        act.Should().NotThrow();
        p2.Life.Should().Be(20); // no damage redirected to player
    }

    [Fact]
    public void DamageEffect_ReportsCorrectLogMessages()
    {
        // Arrange - test player damage log
        var (state, p1, p2) = CreateGameState();
        var effect = new DamageEffect(3);
        var playerTarget = new TargetInfo(Guid.Empty, p2.Id, ZoneType.None);
        var spell = CreateSpell("Lightning Bolt", p1.Id, new List<TargetInfo> { playerTarget });

        // Act
        effect.Resolve(state, spell);

        // Assert
        state.GameLog.Should().ContainSingle()
            .Which.Should().Contain("Lightning Bolt")
            .And.Contain("3 damage")
            .And.Contain("Bob")
            .And.Contain("17 life");
    }

    [Fact]
    public void DamageEffect_ReportsCorrectLogMessages_ForCreatureDamage()
    {
        // Arrange
        var (state, p1, p2) = CreateGameState();
        var effect = new DamageEffect(2);
        var creature = GameCard.Create("Grizzly Bears", "Creature - Bear");
        p2.Battlefield.Add(creature);
        var creatureTarget = new TargetInfo(creature.Id, p2.Id, ZoneType.Battlefield);
        var spell = CreateSpell("Shock", p1.Id, new List<TargetInfo> { creatureTarget });

        // Act
        effect.Resolve(state, spell);

        // Assert
        state.GameLog.Should().ContainSingle()
            .Which.Should().Contain("Shock")
            .And.Contain("2 damage")
            .And.Contain("Grizzly Bears")
            .And.Contain("2 total damage");
    }

    [Fact]
    public void DamageEffect_NoTargets_DoesNothing()
    {
        // Arrange
        var (state, p1, p2) = CreateGameState();
        var effect = new DamageEffect(3);
        var spell = CreateSpell("Lightning Bolt", p1.Id, new List<TargetInfo>());

        // Act
        effect.Resolve(state, spell);

        // Assert
        p1.Life.Should().Be(20);
        p2.Life.Should().Be(20);
        state.GameLog.Should().BeEmpty();
    }

    [Fact]
    public void DamageEffect_DefaultConstructor_CanTargetBothCreatureAndPlayer()
    {
        // Arrange & Act
        var effect = new DamageEffect(3);

        // Assert
        effect.Amount.Should().Be(3);
        effect.CanTargetCreature.Should().BeTrue();
        effect.CanTargetPlayer.Should().BeTrue();
    }

    [Fact]
    public void DamageEffect_CreatureOnlyMode_FlagsSetCorrectly()
    {
        // Arrange & Act
        var effect = new DamageEffect(4, canTargetCreature: true, canTargetPlayer: false);

        // Assert
        effect.Amount.Should().Be(4);
        effect.CanTargetCreature.Should().BeTrue();
        effect.CanTargetPlayer.Should().BeFalse();
    }

    [Fact]
    public void DamageEffect_DamageAccumulates_OnCreature()
    {
        // Arrange
        var (state, p1, p2) = CreateGameState();
        var creature = GameCard.Create("Hill Giant", "Creature - Giant");
        creature.Power = 3;
        creature.Toughness = 3;
        p2.Battlefield.Add(creature);
        var creatureTarget = new TargetInfo(creature.Id, p2.Id, ZoneType.Battlefield);

        var effect1 = new DamageEffect(1);
        var spell1 = CreateSpell("Shock1", p1.Id, new List<TargetInfo> { creatureTarget });

        var effect2 = new DamageEffect(2);
        var spell2 = CreateSpell("Shock2", p1.Id, new List<TargetInfo> { creatureTarget });

        // Act
        effect1.Resolve(state, spell1);
        effect2.Resolve(state, spell2);

        // Assert
        creature.DamageMarked.Should().Be(3); // 1 + 2
    }
}
