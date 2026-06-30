# Backend API – .NET 10

> **Framework:** ASP.NET Core 10 Minimal API + Controllers  
> **Auth:** Bearer Token / Basic Auth / API Key — xem chi tiết [08-authentication.md](./08-authentication.md)  
> **Rate Limit:** Built-in .NET 8+ Rate Limiter

---

## Base URL

```
http://localhost:5000/api
```

---

## 1. Upload API

### `POST /api/upload`
Upload file lên NAS và tạo UploadJob.

**Request:** `multipart/form-data`
| Field | Bắt buộc | Mô tả |
|-------|----------|-------|
| `files` | ✅ | 1–5 files (video/audio) |
| `userId` | ✅ | Mã nhân viên |
| `customerCode` | ✅ | Mã khách hàng |
| `erpTarget` | ✅ | `DND` / `Zomzem` / `Zozin` |

**Response 200:**
```json
{
  "success": true,
  "uploaded": [
    {
      "fileId": "b239e355c7b311ae",
      "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "originalName": "video.mp4",
      "sizeInMB": "14.12",
      "status": "Pending"
    }
  ]
}
```

**Validation** *(đọc động từ DB qua `SettingsService`)*:
- Max file size: lấy từ `upload.max_file_size_mb`
- Max files/request: lấy từ `upload.max_files_per_request`
- Allowed extensions: lấy từ `upload.allowed_extensions`
- Allowed MIME types: lấy từ `upload.allowed_mimetypes`
- Min disk space: lấy từ `nas.min_disk_space_gb`

---

### `GET /api/files`
Danh sách tất cả files đã upload.

**Query params:**
| Param | Mô tả |
|-------|-------|
| `page` | Trang (default: 1) |
| `pageSize` | Số bản ghi (default: 20) |
| `userId` | Lọc theo nhân viên |
| `customerCode` | Lọc theo khách hàng |
| `from` | Từ ngày (ISO8601) |
| `to` | Đến ngày |

**Response 200:**
```json
{
  "total": 150,
  "page": 1,
  "pageSize": 20,
  "data": [{ "fileId": "...", "originalName": "...", ... }]
}
```

---

### `DELETE /api/files/{fileId}`
Xóa file khỏi NAS và hủy job tương ứng.

**Response 200:**
```json
{ "success": true, "message": "File deleted" }
```

**Lưu ý:** Chỉ xóa được file có job ở trạng thái `Pending` hoặc `Failed`. File đang `Running` không được xóa.

---

## 2. Jobs API

### `GET /api/jobs`
Danh sách jobs với filter và phân trang.

**Query params:**
| Param | Mô tả |
|-------|-------|
| `status` | `Pending` / `Running` / `Done` / `Failed` / `Paused` |
| `erpTarget` | `DND` / `Zomzem` / `Zozin` |
| `from` / `to` | Khoảng thời gian |
| `search` | Tìm theo fileId, userId, customerCode |
| `page` / `pageSize` | Phân trang |

**Response 200:**
```json
{
  "total": 450,
  "page": 1,
  "data": [
    {
      "id": "3fa85f64...",
      "fileId": "b239e355",
      "originalName": "video.mp4",
      "erpTarget": "DND",
      "status": "Pending",
      "retryCount": 0,
      "createdAt": "2026-06-30T07:00:00Z",
      "scheduledAt": null
    }
  ]
}
```

---

### `POST /api/jobs/{id}/pause`
Pause 1 job đang ở trạng thái `Pending` hoặc `Running`.

**Response 200:**
```json
{ "success": true, "jobId": "...", "newStatus": "Paused" }
```

---

### `POST /api/jobs/{id}/resume`
Resume job đang ở trạng thái `Paused`.

**Response 200:**
```json
{ "success": true, "jobId": "...", "newStatus": "Pending" }
```

---

### `POST /api/jobs/{id}/retry`
Retry job đang ở trạng thái `Failed`. Reset RetryCount về 0.

**Response 200:**
```json
{ "success": true, "jobId": "...", "newStatus": "Pending" }
```

---

### `DELETE /api/jobs/{id}`
Hủy job (chuyển sang `Cancelled`). Không xóa file trên NAS.

**Response 200:**
```json
{ "success": true, "jobId": "...", "newStatus": "Cancelled" }
```

---

## 3. Config API – TimeWindow

### `GET /api/config/timewindows`
```json
[
  {
    "id": 1,
    "name": "Sáng sớm",
    "daysOfWeek": "1,2,3,4,5",
    "startTime": "07:00",
    "endTime": "08:00",
    "isEnabled": true,
    "maxConcurrent": 5,
    "erpTarget": null,
    "isCurrentlyActive": false
  }
]
```

### `POST /api/config/timewindows`
**Body:**
```json
{
  "name": "Cuối tuần",
  "daysOfWeek": "6,7",
  "startTime": "08:00",
  "endTime": "22:00",
  "isEnabled": true,
  "maxConcurrent": 10,
  "erpTarget": "DND"
}
```

### `PUT /api/config/timewindows/{id}`
Body tương tự POST.

### `DELETE /api/config/timewindows/{id}`
```json
{ "success": true }
```

---

## 4. Config API – ERP Endpoints

### `GET /api/config/erp`
```json
[
  {
    "id": 1,
    "name": "DND",
    "url": "https://locnuoc365.xyz/api/order/upload-videos",
    "tokenMasked": "***abc123",
    "isEnabled": true,
    "timeoutSeconds": 30
  }
]
```

### `PUT /api/config/erp/{id}`
**Body:**
```json
{
  "url": "https://locnuoc365.xyz/api/order/upload-videos",
  "token": "new-token-value",
  "isEnabled": true,
  "timeoutSeconds": 30
}
```

> Token được mã hóa AES-256 trước khi lưu DB, chỉ trả về dạng masked khi GET.

---

## 5. Worker Control API

### `GET /api/worker/status`
```json
{
  "isPaused": false,
  "pausedReason": null,
  "activeJobsCount": 3,
  "currentWindow": {
    "name": "Sáng sớm",
    "endTime": "08:00"
  },
  "nextWindow": {
    "name": "Tối",
    "startTime": "18:00"
  }
}
```

### `POST /api/worker/pause`
**Body:**
```json
{ "reason": "Bảo trì hệ thống" }
```

### `POST /api/worker/resume`
```json
{ "success": true }
```

---

## 6. System Settings API

> Xem đặc tả đầy đủ tại [09-system-settings.md](./09-system-settings.md)

### `GET /api/config/settings`
Trả về toàn bộ settings nhóm theo category (nas, upload, worker, ratelimit, cors).

### `PATCH /api/config/settings`
Cập nhật nhiều settings cùng lúc. Trả về danh sách key nào `hotReload` (hiệu lực ngay) và key nào `requiresRestart`.

### `POST /api/config/settings/reset/{key}`
Reset 1 key về giá trị seed mặc định.

---

## 7. Dashboard API

### `GET /api/dashboard/stats`
```json
{
  "pending": 12,
  "running": 3,
  "done": 450,
  "failed": 2,
  "cancelled": 1,
  "totalToday": 45,
  "successRateToday": 97.8
}
```

### `GET /api/dashboard/timeline`
**Query:** `?period=today|week|month&erpTarget=DND`

```json
{
  "labels": ["00:00", "01:00", "02:00", "..."],
  "datasets": [
    { "label": "Done", "data": [0, 0, 5, 12, 8, ...] },
    { "label": "Failed", "data": [0, 0, 0, 1, 0, ...] }
  ]
}
```

---

## 8. Credentials API

> Xem đặc tả đầy đủ tại [08-authentication.md](./08-authentication.md)

| Method | Endpoint | Mô tả |
|--------|----------|---------|
| `GET` | `/api/credentials` | Danh sách credentials (không trả secret) |
| `POST` | `/api/credentials/bearer` | Tạo Bearer Token |
| `POST` | `/api/credentials/basic` | Tạo Basic Auth |
| `POST` | `/api/credentials/apikey` | Tạo API Key |
| `PATCH` | `/api/credentials/{id}` | Bật/tắt, đổi quyền |
| `POST` | `/api/credentials/{id}/rotate` | Tạo secret mới |
| `DELETE` | `/api/credentials/{id}` | Thu hồi credential |

---

## SignalR Hub: `/hubs/jobs`

### Events từ Server → Client

| Event | Payload | Mô tả |
|-------|---------|-------|
| `job:created` | `{ jobId, fileId, erpTarget, status }` | Job mới được tạo |
| `job:statusChanged` | `{ jobId, oldStatus, newStatus, message }` | Trạng thái job thay đổi |
| `worker:statusChanged` | `{ isPaused, activeCount, reason }` | Worker pause/resume |
| `stats:updated` | `{ pending, running, done, failed }` | Cập nhật thống kê |

### Kết nối từ Client (TypeScript)

```typescript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/jobs")
  .withAutomaticReconnect()
  .build();

connection.on("job:statusChanged", (payload) => {
  useJobStore.getState().updateJobStatus(payload);
});

connection.on("stats:updated", (stats) => {
  useDashboardStore.getState().setStats(stats);
});

await connection.start();
```

---

## Program.cs (cấu hình chính)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Database – PostgreSQL
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Services
builder.Services.AddScoped<IUploadService, UploadService>();
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<IConfigService, ConfigService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// Worker
builder.Services.AddSingleton<WorkerStateService>(); // giữ state pause/resume
builder.Services.AddHostedService<JobWorkerService>();

// SignalR
builder.Services.AddSignalR();

// CORS
builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.WithOrigins("http://localhost:5173")  // React dev
     .AllowAnyMethod().AllowAnyHeader().AllowCredentials()));

// Rate Limiting
builder.Services.AddRateLimiter(opt => {
    opt.AddFixedWindowLimiter("upload", w => {
        w.PermitLimit = 10;
        w.Window = TimeSpan.FromMinutes(1);
    });
});

var app = builder.Build();

app.UseCors();
app.UseRateLimiter();
app.MapControllers();
app.MapHub<JobHub>("/hubs/jobs");

// Auto migrate on startup
using var scope = app.Services.CreateScope();
scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

app.Run();
```
