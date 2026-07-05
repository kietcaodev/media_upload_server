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
    FileStagingService fileStaging,
    IHubContext<JobHub> hub) : ControllerBase
{
    [HttpPost]
    [RequirePermission("upload")]
    public async Task<IActionResult> Upload(
        [FromForm(Name = "company_id")] string? companyId,
        [FromForm(Name = "ord_code")] string? ordCode,
        [FromForm(Name = "user_id")] string? userId,
        [FromForm(Name = "filename")] string? customFilename,
        [FromForm(Name = "longitude")] string? longitude,
        [FromForm(Name = "latitude")] string? latitude,
        [FromForm(Name = "flow_id")] string? flowId,
        [FromForm(Name = "order_id")] string? orderId,
        [FromForm(Name = "nvkt_id")] string? nvktId,
        IFormFileCollection files)
    {
        // Validate required fields – giữ nguyên field name/code/message như server.js cũ
        // để hệ thống ngoài đang tích hợp theo contract đó không phải sửa gì.
        (string Code, string? Value, string Message)[] required =
        [
            ("MISSING_COMPANY_ID", companyId, "Thiếu company_id (bắt buộc)"),
            ("MISSING_ORD_CODE",   ordCode,   "Thiếu ord_code (bắt buộc)"),
            ("MISSING_USER_ID",    userId,    "Thiếu user_id (bắt buộc)"),
            ("MISSING_LONGITUDE",  longitude, "Thiếu longitude (bắt buộc)"),
            ("MISSING_LATITUDE",   latitude,  "Thiếu latitude (bắt buộc)"),
            ("MISSING_FLOW_ID",    flowId,    "Thiếu flow_id (bắt buộc)"),
            ("MISSING_ORDER_ID",   orderId,   "Thiếu order_id (bắt buộc)"),
            ("MISSING_NVKT_ID",    nvktId,    "Thiếu nvkt_id (bắt buộc)"),
        ];
        foreach (var (code, value, message) in required)
            if (string.IsNullOrWhiteSpace(value))
                return BadRequest(new { success = false, code, message });

        if (files.Count == 0)
            return BadRequest(new { success = false, code = "NO_FILES", message = "Không tìm thấy file" });

        var maxFileSize = await settings.GetLongAsync("upload.max_file_size_bytes", 1572864000);
        var maxFiles    = await settings.GetIntAsync("upload.max_files_per_request", 5);
        var allowedExts = await settings.GetListAsync("upload.allowed_extensions");

        if (files.Count > maxFiles)
            return BadRequest(new { success = false, code = "TOO_MANY_FILES", message = $"Quá nhiều file. Tối đa {maxFiles} file" });

        // Pass 1: validate TOÀN BỘ file (đuôi/dung lượng/magic-bytes) trước khi ghi bất
        // kỳ file nào xuống đĩa hoặc tạo job – tránh phải rollback nửa chừng như bản cũ.
        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (allowedExts.Count > 0 && !allowedExts.Contains(ext))
                return BadRequest(new { success = false, code = "INVALID_FILE_TYPE", message = $"File {file.FileName} không đúng định dạng" });

            if (file.Length > maxFileSize)
                return BadRequest(new { success = false, code = "FILE_TOO_LARGE", message = $"File quá lớn. Giới hạn {maxFileSize / 1024 / 1024}MB" });

            await using var stream = file.OpenReadStream();
            if (!await FileSignatureValidator.IsValidAsync(stream))
                return BadRequest(new { success = false, code = "INVALID_FILE_TYPE", message = $"File {file.FileName} không đúng định dạng" });
        }

        var nowUtc = DateTime.UtcNow;
        var tz = await settings.GetTimezoneAsync();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
        var (relFolder, company, ord, user, year, month, day) =
            FileStagingService.BuildFolderStructure(companyId!, ordCode!, userId!, nowLocal);
        var stagingDir = await fileStaging.GetStagingDirAsync();
        var erpTarget  = companyId!.Trim().ToUpperInvariant();
        var maxRetry   = await settings.GetIntAsync("worker.max_retry", 3);

        var uploadedFiles  = new List<UploadedFileDto>();
        var listVideoPaths = new List<string>();
        var jobRefs        = new List<UploadJobRefDto>();
        long totalSize = 0;

        // Pass 2: mọi file đã hợp lệ – ghi xuống staging + tạo job.
        foreach (var file in files)
        {
            var finalFilename = await fileStaging.GenerateUniqueFilenameAsync(relFolder, file.FileName, customFilename);
            var relativePath  = Path.Combine(relFolder, finalFilename).Replace('\\', '/');
            var stagingPath   = Path.Combine(stagingDir, relFolder, finalFilename);

            Directory.CreateDirectory(Path.GetDirectoryName(stagingPath)!);
            await using (var fs = System.IO.File.Create(stagingPath))
                await file.CopyToAsync(fs);

            // Trong window → chuyển thẳng lên NAS ngay; ngoài window → giữ ở staging,
            // worker sẽ tự chuyển lên NAS khi vào window (xem FileStagingService).
            var savedPath = await fileStaging.PromoteToNasIfWithinWindowAsync(stagingPath);

            var job = new UploadJob
            {
                FileId           = Guid.NewGuid().ToString("N"),
                OriginalFileName = Path.GetFileName(file.FileName).Replace("..", ""),
                SavedPath        = savedPath,
                RelativePath     = relativePath,
                FileSize         = file.Length,
                ErpTarget        = erpTarget,
                Longitude        = longitude,
                Latitude         = latitude,
                FlowId           = flowId,
                OrderId          = orderId,
                NvktId           = nvktId,
                CompanyId        = company,
                OrdCode          = ord,
                UserId           = user,
                CustomFilename   = customFilename,
                MaxRetry         = maxRetry,
            };

            await jobRepo.AddAsync(job);
            await hub.Clients.All.SendAsync("job:created", MapJob(job));

            var sizeInMb = (file.Length / 1024.0 / 1024.0).ToString("F2");
            uploadedFiles.Add(new UploadedFileDto(job.FileId, job.OriginalFileName, finalFilename, relativePath, file.Length, sizeInMb));
            listVideoPaths.Add(relativePath);
            jobRefs.Add(new UploadJobRefDto(job.Id.ToString(), job.FileId, job.Status.ToString()));
            totalSize += file.Length;
        }

        var uploadedAtVN = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz).ToString("dd/MM/yyyy HH:mm:ss");

        var response = new UploadResultResponse(
            Success: true,
            Message: uploadedFiles.Count == 1 ? "Upload thành công" : $"Upload thành công {uploadedFiles.Count} file",
            CompanyId: company,
            OrdCode: ord,
            UserId: user,
            OrderId: orderId,
            NvktId: nvktId,
            Location: new LocationDto(ParseDoubleOrNull(longitude), ParseDoubleOrNull(latitude)),
            FlowId: flowId,
            FolderStructure: new FolderStructureDto(company, ord, user, year, month, day),
            ListVideoPaths: listVideoPaths,
            UploadedAt: nowUtc.ToString("O"),
            UploadedAtVN: uploadedAtVN,
            ErpSync: new ErpSyncDto(
                Queued: true,
                Message: "Đã đưa vào hàng đợi, worker sẽ đẩy lên ERP khi tới lượt xử lý (có thể trễ nếu ngoài Time Window đang cấu hình).",
                Jobs: jobRefs),
            File: uploadedFiles.Count == 1 ? uploadedFiles[0] : null,
            Files: uploadedFiles.Count > 1 ? uploadedFiles : null,
            TotalSize: uploadedFiles.Count > 1 ? totalSize : null,
            TotalSizeMB: uploadedFiles.Count > 1 ? (totalSize / 1024.0 / 1024.0).ToString("F2") : null
        );

        return Ok(response);
    }

    private static double? ParseDoubleOrNull(string? s) =>
        double.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;

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
