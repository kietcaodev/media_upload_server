using MediaUpload.Domain.Interfaces;

namespace MediaUpload.Application.Services;

public class TimeWindowChecker(ITimeWindowConfigRepository repo, SettingsService settings)
{
    /// <summary>
    /// Returns true if current time falls within at least one enabled time window.
    /// Returns true if no windows are configured (no restriction).
    /// </summary>
    public async Task<bool> IsWithinWindowAsync()
    {
        var windows = await repo.GetAllAsync();
        if (windows.Count == 0) return true;

        var tz = await settings.GetTimezoneAsync();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);

        return windows.Any(w => w.IsActive(nowLocal));
    }
}
