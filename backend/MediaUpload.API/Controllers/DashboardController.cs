using Microsoft.AspNetCore.Mvc;
using MediaUpload.Application.DTOs;
using MediaUpload.Application.Services;
using MediaUpload.Application.Worker;
using MediaUpload.Domain.Enums;
using MediaUpload.Domain.Interfaces;
using MediaUpload.API.Middleware;
using Microsoft.EntityFrameworkCore;
using MediaUpload.Infrastructure.Persistence;

namespace MediaUpload.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[RequirePermission("read_jobs")]
public class DashboardController(
    IUploadJobRepository jobRepo,
    WorkerStateService workerState,
    TimeWindowChecker timeWindow,
    SettingsService settings,
    AppDbContext db) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var stats = new DashboardStats(
            TotalJobs:       await jobRepo.CountAsync(),
            PendingJobs:     await jobRepo.CountAsync(JobStatus.Pending),
            ProcessingJobs:  await jobRepo.CountAsync(JobStatus.Processing),
            SuccessJobs:     await jobRepo.CountAsync(JobStatus.Success),
            FailedJobs:      await jobRepo.CountAsync(JobStatus.Failed),
            CancelledJobs:   await jobRepo.CountAsync(JobStatus.Cancelled),
            WorkerPaused:    workerState.IsPaused,
            WorkerPauseReason: workerState.PauseReason,
            ActiveWorkers:   workerState.ActiveCount,
            WithinTimeWindow: await timeWindow.IsWithinWindowAsync()
        );
        return Ok(stats);
    }

    [HttpGet("timeline")]
    public async Task<IActionResult> Timeline([FromQuery] int days = 7)
    {
        if (days < 1 || days > 90) days = 7;
        var tz = await settings.GetTimezoneAsync();
        var from = DateTime.UtcNow.AddDays(-days);

        // Group directly in DB, not in memory
        var groups = await db.UploadJobs
            .Where(j => j.CreatedAtUtc >= from)
            .GroupBy(j => j.CreatedAtUtc.Date)
            .Select(g => new
            {
                Date    = g.Key,
                Success = g.Count(j => j.Status == JobStatus.Success),
                Failed  = g.Count(j => j.Status == JobStatus.Failed),
                Pending = g.Count(j => j.Status == JobStatus.Pending),
            })
            .OrderBy(g => g.Date)
            .ToListAsync();

        var result = groups.Select(g => new DashboardTimelineItem(
            Date:    TimeZoneInfo.ConvertTimeFromUtc(g.Date, tz).ToString("yyyy-MM-dd"),
            Success: g.Success,
            Failed:  g.Failed,
            Pending: g.Pending
        )).ToList();

        return Ok(result);
    }
}
