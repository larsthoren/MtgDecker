using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Infrastructure.Data.Configurations;

public class DeckEntryConfiguration : IEntityTypeConfiguration<DeckEntry>
{
    public void Configure(EntityTypeBuilder<DeckEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Category).IsRequired();
        builder.Property(e => e.Quantity).IsRequired();

        builder.HasIndex(e => e.DeckId);
        builder.HasIndex(e => e.CardId);
    }
}
