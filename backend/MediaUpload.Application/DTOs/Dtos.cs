using MediaUpload.Domain.Enums;

namespace MediaUpload.Application.DTOs;

// ── Upload ──────────────────────────────────────────────
public record UploadRequest(
    string ErpTarget,
    string? Longitude,
    string? Latitude,
    string? FlowId,
    string? OrderId,
    string? NvktId
);

public record UploadResponse(
    Guid JobId,
    string FileId,
    string FileName,
    long FileSize,
    string Status,
    string CreatedAtUtc
);

// ── Jobs ─────────────────────────────────────────────────
public record JobDto(
    Guid Id,
    string FileId,
    string OriginalFileName,
    long FileSize,
    string ErpTarget,
    string Status,
    int RetryCount,
    int MaxRetry,
    string? LastError,
    string CreatedAtUtc,
    string? ProcessedAtUtc,
    string? CompletedAtUtc
);

public record JobListResponse(List<JobDto> Items, int Total, int Page, int PageSize);

// ── TimeWindow ───────────────────────────────────────────
public record TimeWindowDto(
    int Id,
    string Name,
    string StartTime,   // HH:mm
    string EndTime,     // HH:mm
    string DaysOfWeek,  // "1,2,3,4,5"
    bool Enabled
);

public record TimeWindowRequest(
    string Name,
    string StartTime,
    string EndTime,
    string DaysOfWeek,
    bool Enabled
);

// ── ERP Endpoint ─────────────────────────────────────────
public record ErpEndpointDto(
    int Id,
    string Target,
    string Url,
    string TokenPrefix, // first 8 chars only – never full token
    bool Enabled
);

public record ErpEndpointRequest(
    string Target,
    string Url,
    string Token, // raw, will be encrypted server-side
    bool Enabled
);

// ── Credentials ──────────────────────────────────────────
public record CredentialDto(
    int Id,
    string Name,
    string AuthType,
    string TokenPrefix,
    string? Username,
    bool CanUpload,
    bool CanReadJobs,
    bool CanConfig,
    string AllowedErp,
    bool Enabled,
    string CreatedAtUtc,
    string? LastUsedAtUtc
);

public record CreateCredentialRequest(
    string Name,
    AuthType AuthType,
    string? Username,   // for Basic auth
    bool CanUpload,
    bool CanReadJobs,
    bool CanConfig,
    string AllowedErp
);

public record CreateCredentialResponse(
    int Id,
    string Name,
    string AuthType,
    string RawToken,    // ONLY returned once at creation
    string? Username,
    string TokenPrefix
);

// ── System Settings ──────────────────────────────────────
public record SettingDto(
    string Key,
    string Value,
    string? Description,
    bool HotReload,
    string UpdatedAtUtc
);

public record PatchSettingsRequest(Dictionary<string, string> Updates);

// ── Dashboard ────────────────────────────────────────────
public record DashboardStats(
    int TotalJobs,
    int PendingJobs,
    int ProcessingJobs,
    int SuccessJobs,
    int FailedJobs,
    int CancelledJobs,
    bool WorkerPaused,
    string? WorkerPauseReason,
    int ActiveWorkers,
    bool WithinTimeWindow
);

public record DashboardTimelineItem(
    string Date,        // yyyy-MM-dd in local time
    int Success,
    int Failed,
    int Pending
);

// ── Worker ───────────────────────────────────────────────
public record WorkerStatusDto(
    bool IsPaused,
    string? PauseReason,
    int ActiveCount
);
