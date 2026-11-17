using Microsoft.AspNetCore.SignalR;
using MediaUpload.Application.Worker;
using MediaUpload.Domain.Entities;

namespace MediaUpload.API.Hubs;

public class JobHub : Hub
{
    // Clients connect to receive real-time events.
    // Events pushed via IHubContext<JobHub>:
    //   job:created       { job }                                   – UploadController
    //   job:statusChanged { jobId, status, retryCount, lastError }  – JobHubNotifier (worker), JobsController
    //   worker:status     { isPaused, pauseReason, activeCount }    – WorkerController
}

public class JobHubNotifier(IHubContext<JobHub> hub, ILogger<JobHubNotifier> logger) : IJobNotifier
{
    public async Task JobStatusChangedAsync(UploadJob job)
    {
        // Lỗi SignalR không được làm hỏng việc xử lý job (job sẽ kẹt ở Processing).
        try
        {
            await hub.Clients.All.SendAsync("job:statusChanged", new
            {
                jobId = job.Id,
                status = job.Status.ToString(),
                retryCount = job.RetryCount,
                lastError = job.LastError,
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SignalR notify failed for job {Id}", job.Id);
        }
    }
}
