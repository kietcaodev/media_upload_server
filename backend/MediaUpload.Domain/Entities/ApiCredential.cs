using MediaUpload.Domain.Enums;

namespace MediaUpload.Domain.Entities;

public class ApiCredential
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public AuthType AuthType { get; set; }

    /// <summary>BCrypt hash of token/password. Raw value shown ONLY at creation.</summary>
    public string HashedSecret { get; set; } = string.Empty;

    /// <summary>First 8 chars of raw token for display (set at creation, never updated).</summary>
    public string TokenPrefix { get; set; } = string.Empty;

    // Basic auth only
    public string? Username { get; set; }

    // Permissions
    public bool CanUpload { get; set; }
    public bool CanReadJobs { get; set; }
    public bool CanConfig { get; set; }

    /// <summary>Comma-separated allowed ERP targets, empty = all</summary>
    public string AllowedErp { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAtUtc { get; set; }
}
