using Microsoft.EntityFrameworkCore;
using MtgDecker.Domain.Entities;
using MtgDecker.Domain.ValueObjects;

namespace MtgDecker.Infrastructure.Data;

public class MtgDeckerDbContext : DbContext
{
    public MtgDeckerDbContext(DbContextOptions<MtgDeckerDbContext> options) : base(options) { }

    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CardFace> CardFaces => Set<CardFace>();
    public DbSet<Deck> Decks => Set<Deck>();
    public DbSet<DeckEntry> DeckEntries => Set<DeckEntry>();
    public DbSet<CollectionEntry> CollectionEntries => Set<CollectionEntry>();
    public DbSet<User> Users => Set<User>();
    public DbSet<BulkDataImportMetadata> BulkDataImports => Set<BulkDataImportMetadata>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MtgDeckerDbContext).Assembly);
    }
}
