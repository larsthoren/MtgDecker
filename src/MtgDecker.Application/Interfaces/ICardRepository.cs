using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Interfaces;

public interface ICardRepository
{
    Task<Card?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Card?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<(List<Card> Cards, int TotalCount)> SearchAsync(CardSearchFilter filter, CancellationToken ct = default);
    Task<List<Card>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<List<Card>> GetByOracleIdAsync(string oracleId, CancellationToken ct = default);
    Task UpsertBatchAsync(IEnumerable<Card> cards, CancellationToken ct = default);
}

public class CardSearchFilter
{
    public string? SearchText { get; set; }
    public string? Format { get; set; }
    public List<string>? Colors { get; set; }
    public string? Type { get; set; }
    public double? MinCmc { get; set; }
    public double? MaxCmc { get; set; }
    public string? Rarity { get; set; }
    public string? SetCode { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
