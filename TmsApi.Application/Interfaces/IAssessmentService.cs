using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TmsApi.Application.DTOs;

namespace TmsApi.Application.Interfaces;

public interface IAssessmentService
{
    Task<IReadOnlyList<AssessmentDto>> GetAssessmentsAsync(int? courseId, CancellationToken ct);
    Task<AssessmentDto?> GetAssessmentByIdAsync(int id, CancellationToken ct);
    Task<AssessmentDto> CreateAssessmentAsync(CreateAssessmentDto request, string userId, CancellationToken ct);
    Task<AssessmentDto> UpdateAssessmentAsync(int id, UpdateAssessmentDto request, string userId, CancellationToken ct);
    Task DeleteAssessmentAsync(int id, string userId, CancellationToken ct);
    Task PublishAssessmentAsync(int id, string userId, CancellationToken ct);
    Task UnpublishAssessmentAsync(int id, string userId, CancellationToken ct);
    Task<IReadOnlyList<AssessmentResultDto>> GetResultsByAssessmentAsync(int assessmentId, CancellationToken ct);
    Task<IReadOnlyList<AssessmentResultDto>> GetResultsByStudentAsync(int studentId, CancellationToken ct);
    Task SaveBulkMarksAsync(int assessmentId, BulkMarksEntryRequest request, string userId, CancellationToken ct);
    Task<AssessmentStatisticsDto> GetStatisticsAsync(int assessmentId, CancellationToken ct);
    Task<bool> IsAuthorizedAsync(int courseId, string userId, bool isAdmin, CancellationToken ct);
    Task<bool> IsAuthorizedForAssessmentAsync(int assessmentId, string userId, bool isAdmin, CancellationToken ct);
}
