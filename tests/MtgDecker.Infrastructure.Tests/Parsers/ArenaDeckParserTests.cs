using FluentAssertions;
using MtgDecker.Infrastructure.Parsers;

namespace MtgDecker.Infrastructure.Tests.Parsers;

public class ArenaDeckParserTests
{
    private readonly ArenaDeckParser _parser = new();

    [Fact]
    public void FormatName_ReturnsArena()
    {
        _parser.FormatName.Should().Be("Arena");
    }

    [Fact]
    public void Parse_MainDeckWithSetInfo_ParsesAllFields()
    {
        var text = """
            4 Lightning Bolt (LEA) 161
            2 Counterspell (LEA) 54
            """;

        var result = _parser.Parse(text);

        result.MainDeck.Should().HaveCount(2);
        result.MainDeck[0].Quantity.Should().Be(4);
        result.MainDeck[0].CardName.Should().Be("Lightning Bolt");
        result.MainDeck[0].SetCode.Should().Be("lea");
        result.MainDeck[0].CollectorNumber.Should().Be("161");
    }

    [Fact]
    public void Parse_MainDeckWithoutSetInfo_ParsesNameAndQuantity()
    {
        var text = "4 Lightning Bolt";

        var result = _parser.Parse(text);

        result.MainDeck.Should().HaveCount(1);
        result.MainDeck[0].CardName.Should().Be("Lightning Bolt");
        result.MainDeck[0].SetCode.Should().BeNull();
        result.MainDeck[0].CollectorNumber.Should().BeNull();
    }

    [Fact]
    public void Parse_SideboardSection_ParsesAfterSideboardHeader()
    {
        var text = """
            Deck
            4 Lightning Bolt (LEA) 161

            Sideboard
            2 Pyroblast (ICE) 212
            """;

        var result = _parser.Parse(text);

        result.MainDeck.Should().HaveCount(1);
        result.MainDeck[0].CardName.Should().Be("Lightning Bolt");
        result.Sideboard.Should().HaveCount(1);
        result.Sideboard[0].CardName.Should().Be("Pyroblast");
        result.Sideboard[0].SetCode.Should().Be("ice");
    }

    [Fact]
    public void Parse_CompanionSection_TreatedAsSideboard()
    {
        var text = """
            Companion
            1 Lurrus of the Dream-Den (IKO) 226

            Deck
            4 Lightning Bolt (LEA) 161
            """;

        var result = _parser.Parse(text);

        // Companion parsed before Deck, so Lurrus goes to sideboard
        // Then "Deck" resets to main deck
        result.Sideboard.Should().HaveCount(1);
        result.Sideboard[0].CardName.Should().Be("Lurrus of the Dream-Den");
        result.MainDeck.Should().HaveCount(1);
        result.MainDeck[0].CardName.Should().Be("Lightning Bolt");
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmptyDeck()
    {
        var result = _parser.Parse("");

        result.MainDeck.Should().BeEmpty();
        result.Sideboard.Should().BeEmpty();
    }
}
