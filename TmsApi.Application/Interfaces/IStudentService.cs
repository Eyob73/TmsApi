using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;

namespace TmsApi.Application.Interfaces;

public interface IStudentService
{
    Task<StudentDto?> GetByIdAsync(string id);
    Task<IReadOnlyList<StudentDto>> GetAllAsync();
    Task<IReadOnlyList<StudentDto>> GetByNameAsync(int page = 1, CancellationToken ct = default);
    Task<Student> CreateAsync(Student student, CancellationToken cancellationToken = default);
    Task<Student> UpdateAsync(Student student, CancellationToken cancellationToken = default);
}
