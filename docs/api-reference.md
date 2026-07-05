# API Reference – Gọi trực tiếp API (không qua Web UI)

Tài liệu này dành cho hệ thống ngoài (ERP, script, app khác) muốn tích hợp trực tiếp với Media Upload API mà không qua giao diện web. Toàn bộ endpoint đọc trực tiếp từ code backend (`backend/MediaUpload.API/Controllers`).

---

## 1. Base URL

| Môi trường | Base URL |
|---|---|
| Local dev (.NET chạy trực tiếp) | `http://localhost:5045` (http) hoặc `https://localhost:7068` (https) |
| Production (qua Nginx reverse proxy) | `https://<VPS_IP>:8443/media-upload` |

> Production dùng chứng chỉ TLS tự ký → client (curl/Postman) cần bỏ qua verify SSL (`curl -k`), hoặc cài chứng chỉ CA tương ứng.

Tất cả path bên dưới viết dạng **tương đối** (vd `/api/upload`). Ở production, ghép thêm prefix `/media-upload`, vd:
```
https://<VPS_IP>:8443/media-upload/api/upload
```

---

## 2. Xác thực (Authentication)

Mọi request (trừ `GET /health` và WebSocket `/hubs/*`) **bắt buộc** phải có 1 trong 3 kiểu Authorization header, tương ứng loại `AuthType` của credential đã tạo trong tab **Cấu hình → Credentials/Auth** (hoặc qua API `POST /api/credentials`):

| Loại | Header | Ví dụ |
|---|---|---|
| **Bearer** | `Authorization: Bearer <raw_token>` | `Authorization: Bearer aBcD1234...` |
| **Basic** | `Authorization: Basic <base64(username:password)>` | dùng cho web login, username cố định vd `admin` |
| **ApiKey** | `X-Api-Key: <raw_token>` | header riêng, không dùng `Authorization` |

- Token **raw** (dạng gốc, chưa hash) chỉ được trả về **đúng 1 lần** tại thời điểm tạo (`POST /api/credentials`) hoặc rotate (`POST /api/credentials/{id}/rotate`). Server chỉ lưu bản BCrypt-hash, **không thể xem lại token cũ** — nếu mất phải rotate lại để lấy token mới.
- Mỗi credential có 3 quyền độc lập, kiểm tra riêng theo từng endpoint (xem cột **Quyền** ở bảng endpoint bên dưới):
  - `CanUpload` → permission `upload`
  - `CanReadJobs` → permission `read_jobs`
  - `CanConfig` → permission `config`
- Thiếu header xác thực → `401 { "error": "Missing authentication" }`.
- Sai token/password → `401 { "error": "Invalid credentials" }`.
- Credential bị disable (`Enabled = false`) → `403 { "error": "Credential disabled" }`.
- Đúng token nhưng thiếu quyền cho endpoint đó → `403 { "error": "Permission '<perm>' required" }`.

### Ví dụ tạo credential Bearer mới (cần credential có quyền `config` để gọi)
```bash
curl -k -X POST https://<VPS_IP>:8443/media-upload/api/credentials \
  -H "Authorization: Bearer <admin_token>" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "ERP integration - DND",
    "authType": 0,
    "canUpload": true,
    "canReadJobs": true,
    "canConfig": false,
    "allowedErp": "DND"
  }'
```
`authType`: `0 = Bearer`, `1 = Basic`, `2 = ApiKey`.

Response (chỉ 1 lần):
```json
{
  "id": 5,
  "name": "ERP integration - DND",
  "authType": "Bearer",
  "rawToken": "9f8e7d6c5b4a...",
  "username": null,
  "tokenPrefix": "9f8e7d6c"
}
```
→ Lưu `rawToken` lại ngay, dùng làm `Authorization: Bearer 9f8e7d6c5b4a...` cho các request upload về sau.

---

## 3. Upload file & tạo job

### `POST /api/upload`
**Quyền:** `upload` · **Content-Type:** `multipart/form-data`

> Contract của endpoint này giữ **nguyên field/response như bản `server.js` cũ** (để hệ thống ngoài đang tích hợp không cần sửa code) — chỉ khác ở cách xử lý ERP (xem mục "Khi nào push ERP" bên dưới).

| Field form-data | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `files` (hoặc `files[]`) | file (có thể lặp nhiều lần cùng field name để gửi nhiều file) | ✅ | Tối đa `upload.max_files_per_request` file/request (mặc định 5) |
| `company_id` | string | ✅ | Cũng được dùng làm ERP target (viết hoa tự động) — phải khớp 1 `target` đã tạo ở `/api/config/erp` (vd `DND`, `ZOMZEM`, `ZOZIN`) |
| `ord_code` | string | ✅ | Mã đơn hàng — dùng làm tên thư mục con |
| `user_id` | string | ✅ | Dùng làm tên thư mục con |
| `longitude` | string | ✅ | Kinh độ |
| `latitude` | string | ✅ | Vĩ độ |
| `flow_id` | string | ✅ | |
| `order_id` | string | ✅ | |
| `nvkt_id` | string | ✅ | |
| `filename` | string | không | Tên file tuỳ chỉnh (không cần đuôi phải khớp, sẽ tự sanitize) — bỏ trống thì dùng tên file gốc đã sanitize |

Giới hạn áp dụng theo **System Settings** hiện hành (đọc động từ DB mỗi request, không hardcode):
- `upload.max_file_size_bytes` — dung lượng tối đa mỗi file.
- `upload.allowed_extensions` — danh sách đuôi file cho phép (so khớp đuôi **và** kiểm tra magic-bytes thật của file, đổi đuôi giả không qua được).
- `upload.max_files_per_request` — số file tối đa/request.

**File được lưu theo cây thư mục** (giống hệt `server.js` cũ):
```
{nas.upload_dir}/{company_id}/{ord_code}/{user_id}/{yyyy}/{mm}/{dd}/{filename}
```

### Khi nào push ERP?
Khác với `server.js` cũ (gọi ERP **đồng bộ ngay trong request**, response phải chờ ERP trả lời), hệ thống hiện tại làm theo đúng logic **lưu local trước, rồi mới check & chạy job**:
1. File được lưu ngay lập tức vào khu vực staging local.
2. Nếu thời điểm hiện tại đang nằm trong 1 **Time Window** đang bật (tab Cấu hình → Time Windows) → file được chuyển thẳng lên `nas.upload_dir` ngay.
3. Mỗi file tạo **1 UploadJob** (`Pending`) → response trả về **ngay lập tức**, không chờ ERP.
4. Một worker nền kiểm tra định kỳ (`worker.tick_interval_seconds`): nếu đang trong Time Window, chuyển nốt các file còn ở staging lên NAS rồi đẩy job lên ERP thật; nếu thất bại sẽ tự retry theo `worker.max_retry`/`worker.retry_delay_minutes`.
→ Vì vậy trường `erpSync` trong response chỉ phản ánh **trạng thái đã xếp hàng đợi**, không phải kết quả push ERP thật (poll `GET /api/jobs/{id}` hoặc subscribe SignalR để biết kết quả thật — xem mục 7).

**Response `200 OK`:**
```json
{
  "success": true,
  "message": "Upload thành công",
  "companyId": "DND",
  "ordCode": "KHZ28695175",
  "userId": "NVDND002361",
  "orderId": "ORD20260001",
  "nvktId": "NVKT123456",
  "location": { "longitude": 106.6297, "latitude": 10.8231 },
  "flowId": "FLOW001",
  "folderStructure": {
    "company": "DND", "ordCode": "KHZ28695175", "user": "NVDND002361",
    "year": "2026", "month": "07", "day": "05"
  },
  "listVideoPaths": ["DND/KHZ28695175/NVDND002361/2026/07/05/video.mp4"],
  "uploadedAt": "2026-07-05T08:45:12.0000000Z",
  "uploadedAtVN": "05/07/2026 15:45:12",
  "erpSync": {
    "queued": true,
    "message": "Đã đưa vào hàng đợi, worker sẽ đẩy lên ERP khi tới lượt xử lý (có thể trễ nếu ngoài Time Window đang cấu hình).",
    "jobs": [{ "jobId": "6f9e2b1a-...", "fileId": "b2f7a1c4...", "status": "Pending" }]
  },
  "file": {
    "fileId": "b2f7a1c4...",
    "originalName": "video.mp4",
    "filename": "video.mp4",
    "relativePath": "DND/KHZ28695175/NVDND002361/2026/07/05/video.mp4",
    "size": 15728640,
    "sizeInMB": "15.00"
  }
}
```
Khi upload **nhiều file** cùng lúc: thay `file` bằng `files` (mảng), kèm thêm `totalSize` và `totalSizeMB`; `listVideoPaths`/`erpSync.jobs` sẽ có nhiều phần tử tương ứng.

**Lỗi thường gặp** (giữ nguyên `code`/`message` như `server.js` cũ):
| HTTP | code | Nguyên nhân |
|---|---|---|
| 400 | `MISSING_COMPANY_ID` / `MISSING_ORD_CODE` / `MISSING_USER_ID` / `MISSING_LONGITUDE` / `MISSING_LATITUDE` / `MISSING_FLOW_ID` / `MISSING_ORDER_ID` / `MISSING_NVKT_ID` | Thiếu field bắt buộc tương ứng |
| 400 | `NO_FILES` | Không gửi file nào |
| 400 | `TOO_MANY_FILES` | Vượt `upload.max_files_per_request` |
| 400 | `INVALID_FILE_TYPE` | Đuôi file không được phép, hoặc nội dung thật (magic bytes) không khớp đuôi |
| 400 | `FILE_TOO_LARGE` | Vượt `upload.max_file_size_bytes` |
| 401 / 403 | — | Thiếu/sai/không đủ quyền xác thực — xem mục 2 |

Mỗi lỗi trả `{ "success": false, "code": "...", "message": "..." }`. Toàn bộ file trong request được validate (đuôi/dung lượng/magic-bytes) **trước khi** ghi bất kỳ file nào xuống đĩa hay tạo job — 1 file lỗi thì cả request bị từ chối, không có file/job nào được tạo nửa chừng.

### `curl` ví dụ — upload 1 file
```bash
curl -k -X POST https://<VPS_IP>:8443/media-upload/api/upload \
  -H "Authorization: Bearer <token>" \
  -F "files=@video.mp4" \
  -F "company_id=DND" \
  -F "ord_code=KHZ28695175" \
  -F "user_id=NVDND002361" \
  -F "longitude=106.6297" \
  -F "latitude=10.8231" \
  -F "flow_id=FLOW001" \
  -F "order_id=ORD20260001" \
  -F "nvkt_id=NVKT123456"
```

### `curl` ví dụ — upload nhiều file + tên file tuỳ chỉnh
```bash
curl -k -X POST https://<VPS_IP>:8443/media-upload/api/upload \
  -H "Authorization: Bearer <token>" \
  -F "files=@video1.mp4" \
  -F "files=@video2.mp4" \
  -F "company_id=ZOMZEM" \
  -F "ord_code=KHZ28695175" \
  -F "user_id=NVDND002361" \
  -F "longitude=106.6297" \
  -F "latitude=10.8231" \
  -F "flow_id=FLOW002" \
  -F "order_id=ORD20260002" \
  -F "nvkt_id=NVKT789012" \
  -F "filename=custom_name.mp4"
```

### Các endpoint upload khác
| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/api/upload?page=1&pageSize=20` | `read_jobs` | Danh sách job (giống `/api/jobs` nhưng không filter theo status) |
| `DELETE` | `/api/upload/{id}` | `config` | Xoá hẳn 1 job + file vật lý trên đĩa (không thể hoàn tác) |

---


## 4. Quản lý Job

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/api/jobs?page=1&pageSize=20&status=Pending` | `read_jobs` | Danh sách job, filter tuỳ chọn theo `status` (`Pending`/`Processing`/`Success`/`Failed`/`Cancelled`) |
| `GET` | `/api/jobs/{id}` | `read_jobs` | Chi tiết 1 job theo GUID |
| `PATCH` | `/api/jobs/{id}/cancel` | `config` | Huỷ job (chỉ khi job chưa ở trạng thái kết thúc `Success/Failed/Cancelled`) |
| `PATCH` | `/api/jobs/{id}/retry` | `config` | Đưa job `Failed` về lại `Pending`, reset `retryCount = 0` để worker thử lại |

`JobDto` trả về:
```json
{
  "id": "6f9e2b1a-...",
  "fileId": "b2f7a1c4...",
  "originalFileName": "video1.mp4",
  "fileSize": 15728640,
  "erpTarget": "DND",
  "status": "Pending",
  "retryCount": 0,
  "maxRetry": 3,
  "lastError": null,
  "createdAtUtc": "2026-07-05T08:45:12Z",
  "processedAtUtc": null,
  "completedAtUtc": null
}
```

**Lỗi:**
- `404` nếu `id` không tồn tại.
- `400 { "error": "Cannot cancel job in terminal state" }` khi cancel job đã Success/Failed/Cancelled.
- `400 { "error": "Only failed jobs can be retried" }` khi retry job không ở trạng thái `Failed`.

---

## 5. Dashboard & Worker (đọc trạng thái vận hành)

| Method | Path | Quyền | Mô tả |
|---|---|---|---|
| `GET` | `/api/dashboard/stats` | `read_jobs` | Thống kê tổng số job theo từng trạng thái + trạng thái worker + có đang trong time window không |
| `GET` | `/api/dashboard/timeline?days=7` | `read_jobs` | Số liệu Success/Failed/Pending theo từng ngày, `days` từ 1–90 (mặc định 7 nếu invalid) |
| `GET` | `/api/worker/status` | `config` | `{ isPaused, pauseReason, activeCount }` |
| `POST` | `/api/worker/pause` | `config` | Body: `{ "reason": "..." }` (optional) — tạm dừng worker đẩy job lên ERP |
| `POST` | `/api/worker/resume` | `config` | Cho worker chạy lại |

`GET /api/dashboard/stats` response:
```json
{
  "totalJobs": 120,
  "pendingJobs": 3,
  "processingJobs": 1,
  "successJobs": 110,
  "failedJobs": 6,
  "cancelledJobs": 0,
  "workerPaused": false,
  "workerPauseReason": null,
  "activeWorkers": 1,
  "withinTimeWindow": true
}
```

---

## 6. Cấu hình (Config) qua API

### 6.1 Time Windows — `/api/config/timewindows` (quyền `config`)
| Method | Path | Body |
|---|---|---|
| `GET` | `/` | – |
| `GET` | `/{id}` | – |
| `POST` | `/` | `{ "name", "startTime": "HH:mm", "endTime": "HH:mm", "daysOfWeek": "1,2,3,4,5", "enabled": true }` |
| `PUT` | `/{id}` | như trên |
| `DELETE` | `/{id}` | – |

`daysOfWeek`: chuỗi số cách nhau dấu phẩy, `1=Thứ 2 ... 7=Chủ nhật`.

### 6.2 ERP Endpoints — `/api/config/erp` (quyền `config`)
| Method | Path | Body |
|---|---|---|
| `GET` | `/` | – (trả `tokenPrefix`, **không** trả token thật) |
| `PUT` | `/` | `{ "target": "DND", "url": "https://...", "token": "raw-token-erp-cấp", "enabled": true }` — **upsert theo `target`** (viết hoa tự động), gọi lại với `target` đã có sẽ ghi đè URL/token/enabled |

> `url` chỉ cần base URL (vd `https://locnuoc365.xyz/api/order/upload-videos`) — hệ thống **tự nối thêm `/{order_id}`** khi thật sự gọi ERP lúc worker push job (xem `ErpPushService.cs`). Đảm bảo route phía ERP thật có dạng `POST {url}/{order_id}`.

### 6.3 System Settings — `/api/config/settings` (quyền `config`)
| Method | Path | Body |
|---|---|---|
| `GET` | `/` | – trả toàn bộ setting: `[{ "key", "value", "description", "hotReload", "updatedAtUtc" }, ...]` |
| `PATCH` | `/` | `{ "updates": { "upload.max_file_size_bytes": "2000000000", "worker.max_retry": "5" } }` — sửa nhiều key cùng lúc |
| `POST` | `/reset/{key}` | Đưa 1 key về giá trị mặc định gốc trong code |

Danh sách đầy đủ key + ý nghĩa: xem [huong-dan-su-dung.md § 6.4](huong-dan-su-dung.md#64-tab-system-settings).

> Field có `hotReload: true` áp dụng ngay; `hotReload: false` (hiện chỉ có `system.timezone`) cần restart service `.NET` mới có hiệu lực.

### 6.4 Credentials — `/api/credentials` (quyền `config`)
| Method | Path | Body / Ghi chú |
|---|---|---|
| `GET` | `/` | Danh sách (không trả token thật, chỉ `tokenPrefix`) |
| `GET` | `/{id}` | Chi tiết 1 credential |
| `POST` | `/` | `{ "name", "authType": 0\|1\|2, "username": null, "canUpload", "canReadJobs", "canConfig", "allowedErp": "DND,ZOMZEM" }` → trả `rawToken` **1 lần duy nhất** |
| `POST` | `/{id}/rotate` | Sinh secret mới, trả `{ rawToken, tokenPrefix }` **1 lần duy nhất** — token cũ mất hiệu lực ngay |
| `PUT` | `/{id}` | `{ "name", "canUpload", "canReadJobs", "canConfig", "allowedErp", "enabled" }` — **không** đổi được token qua đây, chỉ đổi metadata/quyền |
| `DELETE` | `/{id}` | Xoá vĩnh viễn |

`allowedErp`: chuỗi target cách nhau dấu phẩy (vd `"DND,ZOMZEM"`) — để trống = credential được upload cho **mọi** ERP target.

---

## 7. Realtime events qua SignalR

Hub: `wss://<host>:8443/media-upload/hubs/jobs` (production) hoặc `ws://localhost:5045/hubs/jobs` (dev).

- Endpoint `negotiate` **bỏ qua auth theo `Authorization` header thông thường** (middleware skip toàn bộ path `/hubs/*`), SignalR JS client tự thêm `access_token` vào query string khi connect — dùng chuẩn `@microsoft/signalr` client là đủ, không cần tự xử lý thủ công.
- Sự kiện server bắn ra (subscribe qua `connection.on("<event>", handler)`):

| Event | Payload | Khi nào bắn |
|---|---|---|
| `job:created` | `JobDto` | Ngay sau khi upload thành công, tạo job mới |
| `job:statusChanged` | `{ jobId, status, retryCount?, lastError? }` | Job đổi trạng thái (cancel/retry/worker xử lý xong) |
| `worker:status` | `WorkerStatusDto` | Khi admin pause/resume worker |

Ví dụ JS client tối thiểu:
```js
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("https://<host>:8443/media-upload/hubs/jobs", {
    accessTokenFactory: () => rawToken, // Bearer token của bạn
  })
  .build();

connection.on("job:statusChanged", (data) => console.log(data));
await connection.start();
```

---

## 8. Health check (không cần auth)

```bash
curl -k https://<VPS_IP>:8443/media-upload/health
```
Trả `200 OK` kèm body đơn giản khi service .NET còn sống — dùng cho load balancer/monitoring, không phản ánh trạng thái DB/worker.

---

## 9. Rate limit

Áp dụng theo 2 setting `ratelimit.window_ms` và `ratelimit.max_requests` (mặc định 900000ms / 20 request). Vượt giới hạn → `429 Too Many Requests`. Có thể chỉnh qua `/api/config/settings` (mục 6.3), áp dụng **ngay lập tức** (hot reload).

---

## 10. Luồng tích hợp mẫu (end-to-end)

1. Admin tạo credential riêng cho hệ thống ngoài (`POST /api/credentials`, quyền `upload` + `read_jobs`, `allowedErp` giới hạn đúng target cần dùng) → lưu lại `rawToken`.
2. Hệ thống ngoài gọi `POST /api/upload` với `Authorization: Bearer <rawToken>` kèm file + metadata → nhận `jobId` cho mỗi file.
3. (Tuỳ chọn) Poll `GET /api/jobs/{id}` định kỳ, hoặc subscribe SignalR `job:statusChanged` để biết khi nào job chuyển `Success`/`Failed`.
4. Nếu `status = Failed` và muốn thử lại: `PATCH /api/jobs/{id}/retry` (cần quyền `config`, nên việc retry thường do admin thực hiện qua UI, không phải hệ thống ngoài).

---

## 11. Lỗi tổng quát & format response lỗi

Hầu hết lỗi trả JSON dạng `{ "error": "<message>" }` với HTTP status tương ứng (`400` validate, `401` chưa xác thực, `403` sai quyền/bị disable, `404` không tìm thấy, `429` rate limit, `500` lỗi server không mong muốn). Khi tích hợp, luôn kiểm tra HTTP status trước khi parse `error`/body thành công.
