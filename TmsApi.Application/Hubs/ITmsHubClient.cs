using System.Threading.Tasks;
using TmsApi.Application.DTOs;

namespace TmsApi.Application.Hubs;

public interface ITmsHubClient
{
    Task ReceiveTranscriptReady(string reportId, string downloadUrl);
    Task ReceiveCourseUpdate(string courseCode, string message);
    Task ReceiveGradePosted(string courseCode, int studentId, decimal grade);

    // Real-time enrollment lifecycle events
    Task ReceiveEnrollmentCreated(int enrollmentId, int studentId, int courseId, string status);
    Task ReceiveEnrollmentApproved(int enrollmentId);
    Task ReceiveEnrollmentRejected(int enrollmentId, string? reason);
    Task ReceiveEnrollmentCancelled(int enrollmentId);
    Task ReceiveEnrollmentStatusUpdated(string enrollmentId, string status);

    // Real-time notification push
    Task ReceiveNotification(NotificationDto notification);
}
