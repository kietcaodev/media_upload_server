namespace MediaUpload.Domain.Entities;

public class TimeWindowConfig
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Stored as HH:mm in GMT+7 display timezone</summary>
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    /// <summary>Comma-separated day-of-week numbers: 1=Mon ... 7=Sun</summary>
    public string DaysOfWeek { get; set; } = "1,2,3,4,5";

    public bool Enabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <param name="nowLocal">Current time already converted to local timezone</param>
    public bool IsActive(DateTime nowLocal)
    {
        if (!Enabled) return false;
        var days = DaysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(int.Parse).ToHashSet();
        int dow = (int)nowLocal.DayOfWeek == 0 ? 7 : (int)nowLocal.DayOfWeek; // 1=Mon..7=Sun
        if (!days.Contains(dow)) return false;
        var nowTime = TimeOnly.FromDateTime(nowLocal);
        return StartTime <= EndTime
            ? nowTime >= StartTime && nowTime <= EndTime
            : nowTime >= StartTime || nowTime <= EndTime; // overnight window
    }
}
