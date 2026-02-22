using FluentAssertions;
using MtgDecker.Engine;
using MtgDecker.Engine.Enums;
using MtgDecker.Engine.Triggers;
using MtgDecker.Engine.Triggers.Effects;

namespace MtgDecker.Engine.Tests.Triggers;

public class CardDefinitionTriggerTests
{
    [Fact]
    public void SiegeGangCommander_HasCreateTokensTrigger()
    {
        CardDefinitions.TryGet("Siege-Gang Commander", out var def).Should().BeTrue();
        def!.Triggers.Should().HaveCount(1);
        def.Triggers[0].Event.Should().Be(GameEvent.EnterBattlefield);
        def.Triggers[0].Effect.Should().BeOfType<CreateTokensEffect>();
        def.Subtypes.Should().Contain("Goblin");
    }

    [Fact]
    public void GoblinMatron_HasSearchLibraryTrigger()
    {
        CardDefinitions.TryGet("Goblin Matron", out var def).Should().BeTrue();
        def!.Triggers.Should().HaveCount(1);
        def.Triggers[0].Event.Should().Be(GameEvent.EnterBattlefield);
        def.Triggers[0].Effect.Should().BeOfType<SearchLibraryEffect>();
        def.Subtypes.Should().Contain("Goblin");
    }

    [Fact]
    public void GoblinRingleader_HasRevealAndFilterTrigger()
    {
        CardDefinitions.TryGet("Goblin Ringleader", out var def).Should().BeTrue();
        def!.Triggers.Should().HaveCount(1);
        def.Triggers[0].Event.Should().Be(GameEvent.EnterBattlefield);
        def.Triggers[0].Effect.Should().BeOfType<RevealAndFilterEffect>();
        def.Subtypes.Should().Contain("Goblin");
    }

    [Theory]
    [InlineData("Goblin Lackey", new[] { "Goblin" })]
    [InlineData("Goblin Piledriver", new[] { "Goblin", "Warrior" })]
    [InlineData("Goblin Warchief", new[] { "Goblin", "Warrior" })]
    [InlineData("Mogg Fanatic", new[] { "Goblin" })]
    [InlineData("Gempalm Incinerator", new[] { "Goblin" })]
    [InlineData("Goblin King", new[] { "Goblin" })]
    [InlineData("Goblin Pyromancer", new[] { "Goblin", "Wizard" })]
    [InlineData("Goblin Sharpshooter", new[] { "Goblin" })]
    [InlineData("Goblin Tinkerer", new[] { "Goblin" })]
    [InlineData("Skirk Prospector", new[] { "Goblin" })]
    [InlineData("Argothian Enchantress", new[] { "Human", "Druid" })]
    public void StarterDeckCreatures_HaveCorrectSubtypes(string cardName, string[] expectedSubtypes)
    {
        CardDefinitions.TryGet(cardName, out var def).Should().BeTrue();
        def!.Subtypes.Should().BeEquivalentTo(expectedSubtypes);
    }
}
