using Microsoft.EntityFrameworkCore;
using MediaUpload.Domain.Entities;
using MediaUpload.Domain.Interfaces;
using MediaUpload.Infrastructure.Persistence;

namespace MediaUpload.Infrastructure.Repositories;

public class UploadJobRepository(AppDbContext db) : IUploadJobRepository
{
    public Task<UploadJob?> GetByIdAsync(Guid id) =>
        db.UploadJobs.FirstOrDefaultAsync(x => x.Id == id);

    public Task<List<UploadJob>> GetPendingJobsAsync(int limit = 50) =>
        db.UploadJobs
          .Where(x => x.Status == Domain.Enums.JobStatus.Pending
                   && (x.ScheduledAtUtc == null || x.ScheduledAtUtc <= DateTime.UtcNow))
          .OrderBy(x => x.CreatedAtUtc)
          .Take(limit)
          .ToListAsync();

    public Task<List<UploadJob>> GetJobsAsync(int page, int pageSize, Domain.Enums.JobStatus? status)
    {
        var q = db.UploadJobs.AsQueryable();
        if (status.HasValue) q = q.Where(x => x.Status == status.Value);
        return q.OrderByDescending(x => x.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
    }

    public Task<int> CountAsync(Domain.Enums.JobStatus? status = null)
    {
        var q = db.UploadJobs.AsQueryable();
        if (status.HasValue) q = q.Where(x => x.Status == status.Value);
        return q.CountAsync();
    }

    public async Task AddAsync(UploadJob job)
    {
        db.UploadJobs.Add(job);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(UploadJob job)
    {
        db.UploadJobs.Update(job);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var job = await db.UploadJobs.FindAsync(id);
        if (job != null) db.UploadJobs.Remove(job);
        await db.SaveChangesAsync();
    }

    public async Task ResetStuckJobsAsync()
    {
        var stuck = await db.UploadJobs
            .Where(x => x.Status == Domain.Enums.JobStatus.Processing)
            .ToListAsync();
        foreach (var j in stuck)
        {
            j.Status = Domain.Enums.JobStatus.Pending;
            j.LastError = "Reset after restart";
        }
        if (stuck.Count > 0) await db.SaveChangesAsync();
    }
}

public class TimeWindowConfigRepository(AppDbContext db) : ITimeWindowConfigRepository
{
    public Task<List<TimeWindowConfig>> GetAllAsync() =>
        db.TimeWindowConfigs.OrderBy(x => x.StartTime).ToListAsync();

    public Task<TimeWindowConfig?> GetByIdAsync(int id) =>
        db.TimeWindowConfigs.FirstOrDefaultAsync(x => x.Id == id);

    public async Task AddAsync(TimeWindowConfig config)
    {
        db.TimeWindowConfigs.Add(config);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(TimeWindowConfig config)
    {
        db.TimeWindowConfigs.Update(config);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var c = await db.TimeWindowConfigs.FindAsync(id);
        if (c != null) db.TimeWindowConfigs.Remove(c);
        await db.SaveChangesAsync();
    }
}

public class ErpEndpointConfigRepository(AppDbContext db) : IErpEndpointConfigRepository
{
    public Task<List<ErpEndpointConfig>> GetAllAsync() =>
        db.ErpEndpointConfigs.ToListAsync();

    public Task<ErpEndpointConfig?> GetByTargetAsync(string target) =>
        db.ErpEndpointConfigs.FirstOrDefaultAsync(x => x.Target == target);

    public async Task UpsertAsync(ErpEndpointConfig config)
    {
        var existing = await db.ErpEndpointConfigs.FirstOrDefaultAsync(x => x.Target == config.Target);
        if (existing == null)
            db.ErpEndpointConfigs.Add(config);
        else
        {
            existing.Url = config.Url;
            existing.EncryptedToken = config.EncryptedToken;
            existing.Enabled = config.Enabled;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }
}

public class ApiCredentialRepository(AppDbContext db) : IApiCredentialRepository
{
    public Task<List<ApiCredential>> GetAllAsync() =>
        db.ApiCredentials.OrderBy(x => x.Name).ToListAsync();

    public Task<ApiCredential?> GetByIdAsync(int id) =>
        db.ApiCredentials.FirstOrDefaultAsync(x => x.Id == id);

    public Task<List<ApiCredential>> GetByAuthTypeAsync(Domain.Enums.AuthType authType) =>
        db.ApiCredentials.Where(x => x.AuthType == authType && x.Enabled).ToListAsync();

    public async Task AddAsync(ApiCredential credential)
    {
        db.ApiCredentials.Add(credential);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(ApiCredential credential)
    {
        db.ApiCredentials.Update(credential);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var c = await db.ApiCredentials.FindAsync(id);
        if (c != null) db.ApiCredentials.Remove(c);
        await db.SaveChangesAsync();
    }
}

public class SystemSettingRepository(AppDbContext db) : ISystemSettingRepository
{
    private static readonly Dictionary<string, string> _defaults = new()
    {
        ["nas.upload_dir"]               = "/opt/media-upload/uploads",
        ["nas.local_staging_dir"]        = "/opt/media-upload/staging",
        ["nas.logs_dir"]                 = "/opt/media-upload/logs",
        ["nas.min_free_space_bytes"]     = "1073741824",
        ["nas.video_path_prefix"]        = "/homes/video/uploads/",
        ["upload.max_file_size_bytes"]   = "1572864000",
        ["upload.max_files_per_request"] = "5",
        ["upload.allowed_extensions"]    = ".mp4,.avi,.mov,.mkv,.flv,.wmv,.webm,.3gp,.mp3,.wav,.ogg,.aac,.flac,.m4a,.wma,.jpg,.jpeg,.png,.gif,.bmp,.webp,.tiff,.tif,.ico,.heic,.heif",
        ["worker.tick_interval_seconds"] = "30",
        ["worker.max_retry"]             = "3",
        ["worker.retry_delay_minutes"]   = "5",
        ["worker.max_concurrent"]        = "3",
        ["ratelimit.window_ms"]          = "900000",
        ["ratelimit.max_requests"]       = "20",
        ["cors.allowed_origins"]         = "https://103.104.123.126:8443,http://localhost:5173",
        ["system.timezone"]              = "Asia/Ho_Chi_Minh",
    };

    public async Task<string?> GetAsync(string key)
    {
        var s = await db.SystemSettings.FirstOrDefaultAsync(x => x.Key == key);
        return s?.Value;
    }

    public async Task SetAsync(string key, string value)
    {
        var s = await db.SystemSettings.FirstOrDefaultAsync(x => x.Key == key);
        if (s == null)
        {
            db.SystemSettings.Add(new SystemSetting { Key = key, Value = value });
        }
        else
        {
            s.Value = value;
            s.UpdatedAtUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    public Task<List<SystemSetting>> GetAllAsync() =>
        db.SystemSettings.OrderBy(x => x.Key).ToListAsync();

    public async Task ResetToDefaultAsync(string key)
    {
        if (_defaults.TryGetValue(key, out var def))
            await SetAsync(key, def);
    }
}
