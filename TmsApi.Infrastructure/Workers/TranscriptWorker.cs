using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TmsApi.Api.Hubs;
using TmsApi.Application.Hubs;
using TmsApi.Application.Transcripts;
using TmsApi.Infrastructure.Transcripts;

namespace TmsApi.Infrastructure.Workers;

public class TranscriptWorker(
    Channel<TranscriptRequest> channel,
    IServiceScopeFactory scopeactory,
    ITranscriptStatusStore statusStore,
    IHubContext<TmsHub, ITmsHubClient> hubContext,
    ILogger<TranscriptWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("Transcript worker started.");

        await foreach (var request in channel.Reader.ReadAllAsync(ct))
        {
            var reportId = request.ReportId!;
            try
            {
                await statusStore.MarkProcessingAsync(reportId, ct);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);

                var downloadUrl = $"/api/v2/transcripts/{reportId}/download";
                await statusStore.MarkReadyAsync(reportId, downloadUrl, ct);

                await hubContext
                    .Clients.Group(GroupNames.Student(request.StudentId.ToString()))
                    .ReceiveTranscriptReady(reportId, downloadUrl);

                logger.LogInformation(
                    "Transcript ready, notification sent: {ReportId} to {Group}",
                    reportId,
                    GroupNames.Student(request.StudentId.ToString())
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Transcript generation failed: {ReportId}", reportId);
                await statusStore.MarkFailedAsync(reportId, ex.Message, CancellationToken.None);
            }
        }
    }
}
