namespace MediaUpload.Domain.Enums;

public enum JobStatus
{
    Pending = 0,
    Processing = 1,
    Success = 2,
    Failed = 3,
    Cancelled = 4,
    Paused = 5
}
