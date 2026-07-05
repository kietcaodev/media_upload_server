namespace MediaUpload.Application.Services;

/// <summary>
/// Files are always written to a local staging directory first at upload time.
/// If the current time falls within a configured Time Window, the file is moved
/// straight to the NAS directory right away. Otherwise it stays staged locally
/// until the worker's producer loop finds an open window and promotes it before
/// the job is pushed to ERP (ERP always expects the file to already be on NAS).
/// </summary>
public class FileStagingService(SettingsService settings, TimeWindowChecker timeWindow)
{
    public Task<string> GetStagingDirAsync() =>
        settings.GetAsync("nas.local_staging_dir", "/opt/media-upload/staging");

    public Task<string> GetNasDirAsync() =>
        settings.GetAsync("nas.upload_dir", "/opt/media-upload/uploads");

    /// <summary>
    /// Call right after a file has been written to the staging directory.
    /// Returns the NAS path if the move happened immediately (within window),
    /// otherwise returns the original staging path unchanged. Preserves whatever
    /// sub-folder structure the file was staged under (see BuildFolderStructure).
    /// </summary>
    public async Task<string> PromoteToNasIfWithinWindowAsync(string currentPath)
    {
        var stagingDir = await GetStagingDirAsync();
        if (!IsUnderDir(currentPath, stagingDir))
            return currentPath; // already on NAS (or somewhere else) – nothing to do

        if (!await timeWindow.IsWithinWindowAsync())
            return currentPath; // outside window – stays staged locally for now

        var nasDir = await GetNasDirAsync();
        var relative = Path.GetRelativePath(stagingDir, currentPath);
        var destPath = Path.Combine(nasDir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        File.Move(currentPath, destPath, overwrite: true);
        return destPath;
    }

    private static bool IsUnderDir(string path, string dir)
    {
        var fullPath = Path.GetFullPath(path);
        var fullDir = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase);
    }

    // ── Legacy folder-structure helpers (ported từ server.js) ───────────────

    /// <summary>Strip ký tự không an toàn khỏi tên thư mục (company_id/ord_code/user_id).</summary>
    public static string SanitizeFolderName(string name)
    {
        var sanitized = System.Text.RegularExpressions.Regex.Replace(name, "[^a-zA-Z0-9._\\-\\s()]", "_");
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, "\\.{2,}", "_");
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, "\\s+", "_");
        return sanitized.Trim();
    }

    /// <summary>Strip ký tự không an toàn khỏi tên file (giữ khoảng trắng/dấu ngoặc, không cho path traversal).</summary>
    public static string SanitizeFilename(string filename)
    {
        var baseName = Path.GetFileName(filename);
        var sanitized = System.Text.RegularExpressions.Regex.Replace(baseName, "[^a-zA-Z0-9._\\-\\s()]", "_");
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, "\\.{2,}", "_").Trim();
        if (string.IsNullOrEmpty(sanitized) || sanitized is "." or "..")
            return $"file_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        return sanitized;
    }

    /// <summary>{company}/{ordCode}/{user}/{yyyy}/{mm}/{dd} – khớp cấu trúc thư mục của server.js cũ.
    /// Truyền vào giờ ĐỊA PHƯƠNG (đã quy đổi theo system.timezone), không phải UTC – server.js gốc
    /// dùng `new Date()` (giờ hệ thống local), nếu dùng UTC trực tiếp thì ngày trong thư mục sẽ lệch
    /// 1 ngày trong nhiều giờ mỗi ngày đối với các timezone lệch UTC (vd Asia/Ho_Chi_Minh = UTC+7).</summary>
    public static (string RelativeFolder, string Company, string OrdCode, string User, string Year, string Month, string Day)
        BuildFolderStructure(string companyId, string ordCode, string userId, DateTime nowLocal)
    {
        var company = SanitizeFolderName(companyId);
        var ord = SanitizeFolderName(ordCode);
        var user = SanitizeFolderName(userId);
        var year = nowLocal.Year.ToString();
        var month = nowLocal.Month.ToString("D2");
        var day = nowLocal.Day.ToString("D2");
        var relFolder = Path.Combine(company, ord, user, year, month, day);
        return (relFolder, company, ord, user, year, month, day);
    }

    /// <summary>
    /// Tên file cuối cùng trong thư mục đích (customFilename nếu có, không thì tên gốc đã sanitize),
    /// tự thêm hậu tố timestamp+random nếu đã tồn tại file cùng tên (chống ghi đè, giống
    /// generateUniqueFilename() của server.js cũ). Kiểm tra CẢ staging dir lẫn NAS dir – file có thể
    /// đang nằm ở 1 trong 2 nơi tuỳ đã được promote lên NAS hay chưa (xem PromoteToNasIfWithinWindowAsync),
    /// kể cả file vừa ghi trong CÙNG request này (upload nhiều file trùng tên/trùng customFilename).
    /// </summary>
    public async Task<string> GenerateUniqueFilenameAsync(string relativeFolder, string originalName, string? customFilename)
    {
        var baseName = !string.IsNullOrWhiteSpace(customFilename) ? SanitizeFilename(customFilename) : SanitizeFilename(originalName);
        var nasDir = await GetNasDirAsync();
        var stagingDir = await GetStagingDirAsync();
        var nasTargetDir = Path.Combine(nasDir, relativeFolder);
        var stagingTargetDir = Path.Combine(stagingDir, relativeFolder);

        var filename = baseName;
        if (File.Exists(Path.Combine(nasTargetDir, filename)) || File.Exists(Path.Combine(stagingTargetDir, filename)))
        {
            var ext = Path.GetExtension(baseName);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(baseName);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var random = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(2)).ToLowerInvariant();
            filename = $"{nameWithoutExt}_{timestamp}_{random}{ext}";
        }
        return filename;
    }
}

