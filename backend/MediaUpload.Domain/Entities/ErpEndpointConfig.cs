namespace MediaUpload.Domain.Entities;

public class ErpEndpointConfig
{
    public int Id { get; set; }
    public string Target { get; set; } = string.Empty; // DND | ZOMZEM | ZOZIN
    public string Url { get; set; } = string.Empty;

    /// <summary>AES-256 encrypted token. Decrypt via IEncryptionService.</summary>
    public string EncryptedToken { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
