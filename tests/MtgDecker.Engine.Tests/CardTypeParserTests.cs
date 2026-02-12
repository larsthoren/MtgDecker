using FluentAssertions;
using MtgDecker.Engine.Enums;

namespace MtgDecker.Engine.Tests;

public class CardTypeParserTests
{
    [Theory]
    [InlineData("Creature — Goblin", CardType.Creature)]
    [InlineData("Basic Land — Mountain", CardType.Land)]
    [InlineData("Enchantment Creature — Human", CardType.Creature | CardType.Enchantment)]
    [InlineData("Artifact Creature — Golem", CardType.Creature | CardType.Artifact)]
    [InlineData("Legendary Enchantment", CardType.Enchantment)]
    [InlineData("Instant", CardType.Instant)]
    [InlineData("Sorcery", CardType.Sorcery)]
    [InlineData("Artifact", CardType.Artifact)]
    [InlineData("Land", CardType.Land)]
    [InlineData("Legendary Creature — Dragon", CardType.Creature)]
    [InlineData("Legendary Artifact — Equipment", CardType.Artifact)]
    [InlineData("Enchantment — Aura", CardType.Enchantment)]
    [InlineData("", CardType.None)]
    public void Parse_TypeLine_ReturnsCorrectCardType(string typeLine, CardType expected)
    {
        CardTypeParser.Parse(typeLine).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(ParseFullTestData))]
    public void ParseFull_ReturnsTypesAndSubtypes(string typeLine, CardType expectedType, string[] expectedSubtypes)
    {
        var result = CardTypeParser.ParseFull(typeLine);

        result.Types.Should().Be(expectedType);
        result.Subtypes.Should().BeEquivalentTo(expectedSubtypes);
    }

    public static IEnumerable<object[]> ParseFullTestData()
    {
        yield return new object[] { "Creature — Goblin", CardType.Creature, new[] { "Goblin" } };
        yield return new object[] { "Legendary Creature — Goblin Warrior", CardType.Creature, new[] { "Goblin", "Warrior" } };
        yield return new object[] { "Enchantment — Aura", CardType.Enchantment, new[] { "Aura" } };
        yield return new object[] { "Basic Land — Mountain", CardType.Land, new[] { "Mountain" } };
        yield return new object[] { "Artifact Creature — Golem", CardType.Creature | CardType.Artifact, new[] { "Golem" } };
        yield return new object[] { "Creature", CardType.Creature, Array.Empty<string>() };
        yield return new object[] { "Instant", CardType.Instant, Array.Empty<string>() };
        yield return new object[] { "", CardType.None, Array.Empty<string>() };
    }
}
