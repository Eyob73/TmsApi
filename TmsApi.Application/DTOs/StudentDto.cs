namespace TmsApi.Application.DTOs;

public record StudentDto(
    int Id,
    string RegistrationNumber,
    string Name,
    decimal GPA,
    bool IsActive,
    DateTime LastUpdated
);
