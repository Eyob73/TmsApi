using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Domain.Entities;

namespace TmsApi.Infrastructure.Persistence.Configurations;

public class TmsUserConfiguration : IEntityTypeConfiguration<TmsUser>
{
    public void Configure(EntityTypeBuilder<TmsUser> builder)
    {
        builder.Property(u => u.FirstName).IsRequired();
        builder.Property(u => u.LastName).IsRequired();
        builder.Property(u => u.IsActive).IsRequired().HasDefaultValue(true);
        builder.Property(u => u.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(u => u.UpdatedAt);
        builder.Property(u => u.IsDeleted).IsRequired().HasDefaultValue(false);
        builder.Property(u => u.DeletedAt);

        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}
