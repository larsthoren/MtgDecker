using FluentAssertions;
using MtgDecker.Infrastructure.Parsers;

namespace MtgDecker.Infrastructure.Tests.Parsers;

public class MtgoDeckParserTests
{
    private readonly MtgoDeckParser _parser = new();

    [Fact]
    public void FormatName_ReturnsMtgo()
    {
        _parser.FormatName.Should().Be("MTGO");
    }

    [Fact]
    public void Parse_MainDeckCards_ParsesCorrectly()
    {
        var text = """
            4 Lightning Bolt
            2 Counterspell
            """;

        var result = _parser.Parse(text);

        result.MainDeck.Should().HaveCount(2);
        result.MainDeck[0].Quantity.Should().Be(4);
        result.MainDeck[0].CardName.Should().Be("Lightning Bolt");
        result.MainDeck[1].Quantity.Should().Be(2);
        result.MainDeck[1].CardName.Should().Be("Counterspell");
    }

    [Fact]
    public void Parse_SideboardCards_ParsesWithSbPrefix()
    {
        var text = """
            4 Lightning Bolt
            SB: 2 Pyroblast
            SB: 1 Blue Elemental Blast
            """;

        var result = _parser.Parse(text);

        result.MainDeck.Should().HaveCount(1);
        result.Sideboard.Should().HaveCount(2);
        result.Sideboard[0].Quantity.Should().Be(2);
        result.Sideboard[0].CardName.Should().Be("Pyroblast");
        result.Sideboard[1].CardName.Should().Be("Blue Elemental Blast");
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmptyDeck()
    {
        var result = _parser.Parse("");

        result.MainDeck.Should().BeEmpty();
        result.Sideboard.Should().BeEmpty();
    }

    [Fact]
    public void Parse_BlankLines_AreSkipped()
    {
        var text = """
            4 Lightning Bolt

            2 Counterspell
            """;

        var result = _parser.Parse(text);

        result.MainDeck.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_DoesNotSetSetCodeOrCollectorNumber()
    {
        var text = "4 Lightning Bolt";

        var result = _parser.Parse(text);

        result.MainDeck[0].SetCode.Should().BeNull();
        result.MainDeck[0].CollectorNumber.Should().BeNull();
    }
}
