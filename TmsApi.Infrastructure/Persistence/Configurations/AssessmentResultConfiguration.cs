using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Domain.Entities;

namespace TmsApi.Infrastructure.Persistence.Configurations;

public class AssessmentResultConfiguration : IEntityTypeConfiguration<AssessmentResult>
{
    public void Configure(EntityTypeBuilder<AssessmentResult> builder)
    {
        builder.ToTable("AssessmentResults");
        
        builder.HasKey(ar => ar.Id);

        builder.Property(ar => ar.MarksObtained)
            .HasPrecision(5, 2);

        builder.Property(ar => ar.Percentage)
            .HasPrecision(5, 2);

        builder.Property(ar => ar.Grade)
            .HasMaxLength(20);
            
        builder.Property(ar => ar.Status)
            .HasConversion<string>();

        builder.HasOne(ar => ar.Assessment)
            .WithMany(a => a.Results)
            .HasForeignKey(ar => ar.AssessmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ar => ar.Student)
            .WithMany()
            .HasForeignKey(ar => ar.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ar => ar.Enrollment)
            .WithMany()
            .HasForeignKey(ar => ar.EnrollmentId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.HasIndex(ar => new { ar.AssessmentId, ar.StudentId }).IsUnique();
    }
}
