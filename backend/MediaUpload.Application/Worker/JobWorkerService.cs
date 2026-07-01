using System.Threading.Channels;
using MediaUpload.Domain.Entities;
using MediaUpload.Domain.Enums;
using MediaUpload.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MediaUpload.Application.Services;

namespace MediaUpload.Application.Worker;

/// <summary>Singleton: tracks worker pause/resume state and active job count.</summary>
public class WorkerStateService
{
    private volatile bool _isPaused;
    private volatile string _pauseReason = string.Empty;
    private int _activeCount;

    public bool IsPaused => _isPaused;
    public string PauseReason => _pauseReason;
    public int ActiveCount => _activeCount;

    public void Pause(string reason = "")
    {
        _pauseReason = reason;   // write reason before setting flag
        _isPaused = true;
    }

    public void Resume()
    {
        _isPaused = false;
        _pauseReason = string.Empty;
    }

    public void IncrementActive() => Interlocked.Increment(ref _activeCount);
    public void DecrementActive() => Interlocked.Decrement(ref _activeCount);
}

/// <summary>
/// Background hosted service.
/// Producer: reads pending jobs from DB → Channel.
/// Consumer pool: processes jobs via ErpPushService.
/// All config values read dynamically from SettingsService (no hardcoded intervals).
/// </summary>
public class JobWorkerService(
    IServiceScopeFactory scopeFactory,
    WorkerStateService workerState,
    ILogger<JobWorkerService> logger)
    : BackgroundService
{
    private readonly Channel<UploadJob> _channel = Channel.CreateBounded<UploadJob>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait });

    // Semaphore controls max concurrent jobs (refreshed each tick from settings)
    private SemaphoreSlim _concurrencySem = new(3, 3);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("JobWorker started.");
        await Task.WhenAll(ProducerLoop(ct), ConsumerLoop(ct));
    }

    private async Task ProducerLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!workerState.IsPaused)
                {
                    using var scope = scopeFactory.CreateScope();
                    var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
                    var timeWindow = scope.ServiceProvider.GetRequiredService<TimeWindowChecker>();
                    var jobRepo = scope.ServiceProvider.GetRequiredService<IUploadJobRepository>();
                    var fileStaging = scope.ServiceProvider.GetRequiredService<FileStagingService>();

                    // Refresh semaphore capacity if max_concurrent changed
                    var maxConcurrent = await settings.GetIntAsync("worker.max_concurrent", 3);
                    if (_concurrencySem.CurrentCount == 0 && workerState.ActiveCount == 0)
                        _concurrencySem = new SemaphoreSlim(maxConcurrent, maxConcurrent);

                    if (await timeWindow.IsWithinWindowAsync())
                    {
                        var jobs = await jobRepo.GetPendingJobsAsync(20);
                        foreach (var job in jobs)
                        {
                            if (ct.IsCancellationRequested) break;

                            // Files uploaded outside the window are still sitting in
                            // local staging – promote them onto NAS now that we're
                            // inside a window, before the job gets pushed to ERP.
                            var promotedPath = await fileStaging.PromoteToNasIfWithinWindowAsync(job.SavedPath);
                            if (promotedPath != job.SavedPath)
                            {
                                job.SavedPath = promotedPath;
                                await jobRepo.UpdateAsync(job);
                            }

                            await _channel.Writer.WriteAsync(job, ct);
                        }
                    }

                    var intervalSec = await settings.GetIntAsync("worker.tick_interval_seconds", 30);
                    await Task.Delay(TimeSpan.FromSeconds(intervalSec), ct);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Producer loop error");
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
        }
        _channel.Writer.Complete();
    }

    private async Task ConsumerLoop(CancellationToken ct)
    {
        while (await _channel.Reader.WaitToReadAsync(ct))
        {
            while (_channel.Reader.TryRead(out var job))
            {
                if (ct.IsCancellationRequested) break;
                await _concurrencySem.WaitAsync(ct);
                _ = Task.Run(async () =>
                {
                    try { await ProcessJobAsync(job, ct); }
                    finally { _concurrencySem.Release(); }
                }, ct);
            }
        }
    }

    private async Task ProcessJobAsync(UploadJob job, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<IUploadJobRepository>();
        var erpPush = scope.ServiceProvider.GetRequiredService<ErpPushService>();
        var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();

        // Re-fetch to avoid stale data
        var fresh = await jobRepo.GetByIdAsync(job.Id);
        if (fresh == null || fresh.Status != JobStatus.Pending) return;

        workerState.IncrementActive();
        try
        {
            fresh.Status = JobStatus.Processing;
            fresh.ProcessedAtUtc = DateTime.UtcNow;
            await jobRepo.UpdateAsync(fresh);

            var (success, error) = await erpPush.PushAsync(fresh);

            if (success)
            {
                fresh.Status = JobStatus.Success;
                fresh.CompletedAtUtc = DateTime.UtcNow;
                fresh.LastError = null;
            }
            else
            {
                fresh.RetryCount++;
                fresh.LastError = error;
                var maxRetry = await settings.GetIntAsync("worker.max_retry", 3);
                var retryDelayMin = await settings.GetIntAsync("worker.retry_delay_minutes", 5);

                if (fresh.RetryCount >= maxRetry)
                {
                    fresh.Status = JobStatus.Failed;
                    fresh.CompletedAtUtc = DateTime.UtcNow;
                }
                else
                {
                    fresh.Status = JobStatus.Pending;
                    fresh.ScheduledAtUtc = DateTime.UtcNow.AddMinutes(retryDelayMin * fresh.RetryCount);
                }
            }

            await jobRepo.UpdateAsync(fresh);
            logger.LogInformation("Job {Id} → {Status}", fresh.Id, fresh.Status);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error processing job {Id}", job.Id);
        }
        finally
        {
            workerState.DecrementActive();
        }
    }
}
