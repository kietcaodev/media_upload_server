using MediaUpload.Domain.Entities;
using MediaUpload.Domain.Enums;
using MediaUpload.Domain.Interfaces;

namespace MediaUpload.Application.Services;

public class ErpPushService(
    IErpEndpointConfigRepository erpRepo,
    IEncryptionService encryption,
    SettingsService settings,
    IHttpClientFactory httpFactory)
{
    public async Task<(bool success, string? error)> PushAsync(UploadJob job)
    {
        var erp = await erpRepo.GetByTargetAsync(job.ErpTarget);
        if (erp == null || !erp.Enabled)
            return (false, $"ERP target '{job.ErpTarget}' not found or disabled");

        var token = encryption.Decrypt(erp.EncryptedToken);
        var prefix = await settings.GetAsync("nas.video_path_prefix", "/homes/video/uploads/");

        var client = httpFactory.CreateClient("erp");
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(job.Longitude ?? ""), "longitude");
        form.Add(new StringContent(job.Latitude ?? ""), "latitude");
        form.Add(new StringContent(job.FlowId ?? ""), "flow_id");
        form.Add(new StringContent(job.OrderId ?? ""), "order_id");
        form.Add(new StringContent(job.NvktId ?? ""), "nvkt_id");
        form.Add(new StringContent($"{prefix}{Path.GetFileName(job.SavedPath)}"), "list_video_path[]");

        // QUAN TRỌNG: ERP thực tế (locnuoc365.xyz/zomzem.xyz/erp.zozin.vn) expose
        // route dạng POST {baseUrl}/{order_id} (xem server.js gốc - bản Node.js
        // cũ đã chạy production trước đây), KHÔNG phải POST thẳng {baseUrl}.
        // Thiếu đoạn nối order_id vào path này khiến mọi request tới ERP thật
        // trả 404 dù order_id vẫn có trong form-data.
        var erpUrl = $"{erp.Url.TrimEnd('/')}/{job.OrderId}";
        using var request = new HttpRequestMessage(HttpMethod.Post, erpUrl) { Content = form };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        try
        {
            var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode) return (true, null);
            var body = await response.Content.ReadAsStringAsync();
            return (false, $"ERP returned {(int)response.StatusCode}: {body[..Math.Min(200, body.Length)]}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
