using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Interfaces;

public interface IDeckRepository
{
    Task<Deck?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Deck>> ListByUserAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Deck deck, CancellationToken ct = default);
    Task UpdateAsync(Deck deck, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
