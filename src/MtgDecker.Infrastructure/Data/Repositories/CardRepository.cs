using Microsoft.EntityFrameworkCore;
using MtgDecker.Application.Cards;
using MtgDecker.Application.Interfaces;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.Enums;

namespace MtgDecker.Infrastructure.Data.Repositories;

public class CardRepository : ICardRepository
{
    private readonly MtgDeckerDbContext _context;

    public CardRepository(MtgDeckerDbContext context)
    {
        _context = context;
    }

    public async Task<Card?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Cards
            .Include(c => c.Faces)
            .Include(c => c.Legalities)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<Card?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        return await _context.Cards
            .Include(c => c.Faces)
            .Include(c => c.Legalities)
            .FirstOrDefaultAsync(c => c.Name == name, ct);
    }

    public async Task<(List<Card> Cards, int TotalCount)> SearchAsync(CardSearchFilter filter, CancellationToken ct = default)
    {
        var query = _context.Cards.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
            query = query.Where(c => c.Name.Contains(filter.SearchText));

        if (!string.IsNullOrWhiteSpace(filter.SetCode))
            query = query.Where(c => c.SetCode == filter.SetCode);

        if (!string.IsNullOrWhiteSpace(filter.Rarity))
            query = query.Where(c => c.Rarity == filter.Rarity);

        if (!string.IsNullOrWhiteSpace(filter.Type))
            query = query.Where(c => c.TypeLine.Contains(filter.Type));

        if (filter.MinCmc.HasValue)
            query = query.Where(c => c.Cmc >= filter.MinCmc.Value);

        if (filter.MaxCmc.HasValue)
            query = query.Where(c => c.Cmc <= filter.MaxCmc.Value);

        if (filter.Colors is { Count: > 0 })
        {
            foreach (var color in filter.Colors)
                query = query.Where(c => c.Colors.Contains(color));
        }

        if (!string.IsNullOrWhiteSpace(filter.Format))
        {
            query = query.Where(c =>
                c.Legalities.Any(l => l.FormatName == filter.Format &&
                    (l.Status == LegalityStatus.Legal || l.Status == LegalityStatus.Restricted)));
        }

        var totalCount = await query.CountAsync(ct);

        var cards = await query
            .OrderBy(c => c.Name)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Include(c => c.Faces)
            .Include(c => c.Legalities)
            .ToListAsync(ct);

        return (cards, totalCount);
    }

    public async Task<List<Card>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        return await _context.Cards
            .Include(c => c.Faces)
            .Include(c => c.Legalities)
            .Where(c => idList.Contains(c.Id))
            .ToListAsync(ct);
    }

    public async Task<List<Card>> GetByOracleIdAsync(string oracleId, CancellationToken ct = default)
    {
        return await _context.Cards
            .Include(c => c.Faces)
            .Include(c => c.Legalities)
            .Where(c => c.OracleId == oracleId)
            .ToListAsync(ct);
    }

    public async Task UpsertBatchAsync(IEnumerable<Card> cards, CancellationToken ct = default)
    {
        foreach (var card in cards)
        {
            var existing = await _context.Cards
                .FirstOrDefaultAsync(c => c.ScryfallId == card.ScryfallId, ct);

            if (existing == null)
            {
                _context.Cards.Add(card);
            }
            else
            {
                existing.OracleId = card.OracleId;
                existing.Name = card.Name;
                existing.ManaCost = card.ManaCost;
                existing.Cmc = card.Cmc;
                existing.TypeLine = card.TypeLine;
                existing.OracleText = card.OracleText;
                existing.Colors = card.Colors;
                existing.ColorIdentity = card.ColorIdentity;
                existing.Rarity = card.Rarity;
                existing.SetCode = card.SetCode;
                existing.SetName = card.SetName;
                existing.CollectorNumber = card.CollectorNumber;
                existing.ImageUri = card.ImageUri;
                existing.ImageUriSmall = card.ImageUriSmall;
                existing.ImageUriArtCrop = card.ImageUriArtCrop;
                existing.Layout = card.Layout;
                existing.PriceUsd = card.PriceUsd;
                existing.PriceUsdFoil = card.PriceUsdFoil;
                existing.PriceEur = card.PriceEur;
                existing.PriceEurFoil = card.PriceEurFoil;
                existing.PriceTix = card.PriceTix;
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<SetInfo>> GetDistinctSetsAsync(string searchText, CancellationToken ct = default)
    {
        var query = _context.Cards
            .Select(c => new { c.SetCode, c.SetName })
            .Distinct();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(s =>
                s.SetName.Contains(searchText) || s.SetCode.Contains(searchText));
        }

        return await query
            .OrderBy(s => s.SetName)
            .Take(20)
            .Select(s => new SetInfo(s.SetCode, s.SetName))
            .ToListAsync(ct);
    }

    public async Task<List<string>> GetDistinctTypesAsync(string searchText, CancellationToken ct = default)
    {
        var query = _context.Cards
            .Select(c => c.TypeLine)
            .Distinct();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(t => t.Contains(searchText));
        }

        // Get distinct type lines, then extract unique base types
        var typeLines = await query.Take(200).ToListAsync(ct);

        return typeLines
            .SelectMany(t => t.Split('—')[0].Trim().Split(' '))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(t => t.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t)
            .Take(20)
            .ToList();
    }
}
