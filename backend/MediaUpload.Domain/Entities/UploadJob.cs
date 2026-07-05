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

    // Legacy fields (ported from the old Node.js server.js contract) – used to
    // build the on-disk folder structure {company}/{ordCode}/{user}/{yyyy}/{mm}/{dd}/
    // and to report back the same shape external callers already integrate against.
    public string CompanyId { get; set; } = string.Empty;
    public string OrdCode { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? CustomFilename { get; set; }
    /// <summary>Path relative to the NAS root (nas.upload_dir), e.g. "DND/KHZ123/NVDND001/2026/07/05/video.mp4".
    /// Sent to ERP as list_video_path[] (prefixed by nas.video_path_prefix).</summary>
    public string RelativePath { get; set; } = string.Empty;

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
