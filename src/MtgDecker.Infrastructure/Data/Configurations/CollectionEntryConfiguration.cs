using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Infrastructure.Data.Configurations;

public class CollectionEntryConfiguration : IEntityTypeConfiguration<CollectionEntry>
{
    public void Configure(EntityTypeBuilder<CollectionEntry> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId).IsRequired();
        builder.Property(c => c.CardId).IsRequired();
        builder.Property(c => c.Quantity).IsRequired();
        builder.Property(c => c.Condition).IsRequired();

        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.CardId);
    }
}
