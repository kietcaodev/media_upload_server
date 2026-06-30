using MediaUpload.Domain.Interfaces;

namespace MediaUpload.Application.Services;

/// <summary>
/// Thread-safe in-memory settings cache. Backed by ISystemSettingRepository.
/// All values are read from DB; no hardcoded fallback in callers.
/// </summary>
public class SettingsService(ISystemSettingRepository repo)
{
    private readonly Dictionary<string, string> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<string> GetAsync(string key, string defaultValue = "")
    {
        await _lock.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out var cached)) return cached;
            var val = await repo.GetAsync(key) ?? defaultValue;
            _cache[key] = val;
            return val;
        }
        finally { _lock.Release(); }
    }

    public async Task<int> GetIntAsync(string key, int defaultValue = 0)
    {
        var s = await GetAsync(key, defaultValue.ToString());
        return int.TryParse(s, out var v) ? v : defaultValue;
    }

    public async Task<long> GetLongAsync(string key, long defaultValue = 0)
    {
        var s = await GetAsync(key, defaultValue.ToString());
        return long.TryParse(s, out var v) ? v : defaultValue;
    }

    public async Task<List<string>> GetListAsync(string key)
    {
        var s = await GetAsync(key);
        return string.IsNullOrWhiteSpace(s)
            ? []
            : [.. s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim())];
    }

    public async Task SetAsync(string key, string value)
    {
        // Persist first (no lock needed for DB call)
        await repo.SetAsync(key, value);
        // Update cache under lock
        await _lock.WaitAsync();
        try { _cache[key] = value; }
        finally { _lock.Release(); }
    }

    public void Invalidate(string key)
    {
        _lock.Wait();
        try { _cache.Remove(key); }
        finally { _lock.Release(); }
    }

    public void InvalidateAll()
    {
        _lock.Wait();
        try { _cache.Clear(); }
        finally { _lock.Release(); }
    }

    public async Task<TimeZoneInfo> GetTimezoneAsync()
    {
        var tzId = await GetAsync("system.timezone", "UTC");
        try { return TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch { return TimeZoneInfo.Utc; }
    }

    public async Task<DateTime> ToLocalTimeAsync(DateTime utc)
    {
        var tz = await GetTimezoneAsync();
        return TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
    }
}
