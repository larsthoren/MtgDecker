using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Cards;
using MtgDecker.Application.Interfaces;

namespace MtgDecker.Application.Tests.Cards;

public class AutocompleteQueryTests
{
    private readonly ICardRepository _cardRepo = Substitute.For<ICardRepository>();

    [Fact]
    public async Task SearchSetNames_ReturnsMatchingSets()
    {
        var sets = new List<SetInfo>
        {
            new("zen", "Zendikar"),
            new("znr", "Zendikar Rising")
        };
        _cardRepo.GetDistinctSetsAsync("zend", Arg.Any<CancellationToken>()).Returns(sets);
        var handler = new SearchSetNamesHandler(_cardRepo);

        var result = await handler.Handle(new SearchSetNamesQuery("zend"), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].SetName.Should().Be("Zendikar");
    }

    [Fact]
    public async Task SearchSetNames_NoMatches_ReturnsEmpty()
    {
        _cardRepo.GetDistinctSetsAsync("xyz", Arg.Any<CancellationToken>()).Returns(new List<SetInfo>());
        var handler = new SearchSetNamesHandler(_cardRepo);

        var result = await handler.Handle(new SearchSetNamesQuery("xyz"), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchTypeNames_ReturnsMatchingTypes()
    {
        var types = new List<string> { "Creature", "Creative" };
        _cardRepo.GetDistinctTypesAsync("Crea", Arg.Any<CancellationToken>()).Returns(types);
        var handler = new SearchTypeNamesHandler(_cardRepo);

        var result = await handler.Handle(new SearchTypeNamesQuery("Crea"), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().Contain("Creature");
    }

    [Fact]
    public async Task SearchTypeNames_NoMatches_ReturnsEmpty()
    {
        _cardRepo.GetDistinctTypesAsync("xyz", Arg.Any<CancellationToken>()).Returns(new List<string>());
        var handler = new SearchTypeNamesHandler(_cardRepo);

        var result = await handler.Handle(new SearchTypeNamesQuery("xyz"), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
