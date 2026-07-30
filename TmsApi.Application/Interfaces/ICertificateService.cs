namespace TmsApi.Application.Interfaces;

public sealed record CertificateResult(string Status, int Attempt);

public interface ICertificateService
{
    Task<CertificateResult> IssueCertificateAsync(
        int studentId,
        string courseCode,
        CancellationToken ct
    );
}
