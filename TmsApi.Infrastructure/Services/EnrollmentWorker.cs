using Microsoft.Extensions.DependencyInjection;
using TmsApi.Application.Interfaces;
using TmsApi.Infrastructure.Services;

public class EnrollmentWorker(IServiceScopeFactory scopeFactory)
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    public void ProcessBatch()
    {
        using var scope = _scopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();
        // svc.Enroll();
    }
}
