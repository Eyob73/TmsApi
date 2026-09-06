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

        builder.Property(c => c.CourseCode).IsRequired().HasMaxLength(20);

        builder.HasIndex(c => c.CourseCode).IsUnique();

        builder.Property(c => c.CourseName).IsRequired().HasMaxLength(150);

        builder.Property(c => c.Description).HasColumnType("TEXT");

        builder.Property(c => c.Credits).IsRequired();

        builder.Property(c => c.DepartmentId).IsRequired();

        builder.Property(c => c.ProgramId);

        builder.Property(c => c.Level).HasMaxLength(30);

        builder.Property(c => c.Semester).HasMaxLength(30);

        builder.Property(c => c.CourseType).IsRequired().HasMaxLength(30);

        builder.Property(c => c.PrerequisiteCourseId);

        builder.Property(c => c.DurationHours);

        builder.Property(c => c.Status).IsRequired().HasMaxLength(20);

        builder.Property(c => c.IsPublished).IsRequired();

        builder.Property(c => c.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(c => c.UpdatedAt);

        builder.Property(c => c.CreatedBy);

        builder.Property(c => c.UpdatedBy);

        builder.Property(c => c.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.Property(c => c.DeletedAt);

        // Legacy fields for backward compatibility
        builder.Ignore(c => c.Code);
        builder.Ignore(c => c.Title);
        builder.Ignore(c => c.MaxCapacity);
    }
}
