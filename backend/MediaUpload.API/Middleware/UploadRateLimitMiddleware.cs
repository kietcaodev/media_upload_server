using System.Collections.Concurrent;

namespace MediaUpload.API.Middleware;

/// <summary>
/// Fixed-window rate limiter for the upload endpoint, driven by
/// ratelimit.window_ms / ratelimit.max_requests (editable via the System
/// Settings UI, see RuntimeConfigCache). Mirrors the behaviour of the legacy
/// express-rate-limit middleware from the old Node.js prototype.
/// </summary>
public class UploadRateLimitMiddleware(RequestDelegate next)
{
    private sealed class Window
    {
        public DateTime ResetAtUtc;
        public int Count;
        public readonly object Lock = new();
    }

    private static readonly ConcurrentDictionary<string, Window> _windows = new();

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (HttpMethods.IsPost(ctx.Request.Method) && ctx.Request.Path.StartsWithSegments("/api/upload"))
        {
            var key = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var windowMs = RuntimeConfigCache.RateLimitWindowMs;
            var maxRequests = RuntimeConfigCache.RateLimitMaxRequests;
            var now = DateTime.UtcNow;

            var window = _windows.GetOrAdd(key, _ => new Window { ResetAtUtc = now.AddMilliseconds(windowMs) });

            bool limited;
            lock (window.Lock)
            {
                if (now >= window.ResetAtUtc)
                {
                    window.ResetAtUtc = now.AddMilliseconds(windowMs);
                    window.Count = 0;
                }
                window.Count++;
                limited = window.Count > maxRequests;
            }

            if (limited)
            {
                ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await ctx.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    code = "RATE_LIMIT",
                    message = "Quá nhiều request. Vui lòng thử lại sau."
                });
                return;
            }
        }

        await next(ctx);
    }
}
