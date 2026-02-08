using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Infrastructure.Data.Configurations;

public class CardFaceConfiguration : IEntityTypeConfiguration<CardFace>
{
    public void Configure(EntityTypeBuilder<CardFace> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name).HasMaxLength(300).IsRequired();
        builder.Property(f => f.ManaCost).HasMaxLength(50);
        builder.Property(f => f.TypeLine).HasMaxLength(200);
        builder.Property(f => f.OracleText).HasMaxLength(1000);
        builder.Property(f => f.ImageUri).HasMaxLength(500);
        builder.Property(f => f.Power).HasMaxLength(10);
        builder.Property(f => f.Toughness).HasMaxLength(10);
    }
}
