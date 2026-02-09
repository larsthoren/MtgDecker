using FluentAssertions;
using FluentValidation;
using MtgDecker.Application.Cards;
using MtgDecker.Application.Collection;
using MtgDecker.Application.Decks;
using MtgDecker.Application.DeckExport;
using MtgDecker.Application.Interfaces;
using MtgDecker.Application.Stats;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Application.Tests.Validators;

public class CommandValidatorTests
{
    // ── Existing tests ──────────────────────────────────────────────────

    [Fact]
    public void RemoveCardFromDeck_EmptyDeckId_Fails()
    {
        var validator = new RemoveCardFromDeckValidator();
        var cmd = new RemoveCardFromDeckCommand(Guid.Empty, Guid.NewGuid(), DeckCategory.MainDeck);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DeckId");
    }

    [Fact]
    public void RemoveCardFromDeck_EmptyCardId_Fails()
    {
        var validator = new RemoveCardFromDeckValidator();
        var cmd = new RemoveCardFromDeckCommand(Guid.NewGuid(), Guid.Empty, DeckCategory.MainDeck);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CardId");
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

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Quantity");
    }

    [Fact]
    public void UpdateCardQuantity_EmptyDeckId_Fails()
    {
        var validator = new UpdateCardQuantityValidator();
        var cmd = new UpdateCardQuantityCommand(Guid.Empty, Guid.NewGuid(), DeckCategory.MainDeck, 1);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DeckId");
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

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DeckId");
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

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Id");
    }

    [Fact]
    public void UpdateCollectionEntry_ZeroQuantity_Fails()
    {
        var validator = new UpdateCollectionEntryValidator();
        var cmd = new UpdateCollectionEntryCommand(Guid.NewGuid(), 0, false, CardCondition.NearMint);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Quantity");
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

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DeckText");
    }

    [Fact]
    public void ImportDeck_EmptyName_Fails()
    {
        var validator = new ImportDeckValidator();
        var cmd = new ImportDeckCommand("4 Bolt", "MTGO", "", Format.Modern, Guid.NewGuid());

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DeckName");
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

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DeckId");
    }

    [Fact]
    public void ExportDeck_EmptyFormat_Fails()
    {
        var validator = new ExportDeckValidator();
        var cmd = new ExportDeckQuery(Guid.NewGuid(), "");

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Format");
    }

    [Fact]
    public void GetDeckStats_EmptyDeckId_Fails()
    {
        var validator = new GetDeckStatsValidator();
        var cmd = new GetDeckStatsQuery(Guid.Empty);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DeckId");
    }

    // ── AddCardToDeck validator tests (validator exists, tests missing) ──

    [Fact]
    public void AddCardToDeck_EmptyDeckId_Fails()
    {
        var validator = new AddCardToDeckValidator();
        var cmd = new AddCardToDeckCommand(Guid.Empty, Guid.NewGuid(), 1, DeckCategory.MainDeck);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DeckId");
    }

    [Fact]
    public void AddCardToDeck_EmptyCardId_Fails()
    {
        var validator = new AddCardToDeckValidator();
        var cmd = new AddCardToDeckCommand(Guid.NewGuid(), Guid.Empty, 1, DeckCategory.MainDeck);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CardId");
    }

    [Fact]
    public void AddCardToDeck_ZeroQuantity_Fails()
    {
        var validator = new AddCardToDeckValidator();
        var cmd = new AddCardToDeckCommand(Guid.NewGuid(), Guid.NewGuid(), 0, DeckCategory.MainDeck);

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Quantity");
    }

    [Fact]
    public void AddCardToDeck_Valid_Passes()
    {
        var validator = new AddCardToDeckValidator();
        var cmd = new AddCardToDeckCommand(Guid.NewGuid(), Guid.NewGuid(), 4, DeckCategory.MainDeck);

        validator.Validate(cmd).IsValid.Should().BeTrue();
    }

    // ── GetCardById validator tests ─────────────────────────────────────

    [Fact]
    public void GetCardById_EmptyId_Fails()
    {
        var validator = new GetCardByIdValidator();
        var result = validator.Validate(new GetCardByIdQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Id");
    }

    [Fact]
    public void GetCardById_Valid_Passes()
    {
        var validator = new GetCardByIdValidator();
        var result = validator.Validate(new GetCardByIdQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    // ── GetCardByName validator tests ───────────────────────────────────

    [Fact]
    public void GetCardByName_EmptyName_Fails()
    {
        var validator = new GetCardByNameValidator();
        var result = validator.Validate(new GetCardByNameQuery(""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void GetCardByName_Valid_Passes()
    {
        var validator = new GetCardByNameValidator();
        var result = validator.Validate(new GetCardByNameQuery("Lightning Bolt"));

        result.IsValid.Should().BeTrue();
    }

    // ── GetCardsByIds validator tests ────────────────────────────────────

    [Fact]
    public void GetCardsByIds_EmptyList_Fails()
    {
        var validator = new GetCardsByIdsValidator();
        var result = validator.Validate(new GetCardsByIdsQuery(new List<Guid>()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CardIds");
    }

    [Fact]
    public void GetCardsByIds_NullList_Fails()
    {
        var validator = new GetCardsByIdsValidator();
        var result = validator.Validate(new GetCardsByIdsQuery(null!));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CardIds");
    }

    [Fact]
    public void GetCardsByIds_Valid_Passes()
    {
        var validator = new GetCardsByIdsValidator();
        var result = validator.Validate(new GetCardsByIdsQuery(new List<Guid> { Guid.NewGuid() }));

        result.IsValid.Should().BeTrue();
    }

    // ── SearchCards validator tests ──────────────────────────────────────

    [Fact]
    public void SearchCards_NullFilter_Fails()
    {
        var validator = new SearchCardsValidator();
        var result = validator.Validate(new SearchCardsQuery(null!));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Filter");
    }

    [Fact]
    public void SearchCards_PageSizeZero_Fails()
    {
        var validator = new SearchCardsValidator();
        var filter = new CardSearchFilter { PageSize = 0 };
        var result = validator.Validate(new SearchCardsQuery(filter));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Filter.PageSize");
    }

    [Fact]
    public void SearchCards_PageSizeTooLarge_Fails()
    {
        var validator = new SearchCardsValidator();
        var filter = new CardSearchFilter { PageSize = 101 };
        var result = validator.Validate(new SearchCardsQuery(filter));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Filter.PageSize");
    }

    [Fact]
    public void SearchCards_PageZero_Fails()
    {
        var validator = new SearchCardsValidator();
        var filter = new CardSearchFilter { Page = 0 };
        var result = validator.Validate(new SearchCardsQuery(filter));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Filter.Page");
    }

    [Fact]
    public void SearchCards_Valid_Passes()
    {
        var validator = new SearchCardsValidator();
        var filter = new CardSearchFilter { Page = 1, PageSize = 20 };
        var result = validator.Validate(new SearchCardsQuery(filter));

        result.IsValid.Should().BeTrue();
    }

    // ── SearchSetNames validator tests ───────────────────────────────────

    [Fact]
    public void SearchSetNames_EmptySearchText_Fails()
    {
        var validator = new SearchSetNamesValidator();
        var result = validator.Validate(new SearchSetNamesQuery(""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SearchText");
    }

    [Fact]
    public void SearchSetNames_Valid_Passes()
    {
        var validator = new SearchSetNamesValidator();
        var result = validator.Validate(new SearchSetNamesQuery("Modern"));

        result.IsValid.Should().BeTrue();
    }

    // ── SearchTypeNames validator tests ──────────────────────────────────

    [Fact]
    public void SearchTypeNames_EmptySearchText_Fails()
    {
        var validator = new SearchTypeNamesValidator();
        var result = validator.Validate(new SearchTypeNamesQuery(""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SearchText");
    }

    [Fact]
    public void SearchTypeNames_Valid_Passes()
    {
        var validator = new SearchTypeNamesValidator();
        var result = validator.Validate(new SearchTypeNamesQuery("Creature"));

        result.IsValid.Should().BeTrue();
    }

    // ── SearchCollection validator tests ────────────────────────────────

    [Fact]
    public void SearchCollection_EmptyUserId_Fails()
    {
        var validator = new SearchCollectionValidator();
        var result = validator.Validate(new SearchCollectionQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId");
    }

    [Fact]
    public void SearchCollection_Valid_Passes()
    {
        var validator = new SearchCollectionValidator();
        var result = validator.Validate(new SearchCollectionQuery(Guid.NewGuid(), "Bolt"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SearchCollection_NullSearchText_Valid_Passes()
    {
        var validator = new SearchCollectionValidator();
        var result = validator.Validate(new SearchCollectionQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    // ── DeleteDeck validator tests ──────────────────────────────────────

    [Fact]
    public void DeleteDeck_EmptyId_Fails()
    {
        var validator = new DeleteDeckValidator();
        var result = validator.Validate(new DeleteDeckCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Id");
    }

    [Fact]
    public void DeleteDeck_Valid_Passes()
    {
        var validator = new DeleteDeckValidator();
        var result = validator.Validate(new DeleteDeckCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    // ── GetDeck validator tests ─────────────────────────────────────────

    [Fact]
    public void GetDeck_EmptyId_Fails()
    {
        var validator = new GetDeckValidator();
        var result = validator.Validate(new GetDeckQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Id");
    }

    [Fact]
    public void GetDeck_Valid_Passes()
    {
        var validator = new GetDeckValidator();
        var result = validator.Validate(new GetDeckQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    // ── ListDecks validator tests ───────────────────────────────────────

    [Fact]
    public void ListDecks_EmptyUserId_Fails()
    {
        var validator = new ListDecksValidator();
        var result = validator.Validate(new ListDecksQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId");
    }

    [Fact]
    public void ListDecks_Valid_Passes()
    {
        var validator = new ListDecksValidator();
        var result = validator.Validate(new ListDecksQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    // ── GetDeckShortages validator tests ─────────────────────────────────

    [Fact]
    public void GetDeckShortages_EmptyDeckId_Fails()
    {
        var validator = new GetDeckShortagesValidator();
        var result = validator.Validate(new GetDeckShortagesQuery(Guid.Empty, Guid.NewGuid()));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DeckId");
    }

    [Fact]
    public void GetDeckShortages_EmptyUserId_Fails()
    {
        var validator = new GetDeckShortagesValidator();
        var result = validator.Validate(new GetDeckShortagesQuery(Guid.NewGuid(), Guid.Empty));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "UserId");
    }

    [Fact]
    public void GetDeckShortages_Valid_Passes()
    {
        var validator = new GetDeckShortagesValidator();
        var result = validator.Validate(new GetDeckShortagesQuery(Guid.NewGuid(), Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    // ── ExportDeck format constraint tests ──────────────────────────────

    [Fact]
    public void ExportDeck_InvalidFormat_Fails()
    {
        var validator = new ExportDeckValidator();
        var cmd = new ExportDeckQuery(Guid.NewGuid(), "InvalidFormat");

        var result = validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Format");
    }

    [Theory]
    [InlineData("Text")]
    [InlineData("CSV")]
    [InlineData("MTGO")]
    [InlineData("Arena")]
    [InlineData("text")]
    [InlineData("arena")]
    public void ExportDeck_ValidFormats_Pass(string format)
    {
        var validator = new ExportDeckValidator();
        var cmd = new ExportDeckQuery(Guid.NewGuid(), format);

        validator.Validate(cmd).IsValid.Should().BeTrue();
    }
}
