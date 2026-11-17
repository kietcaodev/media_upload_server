namespace MediaUpload.Domain.Entities;

/// <summary>Một mốc trong vòng đời job (nhận file → NAS → đẩy ERP → kết quả), để UI hiện timeline.</summary>
public class JobEvent
{
    public long Id { get; set; }
    public Guid JobId { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    /// <summary>Một trong các hằng số của <see cref="JobEventSteps"/>.</summary>
    public string Step { get; set; } = string.Empty;
    public string? Detail { get; set; }
}

public static class JobEventSteps
{
    public const string Received          = "Received";          // file đã ghi xong vào staging
    public const string WaitingForWindow  = "WaitingForWindow";  // ngoài Time Window, giữ ở staging
    public const string MovedToNas        = "MovedToNas";
    public const string Processing        = "Processing";        // bắt đầu 1 lần gửi ERP
    public const string ErpSuccess        = "ErpSuccess";
    public const string ErpFailed         = "ErpFailed";
    public const string RetryScheduled    = "RetryScheduled";
    public const string GaveUp            = "GaveUp";            // hết lượt retry → Failed
    public const string Cancelled         = "Cancelled";
    public const string ManualRetry       = "ManualRetry";
    public const string ResetAfterRestart = "ResetAfterRestart";
}
