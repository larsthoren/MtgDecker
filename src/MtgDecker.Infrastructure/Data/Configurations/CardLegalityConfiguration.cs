using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MtgDecker.Domain.ValueObjects;

namespace MtgDecker.Infrastructure.Data.Configurations;

public class CardLegalityConfiguration : IEntityTypeConfiguration<CardLegality>
{
    public void Configure(EntityTypeBuilder<CardLegality> builder)
    {
        builder.ToTable("CardLegalities");

        builder.HasKey("CardId", nameof(CardLegality.FormatName));

        builder.Property(l => l.FormatName).HasMaxLength(30).IsRequired();
        builder.Property(l => l.Status).IsRequired();
    }
}
