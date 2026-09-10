using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Domain.Entities;
using TmsApi.Domain.Enums;

namespace TmsApi.Infrastructure.Persistence.Configurations;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("Enrollments");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();

        builder.Property(e => e.StudentId).IsRequired();
        builder.Property(e => e.CourseId).IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired()
            .HasDefaultValue(EnrollmentStatus.Pending);

        builder.Property(e => e.Grade).HasPrecision(5, 2);
        builder.Property(e => e.EnrolledAt).IsRequired();

        builder.Property(e => e.ApprovedDate);
        builder.Property(e => e.ApprovedBy).HasMaxLength(100);

        builder.Property(e => e.RejectedDate);
        builder.Property(e => e.RejectedBy).HasMaxLength(100);
        builder.Property(e => e.RejectionReason).HasMaxLength(500);

        builder.Property(e => e.CancellationDate);
        builder.Property(e => e.CancelledBy).HasMaxLength(100);

        builder.Property(e => e.CompletionDate);

        builder.Property(e => e.IsArchived).IsRequired().HasDefaultValue(false);
        builder.Property(e => e.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(e => e.UpdatedAt);

        builder.Ignore(e => e.EnrollmentDate);
        builder.Ignore(e => e.CanBeApproved);
        builder.Ignore(e => e.CanBeRejected);
        builder.Ignore(e => e.CanBeCancelled);
        builder.Ignore(e => e.CanBeArchived);

        builder
            .HasOne(e => e.Student)
            .WithMany(s => s.Enrollments)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(e => e.Course)
            .WithMany(c => c.Enrollments)
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.CourseId);
        builder.HasIndex(e => e.StudentId);
        builder.HasIndex(e => e.Status);

        builder.HasIndex(e => new { e.StudentId, e.CourseId })
            .HasFilter("\"Status\" IN ('Pending', 'Approved', 'Completed') AND \"IsArchived\" = false")
            .IsUnique();
    }
}
