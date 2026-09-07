using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Domain.Entities;

namespace TmsApi.Infrastructure.Persistence.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name).IsRequired().HasMaxLength(150);
        builder.Property(d => d.Code).HasMaxLength(30);
        builder.Property(d => d.Description).HasColumnType("TEXT");
        builder.Property(d => d.IsActive).HasDefaultValue(true);
        builder.Property(d => d.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(d => d.Name).IsUnique();
        builder.HasIndex(d => d.Code).IsUnique();
    }
}
