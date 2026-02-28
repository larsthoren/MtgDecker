using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Infrastructure.Data.Configurations;

public class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ScryfallId).HasMaxLength(36).IsRequired();
        builder.Property(c => c.OracleId).HasMaxLength(36).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(300).IsRequired();
        builder.Property(c => c.ManaCost).HasMaxLength(50);
        builder.Property(c => c.TypeLine).HasMaxLength(200).IsRequired();
        builder.Property(c => c.OracleText).HasColumnType("nvarchar(max)");
        builder.Property(c => c.Colors).HasMaxLength(20);
        builder.Property(c => c.ColorIdentity).HasMaxLength(20);
        builder.Property(c => c.Rarity).HasMaxLength(20).IsRequired();
        builder.Property(c => c.SetCode).HasMaxLength(10).IsRequired();
        builder.Property(c => c.SetName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.CollectorNumber).HasMaxLength(20);
        builder.Property(c => c.ImageUri).HasMaxLength(500);
        builder.Property(c => c.ImageUriSmall).HasMaxLength(500);
        builder.Property(c => c.ImageUriArtCrop).HasMaxLength(500);
        builder.Property(c => c.Layout).HasMaxLength(30);
        builder.Property(c => c.Power).HasMaxLength(10);
        builder.Property(c => c.Toughness).HasMaxLength(10);

        builder.Property(c => c.PriceUsd).HasColumnType("decimal(10,2)");
        builder.Property(c => c.PriceUsdFoil).HasColumnType("decimal(10,2)");
        builder.Property(c => c.PriceEur).HasColumnType("decimal(10,2)");
        builder.Property(c => c.PriceEurFoil).HasColumnType("decimal(10,2)");
        builder.Property(c => c.PriceTix).HasColumnType("decimal(10,2)");

        builder.HasIndex(c => c.Name);
        builder.HasIndex(c => c.OracleId);
        builder.HasIndex(c => c.SetCode);
        builder.HasIndex(c => c.ScryfallId).IsUnique();
        builder.HasIndex(c => c.Rarity);
        builder.HasIndex(c => c.Cmc);

        builder.HasMany(c => c.Faces)
            .WithOne()
            .HasForeignKey(f => f.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Legalities)
            .WithOne()
            .HasForeignKey("CardId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(c => c.IsLand);
        builder.Ignore(c => c.IsBasicLand);
        builder.Ignore(c => c.HasMultipleFaces);
    }
}
