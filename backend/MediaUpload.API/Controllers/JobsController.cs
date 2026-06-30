using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MediaUpload.Application.DTOs;
using MediaUpload.Domain.Enums;
using MediaUpload.Domain.Entities;
using MediaUpload.Domain.Interfaces;
using MediaUpload.API.Hubs;
using MediaUpload.API.Middleware;

namespace MediaUpload.API.Controllers;

[ApiController]
[Route("api/jobs")]
[RequirePermission("read_jobs")]
public class JobsController(
    IUploadJobRepository jobRepo,
    IHubContext<JobHub> hub) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetJobs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        JobStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<JobStatus>(status, true, out var s))
            statusFilter = s;

        var items = await jobRepo.GetJobsAsync(page, pageSize, statusFilter);
        var total = await jobRepo.CountAsync(statusFilter);
        return Ok(new JobListResponse(items.Select(MapJob).ToList(), total, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetJob(Guid id)
    {
        var job = await jobRepo.GetByIdAsync(id);
        return job == null ? NotFound() : Ok(MapJob(job));
    }

    [HttpPatch("{id:guid}/cancel")]
    [RequirePermission("config")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var job = await jobRepo.GetByIdAsync(id);
        if (job == null) return NotFound();
        if (job.Status is JobStatus.Success or JobStatus.Failed or JobStatus.Cancelled)
            return BadRequest(new { error = "Cannot cancel job in terminal state" });

        job.Status = JobStatus.Cancelled;
        await jobRepo.UpdateAsync(job);
        await hub.Clients.All.SendAsync("job:statusChanged", new { jobId = id, status = "Cancelled" });
        return Ok(MapJob(job));
    }

    [HttpPatch("{id:guid}/retry")]
    [RequirePermission("config")]
    public async Task<IActionResult> Retry(Guid id)
    {
        var job = await jobRepo.GetByIdAsync(id);
        if (job == null) return NotFound();
        if (job.Status != JobStatus.Failed)
            return BadRequest(new { error = "Only failed jobs can be retried" });

        job.Status = JobStatus.Pending;
        job.RetryCount = 0;
        job.ScheduledAtUtc = null;
        job.LastError = null;
        await jobRepo.UpdateAsync(job);
        await hub.Clients.All.SendAsync("job:statusChanged", new { jobId = id, status = "Pending" });
        return Ok(MapJob(job));
    }

    private static JobDto MapJob(UploadJob j) => new(
        j.Id, j.FileId, j.OriginalFileName, j.FileSize, j.ErpTarget,
        j.Status.ToString(), j.RetryCount, j.MaxRetry, j.LastError,
        j.CreatedAtUtc.ToString("O"),
        j.ProcessedAtUtc?.ToString("O"),
        j.CompletedAtUtc?.ToString("O"));
}
