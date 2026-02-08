using MtgDecker.Domain.Entities;

namespace MtgDecker.Application.Interfaces;

public interface ICollectionRepository
{
    Task<List<CollectionEntry>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<CollectionEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(CollectionEntry entry, CancellationToken ct = default);
    Task UpdateAsync(CollectionEntry entry, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<List<CollectionEntry>> SearchAsync(Guid userId, string? searchText, CancellationToken ct = default);
}
