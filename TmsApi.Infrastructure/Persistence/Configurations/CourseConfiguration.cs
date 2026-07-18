using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Domain.Entities;

namespace TmsApi.Infrastructure.Persistence.Configurations;

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("Courses");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).ValueGeneratedOnAdd();

        builder.Property(c => c.Code).IsRequired().HasMaxLength(10);

        builder.HasIndex(c => c.Code).IsUnique();

        builder.Property(c => c.Title).IsRequired().HasMaxLength(200);

        builder.Property(c => c.MaxCapacity).IsRequired();
    }
}
