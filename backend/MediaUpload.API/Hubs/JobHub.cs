using Microsoft.AspNetCore.SignalR;

namespace MediaUpload.API.Hubs;

public class JobHub : Hub
{
    // Clients connect to receive real-time events.
    // Events pushed from services via IHubContext<JobHub>:
    //   job:created       { job }
    //   job:statusChanged { jobId, status, retryCount, lastError }
    //   worker:status     { isPaused, pauseReason, activeCount }
    //   stats:updated     { stats }
}
