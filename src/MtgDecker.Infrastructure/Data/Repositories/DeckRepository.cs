using Microsoft.EntityFrameworkCore;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Infrastructure.Data.Repositories;

public class DeckRepository : IDeckRepository
{
    private readonly MtgDeckerDbContext _context;

    public DeckRepository(MtgDeckerDbContext context)
    {
        _context = context;
    }

    public async Task<Deck?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Decks
            .Include(d => d.Entries)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<List<Deck>> ListByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.Decks
            .Include(d => d.Entries)
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync(ct);
    }

    public async Task AddAsync(Deck deck, CancellationToken ct = default)
    {
        _context.Decks.Add(deck);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Deck deck, CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var deck = await _context.Decks.FindAsync(new object[] { id }, ct);
        if (deck != null)
        {
            _context.Decks.Remove(deck);
            await _context.SaveChangesAsync(ct);
        }
    }
}
