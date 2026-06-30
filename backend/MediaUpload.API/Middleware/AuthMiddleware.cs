using System.Text;
using MediaUpload.Domain.Enums;
using MediaUpload.Domain.Interfaces;

namespace MediaUpload.API.Middleware;

/// <summary>
/// Detects auth type from request headers (Bearer / Basic / X-Api-Key),
/// verifies BCrypt hash against stored credentials, and sets HttpContext items.
/// Dev localhost bypass is controlled by environment only.
/// </summary>
public class AuthMiddleware(RequestDelegate next, ILogger<AuthMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        // Skip auth for SignalR negotiate and health
        var path = ctx.Request.Path.Value ?? "";
        if (path.StartsWith("/hubs/") || path == "/health")
        {
            await next(ctx);
            return;
        }

        using var scope = ctx.RequestServices.CreateScope();
        var credRepo = scope.ServiceProvider.GetRequiredService<IApiCredentialRepository>();

        var (authType, rawSecret, username) = ExtractCredentials(ctx.Request);

        if (authType == null)
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(new { error = "Missing authentication" });
            return;
        }

        var candidates = await credRepo.GetByAuthTypeAsync(authType.Value);
        Domain.Entities.ApiCredential? matched = null;

        foreach (var cred in candidates)
        {
            bool valid = authType.Value switch
            {
                AuthType.Basic => cred.Username == username && BCrypt.Net.BCrypt.Verify(rawSecret, cred.HashedSecret),
                _ => BCrypt.Net.BCrypt.Verify(rawSecret, cred.HashedSecret)
            };

            if (valid) { matched = cred; break; }
        }

        if (matched == null)
        {
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(new { error = "Invalid credentials" });
            return;
        }

        if (!matched.Enabled)
        {
            ctx.Response.StatusCode = 403;
            await ctx.Response.WriteAsJsonAsync(new { error = "Credential disabled" });
            return;
        }

        // Update last used (fire-and-forget)
        matched.LastUsedAtUtc = DateTime.UtcNow;
        _ = Task.Run(async () =>
        {
            try { await credRepo.UpdateAsync(matched); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to update LastUsedAt"); }
        });

        ctx.Items["credential"] = matched;
        await next(ctx);
    }

    private static (AuthType? type, string secret, string? username) ExtractCredentials(HttpRequest req)
    {
        var auth = req.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return (AuthType.Bearer, auth[7..].Trim(), null);

        if (auth.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(auth[6..].Trim()));
                var colon = decoded.IndexOf(':');
                if (colon > 0)
                    return (AuthType.Basic, decoded[(colon + 1)..], decoded[..colon]);
            }
            catch { /* invalid base64 */ }
        }

        var apiKey = req.Headers["X-Api-Key"].ToString();
        if (!string.IsNullOrEmpty(apiKey))
            return (AuthType.ApiKey, apiKey, null);

        return (null, string.Empty, null);
    }
}
