using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests.AI;

public class SpellRoleTests
{
    [Fact]
    public void CardDefinition_HasSpellRole_DefaultsToProactive()
    {
        CardDefinitions.TryGet("Goblin Lackey", out var def).Should().BeTrue();
        def!.SpellRole.Should().Be(SpellRole.Proactive);
    }

    [Theory]
    [InlineData("Counterspell", SpellRole.Counterspell)]
    [InlineData("Daze", SpellRole.Counterspell)]
    [InlineData("Force of Will", SpellRole.Counterspell)]
    [InlineData("Mana Leak", SpellRole.Counterspell)]
    [InlineData("Spell Pierce", SpellRole.Counterspell)]
    [InlineData("Flusterstorm", SpellRole.Counterspell)]
    [InlineData("Absorb", SpellRole.Counterspell)]
    [InlineData("Prohibit", SpellRole.Counterspell)]
    [InlineData("Pyroblast", SpellRole.Counterspell)]
    public void Counterspells_HaveCorrectRole(string cardName, SpellRole expected)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue();
        def!.SpellRole.Should().Be(expected);
    }

    [Theory]
    [InlineData("Lightning Bolt", SpellRole.InstantRemoval)]
    [InlineData("Swords to Plowshares", SpellRole.InstantRemoval)]
    [InlineData("Dismember", SpellRole.InstantRemoval)]
    [InlineData("Fatal Push", SpellRole.InstantRemoval)]
    [InlineData("Smother", SpellRole.InstantRemoval)]
    [InlineData("Snuff Out", SpellRole.InstantRemoval)]
    [InlineData("Incinerate", SpellRole.InstantRemoval)]
    [InlineData("Shock", SpellRole.InstantRemoval)]
    [InlineData("Searing Blood", SpellRole.InstantRemoval)]
    [InlineData("Naturalize", SpellRole.InstantRemoval)]
    [InlineData("Disenchant", SpellRole.InstantRemoval)]
    [InlineData("Diabolic Edict", SpellRole.InstantRemoval)]
    [InlineData("Wipe Away", SpellRole.InstantRemoval)]
    [InlineData("Ray of Revelation", SpellRole.InstantRemoval)]
    public void InstantRemoval_HasCorrectRole(string cardName, SpellRole expected)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue();
        def!.SpellRole.Should().Be(expected);
    }

    [Theory]
    [InlineData("Brainstorm", SpellRole.InstantUtility)]
    [InlineData("Fact or Fiction", SpellRole.InstantUtility)]
    [InlineData("Impulse", SpellRole.InstantUtility)]
    [InlineData("Dark Ritual", SpellRole.Ramp)]
    [InlineData("Skeletal Scrying", SpellRole.InstantUtility)]
    [InlineData("Funeral Charm", SpellRole.InstantUtility)]
    [InlineData("Funeral Pyre", SpellRole.InstantUtility)]
    [InlineData("Surgical Extraction", SpellRole.InstantUtility)]
    public void InstantUtility_HasCorrectRole(string cardName, SpellRole expected)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue();
        def!.SpellRole.Should().Be(expected);
    }

    [Theory]
    [InlineData("Goblin Lackey", SpellRole.Proactive)]
    [InlineData("Siege-Gang Commander", SpellRole.Proactive)]
    [InlineData("Replenish", SpellRole.Proactive)]
    public void ProactiveCards_HaveDefaultRole(string cardName, SpellRole expected)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue();
        def!.SpellRole.Should().Be(expected);
    }
}
