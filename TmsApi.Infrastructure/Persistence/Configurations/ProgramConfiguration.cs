using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Domain.Entities;

namespace TmsApi.Infrastructure.Persistence.Configurations;

public class ProgramConfiguration : IEntityTypeConfiguration<Program>
{
    public void Configure(EntityTypeBuilder<Program> builder)
    {
        builder.ToTable("Programs");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(150);
        builder.Property(p => p.Code).HasMaxLength(30);
        builder.Property(p => p.Description).HasColumnType("TEXT");
        builder.Property(p => p.DepartmentId);
        builder.Property(p => p.IsActive).HasDefaultValue(true);
        builder.Property(p => p.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(p => new { p.DepartmentId, p.Name }).IsUnique();
        builder.HasIndex(p => p.Code).IsUnique();

        builder
            .HasOne(p => p.Department)
            .WithMany(d => d.Programs)
            .HasForeignKey(p => p.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
