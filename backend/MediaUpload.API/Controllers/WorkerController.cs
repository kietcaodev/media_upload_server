using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MediaUpload.Application.DTOs;
using MediaUpload.Application.Worker;
using MediaUpload.API.Hubs;
using MediaUpload.API.Middleware;

namespace MediaUpload.API.Controllers;

[ApiController]
[Route("api/worker")]
[RequirePermission("config")]
public class WorkerController(WorkerStateService workerState, IHubContext<JobHub> hub) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status() =>
        Ok(new WorkerStatusDto(workerState.IsPaused, workerState.PauseReason, workerState.ActiveCount));

    [HttpPost("pause")]
    public async Task<IActionResult> Pause([FromBody] PauseRequest req)
    {
        workerState.Pause(req.Reason ?? "Paused by admin");
        await hub.Clients.All.SendAsync("worker:status",
            new WorkerStatusDto(true, workerState.PauseReason, workerState.ActiveCount));
        return Ok(new WorkerStatusDto(true, workerState.PauseReason, workerState.ActiveCount));
    }

    [HttpPost("resume")]
    public async Task<IActionResult> Resume()
    {
        workerState.Resume();
        await hub.Clients.All.SendAsync("worker:status",
            new WorkerStatusDto(false, null, workerState.ActiveCount));
        return Ok(new WorkerStatusDto(false, null, workerState.ActiveCount));
    }
}

public record PauseRequest(string? Reason);
