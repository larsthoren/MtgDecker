using Microsoft.EntityFrameworkCore;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Infrastructure.Data.Repositories;

public class CollectionRepository : ICollectionRepository
{
    private readonly MtgDeckerDbContext _context;

    public CollectionRepository(MtgDeckerDbContext context)
    {
        _context = context;
    }

    public async Task<List<CollectionEntry>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.CollectionEntries
            .Where(e => e.UserId == userId)
            .ToListAsync(ct);
    }

    public async Task<CollectionEntry?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.CollectionEntries.FindAsync(new object[] { id }, ct);
    }

    public async Task AddAsync(CollectionEntry entry, CancellationToken ct = default)
    {
        _context.CollectionEntries.Add(entry);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(CollectionEntry entry, CancellationToken ct = default)
    {
        _context.CollectionEntries.Update(entry);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _context.CollectionEntries.FindAsync(new object[] { id }, ct);
        if (entry != null)
        {
            _context.CollectionEntries.Remove(entry);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<List<CollectionEntry>> SearchAsync(Guid userId, string? searchText, CancellationToken ct = default)
    {
        var query = _context.CollectionEntries
            .Where(e => e.UserId == userId);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var matchingCardIds = _context.Cards
                .Where(c => c.Name.Contains(searchText))
                .Select(c => c.Id);

            query = query.Where(e => matchingCardIds.Contains(e.CardId));
        }

        return await query.ToListAsync(ct);
    }
}
