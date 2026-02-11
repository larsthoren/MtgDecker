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
}
