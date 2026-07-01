namespace MediaUpload.API.Middleware;

/// <summary>
/// Thread-safe in-memory cache for settings that must be checked synchronously
/// on the hot request path (CORS origin check, rate limiting) without awaiting
/// the DB-backed SettingsService on every single request.
///
/// Populated at startup from the DB and refreshed immediately whenever an admin
/// updates the corresponding keys via SystemSettingsController (System Settings
/// tab in the UI) — so changes take effect without restarting the app.
/// </summary>
public static class RuntimeConfigCache
{
    private static string[] _corsAllowedOrigins = ["http://localhost:5173"];
    private static int _rateLimitWindowMs = 900_000;
    private static int _rateLimitMaxRequests = 20;

    public static int RateLimitWindowMs => _rateLimitWindowMs;
    public static int RateLimitMaxRequests => _rateLimitMaxRequests;

    public static bool IsCorsOriginAllowed(string origin) =>
        _corsAllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);

    public static void SetCorsAllowedOrigins(IEnumerable<string> origins)
    {
        var normalized = origins.Select(o => o.Trim()).Where(o => o.Length > 0).ToArray();
        if (normalized.Length > 0) _corsAllowedOrigins = normalized;
    }

    public static void SetRateLimit(int windowMs, int maxRequests)
    {
        if (windowMs > 0) _rateLimitWindowMs = windowMs;
        if (maxRequests > 0) _rateLimitMaxRequests = maxRequests;
    }
}
