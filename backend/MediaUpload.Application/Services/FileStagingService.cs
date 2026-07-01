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
    /// otherwise returns the original staging path unchanged.
    /// </summary>
    public async Task<string> PromoteToNasIfWithinWindowAsync(string currentPath)
    {
        var stagingDir = await GetStagingDirAsync();
        if (!IsUnderDir(currentPath, stagingDir))
            return currentPath; // already on NAS (or somewhere else) – nothing to do

        if (!await timeWindow.IsWithinWindowAsync())
            return currentPath; // outside window – stays staged locally for now

        var nasDir = await GetNasDirAsync();
        Directory.CreateDirectory(nasDir);
        var destPath = Path.Combine(nasDir, Path.GetFileName(currentPath));
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
}
