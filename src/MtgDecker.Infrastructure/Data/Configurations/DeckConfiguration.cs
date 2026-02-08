using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Infrastructure.Data.Configurations;

public class DeckConfiguration : IEntityTypeConfiguration<Deck>
{
    public void Configure(EntityTypeBuilder<Deck> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Format).IsRequired();
        builder.Property(d => d.Description).HasMaxLength(2000);
        builder.Property(d => d.UserId).IsRequired();

        builder.HasIndex(d => d.UserId);

        builder.HasMany(d => d.Entries)
            .WithOne()
            .HasForeignKey(e => e.DeckId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(d => d.TotalMainDeckCount);
        builder.Ignore(d => d.TotalSideboardCount);
    }
}
