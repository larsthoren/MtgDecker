using FluentAssertions;
using FluentValidation;
using MtgDecker.Application.Decks;
using MtgDecker.Application.Collection;
using MtgDecker.Application.DeckExport;
using MtgDecker.Application.Stats;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.Validators;

public class CommandValidatorTests
{
    [Fact]
    public void RemoveCardFromDeck_EmptyDeckId_Fails()
    {
        var validator = new RemoveCardFromDeckValidator();
        var cmd = new RemoveCardFromDeckCommand(Guid.Empty, Guid.NewGuid(), DeckCategory.MainDeck);

        validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RemoveCardFromDeck_EmptyCardId_Fails()
    {
        var validator = new RemoveCardFromDeckValidator();
        var cmd = new RemoveCardFromDeckCommand(Guid.NewGuid(), Guid.Empty, DeckCategory.MainDeck);

        validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RemoveCardFromDeck_Valid_Passes()
    {
        var validator = new RemoveCardFromDeckValidator();
        var cmd = new RemoveCardFromDeckCommand(Guid.NewGuid(), Guid.NewGuid(), DeckCategory.MainDeck);

        validator.Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateCardQuantity_ZeroQuantity_Fails()
    {
        var validator = new UpdateCardQuantityValidator();
        var cmd = new UpdateCardQuantityCommand(Guid.NewGuid(), Guid.NewGuid(), DeckCategory.MainDeck, 0);

        validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateCardQuantity_EmptyDeckId_Fails()
    {
        var validator = new UpdateCardQuantityValidator();
        var cmd = new UpdateCardQuantityCommand(Guid.Empty, Guid.NewGuid(), DeckCategory.MainDeck, 1);

        validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateCardQuantity_Valid_Passes()
    {
        var validator = new UpdateCardQuantityValidator();
        var cmd = new UpdateCardQuantityCommand(Guid.NewGuid(), Guid.NewGuid(), DeckCategory.MainDeck, 4);

        validator.Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void MoveCardCategory_EmptyDeckId_Fails()
    {
        var validator = new MoveCardCategoryValidator();
        var cmd = new MoveCardCategoryCommand(Guid.Empty, Guid.NewGuid(), DeckCategory.Maybeboard, DeckCategory.MainDeck);

        validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void MoveCardCategory_SameFromAndTo_Fails()
    {
        var validator = new MoveCardCategoryValidator();
        var cmd = new MoveCardCategoryCommand(Guid.NewGuid(), Guid.NewGuid(), DeckCategory.MainDeck, DeckCategory.MainDeck);

        validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void MoveCardCategory_Valid_Passes()
    {
        var validator = new MoveCardCategoryValidator();
        var cmd = new MoveCardCategoryCommand(Guid.NewGuid(), Guid.NewGuid(), DeckCategory.Maybeboard, DeckCategory.MainDeck);

        validator.Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void RemoveFromCollection_EmptyId_Fails()
    {
        var validator = new RemoveFromCollectionValidator();
        var cmd = new RemoveFromCollectionCommand(Guid.Empty);

        validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateCollectionEntry_ZeroQuantity_Fails()
    {
        var validator = new UpdateCollectionEntryValidator();
        var cmd = new UpdateCollectionEntryCommand(Guid.NewGuid(), 0, false, CardCondition.NearMint);

        validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateCollectionEntry_Valid_Passes()
    {
        var validator = new UpdateCollectionEntryValidator();
        var cmd = new UpdateCollectionEntryCommand(Guid.NewGuid(), 4, false, CardCondition.NearMint);

        validator.Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ImportDeck_EmptyDeckText_Fails()
    {
        var validator = new ImportDeckValidator();
        var cmd = new ImportDeckCommand("", "MTGO", "Test", Format.Modern, Guid.NewGuid());

        validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ImportDeck_EmptyName_Fails()
    {
        var validator = new ImportDeckValidator();
        var cmd = new ImportDeckCommand("4 Bolt", "MTGO", "", Format.Modern, Guid.NewGuid());

        validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ImportDeck_Valid_Passes()
    {
        var validator = new ImportDeckValidator();
        var cmd = new ImportDeckCommand("4 Bolt", "MTGO", "Test", Format.Modern, Guid.NewGuid());

        validator.Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ExportDeck_EmptyDeckId_Fails()
    {
        var validator = new ExportDeckValidator();
        var cmd = new ExportDeckQuery(Guid.Empty, "Text");

        validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ExportDeck_EmptyFormat_Fails()
    {
        var validator = new ExportDeckValidator();
        var cmd = new ExportDeckQuery(Guid.NewGuid(), "");

        validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void GetDeckStats_EmptyDeckId_Fails()
    {
        var validator = new GetDeckStatsValidator();
        var cmd = new GetDeckStatsQuery(Guid.Empty);

        validator.Validate(cmd).IsValid.Should().BeFalse();
    }
}
