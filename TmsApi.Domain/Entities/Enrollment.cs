using System;
using TmsApi.Domain.Enums;

namespace TmsApi.Domain.Entities;

public class Enrollment
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Pending;
    public decimal? Grade { get; set; }
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public DateTime EnrollmentDate => EnrolledAt;

    public DateTime? ApprovedDate { get; set; }
    public string? ApprovedBy { get; set; }

    public DateTime? RejectedDate { get; set; }
    public string? RejectedBy { get; set; }
    public string? RejectionReason { get; set; }

    public DateTime? CancellationDate { get; set; }
    public string? CancelledBy { get; set; }

    public DateTime? CompletionDate { get; set; }

    public bool IsArchived { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Student Student { get; set; } = null!;
    public Course Course { get; set; } = null!;

    public bool CanBeApproved => Status == EnrollmentStatus.Pending;
    public bool CanBeRejected => Status == EnrollmentStatus.Pending;
    public bool CanBeCancelled => Status == EnrollmentStatus.Pending || Status == EnrollmentStatus.Approved;
    public bool CanBeArchived => Status == EnrollmentStatus.Rejected || Status == EnrollmentStatus.Cancelled || Status == EnrollmentStatus.Completed;

    public void Approve(string approverId)
    {
        if (!CanBeApproved)
            throw new InvalidOperationException($"Cannot approve enrollment in status '{Status}'. Only Pending enrollments can be approved.");

        Status = EnrollmentStatus.Approved;
        ApprovedDate = DateTime.UtcNow;
        ApprovedBy = approverId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject(string reason, string reviewerId)
    {
        if (!CanBeRejected)
            throw new InvalidOperationException($"Cannot reject enrollment in status '{Status}'. Only Pending enrollments can be rejected.");

        Status = EnrollmentStatus.Rejected;
        RejectedDate = DateTime.UtcNow;
        RejectedBy = reviewerId;
        RejectionReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel(string cancelledById)
    {
        if (!CanBeCancelled)
            throw new InvalidOperationException($"Cannot cancel enrollment in status '{Status}'.");

        Status = EnrollmentStatus.Cancelled;
        CancellationDate = DateTime.UtcNow;
        CancelledBy = cancelledById;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        if (Status != EnrollmentStatus.Approved)
            throw new InvalidOperationException($"Cannot complete enrollment in status '{Status}'. Only Approved enrollments can be completed.");

        Status = EnrollmentStatus.Completed;
        CompletionDate = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        if (!CanBeArchived)
            throw new InvalidOperationException($"Cannot archive enrollment in status '{Status}'. Only Rejected, Cancelled, or Completed enrollments can be archived.");

        IsArchived = true;
        Status = EnrollmentStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }
}
