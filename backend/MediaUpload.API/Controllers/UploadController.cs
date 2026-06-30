using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MediaUpload.Application.DTOs;
using MediaUpload.Application.Services;
using MediaUpload.Domain.Enums;
using MediaUpload.Domain.Entities;
using MediaUpload.Domain.Interfaces;
using MediaUpload.API.Hubs;
using MediaUpload.API.Middleware;

namespace MediaUpload.API.Controllers;

[ApiController]
[Route("api/upload")]
public class UploadController(
    IUploadJobRepository jobRepo,
    SettingsService settings,
    IHubContext<JobHub> hub) : ControllerBase
{
    [HttpPost]
    [RequirePermission("upload")]
    public async Task<IActionResult> Upload(
        [FromForm] UploadRequest meta,
        IFormFileCollection files)
    {
        // Validate from settings (no hardcoded values)
        var maxFileSize  = await settings.GetLongAsync("upload.max_file_size_bytes", 1572864000);
        var maxFiles     = await settings.GetIntAsync("upload.max_files_per_request", 5);
        var allowedExts  = await settings.GetListAsync("upload.allowed_extensions");
        var uploadDir    = await settings.GetAsync("nas.upload_dir", "/mnt/nas/uploads");

        if (files.Count == 0)
            return BadRequest(new { error = "No files provided" });

        if (files.Count > maxFiles)
            return BadRequest(new { error = $"Max {maxFiles} files per request" });

        var responses = new List<UploadResponse>();

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (allowedExts.Count > 0 && !allowedExts.Contains(ext))
                return BadRequest(new { error = $"Extension '{ext}' not allowed" });

            if (file.Length > maxFileSize)
                return BadRequest(new { error = $"File '{file.FileName}' exceeds max size" });

            // Sanitize original filename – strip path traversal characters
            var safeOriginalName = Path.GetFileName(file.FileName).Replace("..", "");

            var fileId = Guid.NewGuid().ToString("N");
            var savedName = $"{fileId}{ext}";
            var savedPath = Path.Combine(uploadDir, savedName);

            Directory.CreateDirectory(uploadDir);
            await using var fs = System.IO.File.Create(savedPath);
            await file.CopyToAsync(fs);

            var job = new UploadJob
            {
                FileId           = fileId,
                OriginalFileName = safeOriginalName,
                SavedPath        = savedPath,
                FileSize         = file.Length,
                ErpTarget        = meta.ErpTarget,
                Longitude        = meta.Longitude,
                Latitude         = meta.Latitude,
                FlowId           = meta.FlowId,
                OrderId          = meta.OrderId,
                NvktId           = meta.NvktId,
                MaxRetry         = await settings.GetIntAsync("worker.max_retry", 3),
            };

            await jobRepo.AddAsync(job);
            await hub.Clients.All.SendAsync("job:created", MapJob(job));
            responses.Add(new UploadResponse(job.Id, job.FileId, job.OriginalFileName, job.FileSize, job.Status.ToString(), job.CreatedAtUtc.ToString("O")));
        }

        return Ok(responses);
    }

    [HttpGet]
    [RequirePermission("read_jobs")]
    public async Task<IActionResult> GetFiles([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var items = await jobRepo.GetJobsAsync(page, pageSize, null);
        var total = await jobRepo.CountAsync();
        return Ok(new JobListResponse(items.Select(MapJob).ToList(), total, page, pageSize));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("config")]
    public async Task<IActionResult> DeleteFile(Guid id)
    {
        var job = await jobRepo.GetByIdAsync(id);
        if (job == null) return NotFound();
        if (System.IO.File.Exists(job.SavedPath))
            System.IO.File.Delete(job.SavedPath);
        await jobRepo.DeleteAsync(id);
        return NoContent();
    }

    private static JobDto MapJob(UploadJob j) => new(
        j.Id, j.FileId, j.OriginalFileName, j.FileSize, j.ErpTarget,
        j.Status.ToString(), j.RetryCount, j.MaxRetry, j.LastError,
        j.CreatedAtUtc.ToString("O"),
        j.ProcessedAtUtc?.ToString("O"),
        j.CompletedAtUtc?.ToString("O"));
}
