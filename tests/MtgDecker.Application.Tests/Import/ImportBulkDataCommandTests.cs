using FluentAssertions;
using NSubstitute;
using MtgDecker.Application.Import;
using MtgDecker.Application.Interfaces;

namespace MtgDecker.Application.Tests.Import;

public class ImportBulkDataCommandTests
{
    private readonly IScryfallClient _scryfallClient = Substitute.For<IScryfallClient>();
    private readonly IBulkDataImporter _importer = Substitute.For<IBulkDataImporter>();
    private readonly ImportBulkDataHandler _handler;

    public ImportBulkDataCommandTests()
    {
        _handler = new ImportBulkDataHandler(_scryfallClient, _importer);
    }

    [Fact]
    public async Task Handle_DownloadsAndImports()
    {
        var info = new BulkDataInfo { DownloadUri = "https://data.scryfall.io/test.json", Size = 1000 };
        _scryfallClient.GetBulkDataInfoAsync("default_cards", Arg.Any<CancellationToken>()).Returns(info);

        var stream = new MemoryStream();
        _scryfallClient.DownloadBulkDataAsync(info.DownloadUri, Arg.Any<CancellationToken>()).Returns(stream);
        _importer.ImportFromStreamAsync(stream, null, Arg.Any<CancellationToken>()).Returns(500);

        var result = await _handler.Handle(new ImportBulkDataCommand(), CancellationToken.None);

        result.Should().Be(500);
    }

    [Fact]
    public async Task Handle_NoBulkDataInfo_Throws()
    {
        _scryfallClient.GetBulkDataInfoAsync("default_cards", Arg.Any<CancellationToken>())
            .Returns((BulkDataInfo?)null);

        var act = () => _handler.Handle(new ImportBulkDataCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
