namespace MediaUpload.Domain.Entities;

public class UploadJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileId { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string SavedPath { get; set; } = string.Empty;
    public long FileSize { get; set; }

    // ERP target
    public string ErpTarget { get; set; } = string.Empty; // DND | ZOMZEM | ZOZIN
    public string? Longitude { get; set; }
    public string? Latitude { get; set; }
    public string? FlowId { get; set; }
    public string? OrderId { get; set; }
    public string? NvktId { get; set; }

    // Status
    public Domain.Enums.JobStatus Status { get; set; } = Domain.Enums.JobStatus.Pending;
    public int RetryCount { get; set; }
    public int MaxRetry { get; set; } = 3;
    public string? LastError { get; set; }

    // Timestamps (always UTC)
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ScheduledAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
