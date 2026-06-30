# Background Worker – Job Dispatch & TimeWindow Logic

> **Tech:** `IHostedService` + `Channel<T>` + `SemaphoreSlim` + `IHubContext<JobHub>`

---

## Tổng Quan

```
JobWorkerService (IHostedService)
│
├── Timer: tick mỗi 30 giây
│   ├── Gọi TimeWindowChecker.IsAllowed()
│   ├── Nếu trong giờ: lấy Pending jobs → đưa vào Channel
│   └── Nếu ngoài giờ: log + skip
│
├── Consumer Loop (chạy liên tục trên background thread)
│   ├── Đọc job từ Channel
│   ├── Acquire SemaphoreSlim (max concurrent)
│   ├── Gọi ErpPushService.PushAsync()
│   └── Release Semaphore sau khi xong
│
└── WorkerStateService (Singleton)
    ├── IsPaused: bool
    ├── PauseReason: string
    └── ActiveCount: int
```

---

## WorkerStateService

```csharp
// Application/Workers/WorkerStateService.cs
public class WorkerStateService
{
    private volatile bool _isPaused = false;
    private string? _pauseReason;
    private int _activeCount = 0;

    public bool IsPaused => _isPaused;
    public string? PauseReason => _pauseReason;
    public int ActiveCount => _activeCount;

    public void Pause(string? reason = null)
    {
        _isPaused = true;
        _pauseReason = reason;
    }

    public void Resume()
    {
        _isPaused = false;
        _pauseReason = null;
    }

    public void IncrementActive() => Interlocked.Increment(ref _activeCount);
    public void DecrementActive() => Interlocked.Decrement(ref _activeCount);
}
```

---

## TimeWindowChecker

```csharp
// Application/Workers/TimeWindowChecker.cs
public class TimeWindowChecker
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISettingsService _settings;

    public TimeWindowChecker(IServiceScopeFactory scopeFactory, ISettingsService settings)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
    }

    /// <summary>
    /// Kiểm tra thời điểm hiện tại có nằm trong khung giờ cho phép không.
    /// - Giờ lấy từ UTC → convert sang timezone cấu hình (mặc định Asia/Ho_Chi_Minh)
    /// - TimeWindowConfig.StartTime/EndTime lưu theo giờ địa phương (GMT+7)
    /// </summary>
    public async Task<(bool IsAllowed, int MaxConcurrent, string? WindowName)>
        CheckAsync(string? erpTarget = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Đọc timezone từ settings — không hardcode
        var tzId  = await _settings.GetStringAsync("system.timezone", "Asia/Ho_Chi_Minh");
        var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(tzId);

        // Luôn bắt đầu từ UTC, explicit convert sang local
        var nowUtc   = DateTime.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tzInfo);

        var configs = await db.TimeWindowConfigs
            .Where(x => x.IsEnabled && (x.ErpTarget == null || x.ErpTarget == erpTarget))
            .ToListAsync();

        foreach (var config in configs)
        {
            if (config.IsActive(nowLocal))  // IsActive nhận giờ local đã convert
                return (true, config.MaxConcurrent, config.Name);
        }

        return (false, 0, null);
    }
}
```

---

## ErpPushService

```csharp
// Application/Workers/ErpPushService.cs
public class ErpPushService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<JobHub> _hubContext;
    private readonly ILogger<ErpPushService> _logger;

    public async Task<bool> PushAsync(UploadJob job, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Lấy config ERP
        var erpConfig = await db.ErpEndpointConfigs
            .FirstOrDefaultAsync(x => x.Name == job.ErpTarget && x.IsEnabled, ct);

        if (erpConfig is null)
        {
            await FailJobAsync(db, job, "ERP config không tồn tại hoặc bị tắt", ct);
            return false;
        }

        // Cập nhật status Running
        job.Status = JobStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await BroadcastStatusAsync(job);

        try
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(erpConfig.TimeoutSeconds)
            };
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", DecryptToken(erpConfig.Token));

            using var form = new MultipartFormDataContent();
            var fileBytes = await File.ReadAllBytesAsync(job.SavedPath, ct);
            form.Add(new ByteArrayContent(fileBytes), "file", job.OriginalName);
            form.Add(new StringContent(job.CustomerCode), "customerCode");
            form.Add(new StringContent(job.UserId), "userId");

            var response = await httpClient.PostAsync(erpConfig.Url, form, ct);

            if (response.IsSuccessStatusCode)
            {
                job.Status = JobStatus.Done;
                job.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                await BroadcastStatusAsync(job);
                _logger.LogInformation("Job {JobId} pushed to {ERP} successfully", job.Id, job.ErpTarget);
                return true;
            }
            else
            {
                var error = $"HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}";
                return await HandleRetryAsync(db, job, error, ct);
            }
        }
        catch (Exception ex)
        {
            return await HandleRetryAsync(db, job, ex.Message, ct);
        }
    }

    private async Task<bool> HandleRetryAsync(AppDbContext db, UploadJob job, string error, CancellationToken ct)
    {
        job.RetryCount++;
        _logger.LogWarning("Job {JobId} failed (retry {Count}/{Max}): {Error}",
            job.Id, job.RetryCount, job.MaxRetry, error);

        if (job.RetryCount >= job.MaxRetry)
        {
            await FailJobAsync(db, job, error, ct);
            return false;
        }

        // Delay đọc từ settings — không hardcode
        var delayMin = await _settings.GetIntAsync("worker.retry_delay_minutes", 5);
        job.Status = JobStatus.Pending;
        job.ScheduledAt = DateTime.UtcNow.AddMinutes(delayMin * job.RetryCount);
        await db.SaveChangesAsync(ct);
        await BroadcastStatusAsync(job);
        return false;
    }

    private async Task FailJobAsync(AppDbContext db, UploadJob job, string error, CancellationToken ct)
    {
        job.Status = JobStatus.Failed;
        job.ErrorMessage = error;
        job.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await BroadcastStatusAsync(job);
    }

    private async Task BroadcastStatusAsync(UploadJob job)
    {
        await _hubContext.Clients.All.SendAsync("job:statusChanged", new
        {
            jobId = job.Id,
            status = job.Status.ToString(),
            retryCount = job.RetryCount,
            errorMessage = job.ErrorMessage
        });
    }

    private string DecryptToken(string encryptedToken)
    {
        // AES-256 decrypt – key lấy từ appsettings
        // Implementation trong Infrastructure/Security/AesEncryptor.cs
        return AesEncryptor.Decrypt(encryptedToken);
    }
}
```

---

## JobWorkerService (IHostedService)

```csharp
// Application/Workers/JobWorkerService.cs
public class JobWorkerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkerStateService _workerState;
    private readonly TimeWindowChecker _timeWindowChecker;
    private readonly ErpPushService _erpPushService;
    private readonly IHubContext<JobHub> _hubContext;
    private readonly ILogger<JobWorkerService> _logger;

    // Channel: buffer tối đa 100 jobs
    private readonly Channel<UploadJob> _jobChannel =
        Channel.CreateBounded<UploadJob>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Chạy song song: producer (timer) + consumer (worker)
        await Task.WhenAll(
            RunProducerAsync(stoppingToken),
            RunConsumerAsync(stoppingToken)
        );
    }

    // Producer: kiểm tra TimeWindow và đưa jobs vào Channel
    // Interval đọc động từ settings (không dùng PeriodicTimer cố định)
    private async Task RunProducerAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Đọc interval từ DB mỗi lần — hoạt động hot-reload
            var intervalSec = await _settings.GetIntAsync("worker.tick_interval_seconds", 30);
            await Task.Delay(TimeSpan.FromSeconds(intervalSec), ct);

            if (_workerState.IsPaused)
            {
                _logger.LogDebug("Worker paused: {Reason}", _workerState.PauseReason);
                continue;
            }

            var (isAllowed, maxConcurrent, windowName) = await _timeWindowChecker.CheckAsync();

            if (!isAllowed)
            {
                _logger.LogDebug("Outside time window, skipping dispatch");
                continue;
            }

            _logger.LogInformation("In window '{Window}', dispatching jobs (max: {Max})",
                windowName, maxConcurrent);

            await DispatchPendingJobsAsync(maxConcurrent, ct);
        }
    }

    private async Task DispatchPendingJobsAsync(int maxConcurrent, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var available = maxConcurrent - _workerState.ActiveCount;
        if (available <= 0) return;

        var jobs = await db.UploadJobs
            .Where(j => j.Status == JobStatus.Pending &&
                        (j.ScheduledAt == null || j.ScheduledAt <= DateTime.UtcNow))
            .OrderBy(j => j.CreatedAt)
            .Take(available)
            .ToListAsync(ct);

        foreach (var job in jobs)
            await _jobChannel.Writer.WriteAsync(job, ct);
    }

    // Consumer: xử lý jobs từ Channel
    private async Task RunConsumerAsync(CancellationToken ct)
    {
        await foreach (var job in _jobChannel.Reader.ReadAllAsync(ct))
        {
            _workerState.IncrementActive();

            // Fire-and-forget với tracking
            _ = Task.Run(async () =>
            {
                try
                {
                    await _erpPushService.PushAsync(job, ct);
                }
                finally
                {
                    _workerState.DecrementActive();

                    // Broadcast worker stats
                    await _hubContext.Clients.All.SendAsync("worker:statusChanged", new
                    {
                        isPaused = _workerState.IsPaused,
                        activeCount = _workerState.ActiveCount
                    }, ct);
                }
            }, ct);
        }
    }
}
```

---

## Các Trường Hợp Đặc Biệt

### Khi Worker Khởi Động Lại
- Jobs ở trạng thái `Running` khi server restart → tự động reset về `Pending`
- Thực hiện trong `Program.cs` khi startup:

```csharp
// Reset stuck Running jobs on startup
using var startScope = app.Services.CreateScope();
var db = startScope.ServiceProvider.GetRequiredService<AppDbContext>();
var stuckJobs = await db.UploadJobs
    .Where(j => j.Status == JobStatus.Running)
    .ToListAsync();
stuckJobs.ForEach(j => j.Status = JobStatus.Pending);
await db.SaveChangesAsync();
```

### Pause/Resume Thủ Công
```csharp
// WorkerController.cs
[HttpPost("pause")]
public IActionResult Pause([FromBody] PauseRequest req)
{
    _workerState.Pause(req.Reason);
    _hubContext.Clients.All.SendAsync("worker:statusChanged", new
    {
        isPaused = true,
        reason = req.Reason,
        activeCount = _workerState.ActiveCount
    });
    return Ok(new { success = true });
}

[HttpPost("resume")]
public IActionResult Resume()
{
    _workerState.Resume();
    _hubContext.Clients.All.SendAsync("worker:statusChanged", new
    {
        isPaused = false,
        activeCount = _workerState.ActiveCount
    });
    return Ok(new { success = true });
}
```

### Retry Strategy
| Lần retry | Delay chờ |
|-----------|-----------|
| Lần 1 | 5 phút |
| Lần 2 | 10 phút |
| Lần 3 | 15 phút |
| > 3 lần | → `Failed` |
