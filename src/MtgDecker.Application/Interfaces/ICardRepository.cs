using MtgDecker.Application.Cards;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Interfaces;

public interface ICardRepository
{
    Task<Card?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Card?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<(List<Card> Cards, int TotalCount)> SearchAsync(CardSearchFilter filter, CancellationToken ct = default);
    Task<List<Card>> GetByNamesAsync(IEnumerable<string> names, CancellationToken ct = default);
    Task<List<Card>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<List<Card>> GetByOracleIdAsync(string oracleId, CancellationToken ct = default);
    Task UpsertBatchAsync(IEnumerable<Card> cards, CancellationToken ct = default);
    Task<List<Cards.SetInfo>> GetDistinctSetsAsync(string searchText, CancellationToken ct = default);
    Task<List<string>> GetDistinctTypesAsync(string searchText, CancellationToken ct = default);
}
