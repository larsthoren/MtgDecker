using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MtgDecker.Domain.Entities;

namespace MtgDecker.Infrastructure.Data.Configurations;

public class BulkDataImportMetadataConfiguration : IEntityTypeConfiguration<BulkDataImportMetadata>
{
    public void Configure(EntityTypeBuilder<BulkDataImportMetadata> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.ScryfallDataType).HasMaxLength(50).IsRequired();
    }
}
