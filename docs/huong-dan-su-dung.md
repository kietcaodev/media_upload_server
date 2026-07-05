# Hướng dẫn sử dụng Media Upload System

Tài liệu này mô tả **từng màn hình, từng trường/nút bấm** đúng như giao diện thực tế (web UI React tại `frontend/media-upload-ui`), chạy trên backend .NET tại `backend/MediaUpload.API`.

> URL truy cập production: `https://<VPS_IP>:8443/media-upload/`
> (chứng chỉ TLS tự ký — trình duyệt sẽ cảnh báo "Not secure", bấm **Advanced → Proceed** để tiếp tục lần đầu.)

---

## 1. Đăng nhập

Màn hình đầu tiên khi mở app (nếu chưa đăng nhập, hoặc phiên đăng nhập cũ không còn hợp lệ).

| Trường | Mô tả |
|---|---|
| **Tên đăng nhập** | Username của credential loại **Basic** (mặc định `admin`) |
| **Mật khẩu** | Mật khẩu tương ứng |

- Bấm **Đăng nhập**: hệ thống gọi thử API `GET /api/dashboard/stats` để xác minh ngay tại chỗ. Sai tên đăng nhập/mật khẩu → hiện thông báo đỏ "Sai tên đăng nhập hoặc mật khẩu".
- Đăng nhập thành công → token được lưu ở `localStorage` (key `auth_token`, `auth_type`) và gửi kèm mọi request sau đó dưới dạng header `Authorization: Basic ...`.
- Góc trên bên phải mọi trang có nút **Đăng xuất** — xoá token khỏi trình duyệt và quay lại màn hình đăng nhập.

> ⚠️ Mật khẩu admin mặc định (seed sẵn trong code) là `MediaUploadAdmin2026!ChangeMe`. Sau lần đăng nhập đầu tiên, **luôn vào Cấu hình → Credentials/Auth → Rotate** để đổi sang mật khẩu riêng.

---

## 2. Sidebar (menu bên trái)

4 mục cố định:

| Icon | Mục | Trang tương ứng |
|---|---|---|
| 📊 | **Dashboard** | Tổng quan job + điều khiển worker |
| ⬆️ | **Upload** | Upload video/ảnh và tạo job |
| 📋 | **Jobs** | Danh sách job đầy đủ, lọc theo trạng thái |
| ⚙️ | **Cấu hình** | Time Windows / ERP Endpoints / Credentials / System Settings |

Sidebar có thể thu gọn (icon-only) khi màn hình nhỏ hơn breakpoint `lg`.

---

## 3. Dashboard

### 3.1 Thanh công cụ trên cùng
- **Refresh**: tải lại số liệu thống kê (`stats`) và bảng job trang hiện tại.
- **Pause Worker** / **Resume Worker**: nút đổi động theo trạng thái worker hiện tại.
  - Nếu worker đang chạy → hiện nút đỏ **Pause Worker** (khi bấm, gửi lý do mặc định `"Paused by admin"`).
  - Nếu worker đang tạm dừng → hiện nút xanh **Resume Worker**.

### 3.2 Banner cảnh báo (chỉ hiện khi có điều kiện tương ứng)
- 🟠 `Worker đang tạm dừng: <lý do>` — khi `workerStatus.isPaused = true`.
- 🔵 `Ngoài time window – worker sẽ không xử lý job cho đến khi vào window.` — khi hiện tại **không** nằm trong bất kỳ Time Window nào đang bật (xem mục 5.1) và worker không bị pause thủ công.

### 3.3 6 thẻ thống kê (Statistic cards)
| Thẻ | Ý nghĩa |
|---|---|
| **Tổng** | Tổng số job trong hệ thống |
| **Pending** | Job đang chờ worker xử lý |
| **Processing** | Job đang được worker đẩy lên ERP |
| **Success** | Job đã push ERP thành công |
| **Failed** | Job thất bại (đã hết số lần retry hoặc lỗi không thể retry) |
| **Worker** | `Active (n)` — số worker đang chạy song song, hoặc `Paused` nếu đang tạm dừng |

### 3.4 Bảng "Jobs gần nhất"
Cột: **File** (tên gốc), **ERP** (target: DND/ZOMZEM/ZOZIN...), **Status** (tag màu theo trạng thái), **Retry** (`đã dùng/tối đa`), **Created** (giờ tạo, quy đổi theo timezone `system.timezone`), **Lỗi** (thông báo lỗi cuối cùng nếu có).

Có phân trang (page size cố định theo mặc định của bảng). Dữ liệu job trong bảng này **cập nhật realtime qua SignalR** (`/hubs/jobs`) — job mới/đổi trạng thái sẽ tự động phản ánh mà không cần bấm Refresh.

---

## 4. Upload Video

Form nhập metadata + kéo-thả file, gọi `POST /api/upload` để tạo job.

### 4.1 Các trường metadata

| Trường | Bắt buộc | Ghi chú |
|---|---|---|
| **ERP Target** | ✅ | Dropdown — chỉ liệt kê các ERP target đang **Enabled** (cấu hình ở tab ERP Endpoints), tránh chọn nhầm target chưa cấu hình URL/token |
| **Order ID** | không bắt buộc trên UI (nhưng ERP thật cần để build URL và push) | |
| **NVKT ID** | không bắt buộc trên UI | |
| **Flow ID** | không bắt buộc trên UI | |
| **Longitude** | không bắt buộc trên UI | Kinh độ |
| **Latitude** | không bắt buộc trên UI | Vĩ độ |

### 4.2 Khu vực "Video Files"
- Kéo-thả hoặc click để chọn file.
- Định dạng hỗ trợ hiển thị trên UI: **MP4, AVI, MOV, MKV, WMV, FLV** (accept attribute của Dragger). Danh sách đầy đủ được BE chấp nhận (bao gồm audio/ảnh) nằm ở **System Settings → Upload → allowed_extensions**.
- Có thể chọn nhiều file cùng lúc (`multiple`).
- Mỗi file chọn xong hiện ngay trong danh sách bên dưới khung kéo-thả, có thể bấm (x) để bỏ bớt trước khi upload.

### 4.3 Nút "Upload & Tạo Job"
- Validate: phải có **ít nhất 1 file** và đã chọn **ERP Target**, nếu không sẽ báo lỗi tương ứng (`Vui lòng chọn ít nhất 1 file` / lỗi required của Form).
- Khi bấm: gửi `multipart/form-data` gồm toàn bộ file + các trường metadata lên `POST /api/upload`.
- Thành công: hiện thông báo xanh `Upload thành công n file(s). Job đã được tạo.`, tự reset form và xoá danh sách file đã chọn.
- Thất bại: hiện thông báo đỏ lấy từ `error` trong response BE (hoặc `Upload thất bại` nếu không xác định được).
- Mỗi file upload thành công sẽ tạo **1 UploadJob** riêng ở trạng thái `Pending`, xuất hiện ngay trong Dashboard/Jobs (qua SignalR).

---

## 5. Jobs

Danh sách **đầy đủ** toàn bộ job (không giới hạn "gần nhất" như Dashboard), có bộ lọc.

### 5.1 Thanh lọc
- **Dropdown lọc status**: Tất cả / Pending / Processing / Success / Failed / Cancelled.
- **Refresh**: tải lại trang hiện tại theo bộ lọc đang chọn.

### 5.2 Cột trong bảng
File, ERP, **Size** (MB), Status, Retry (`đã dùng/tối đa`), **Tạo lúc**, **Hoàn thành** (thời điểm job kết thúc, trống nếu chưa xong), **Lỗi**, và cột **Thao tác**:

| Điều kiện trạng thái | Thao tác khả dụng |
|---|---|
| `Failed` | **Retry** (có xác nhận Popconfirm) — đưa job về hàng đợi để worker thử lại |
| `Pending` hoặc `Processing` | **Huỷ** (có xác nhận Popconfirm, màu đỏ) — chuyển job sang `Cancelled` |

---

## 6. Cấu hình (Config)

Trang có 4 tab. Mọi thay đổi ở đây yêu cầu credential có quyền **Config** (`canConfig = true`).

### 6.1 Tab "Time Windows"

Quản lý các khung giờ cho phép worker được phép **đẩy job lên ERP** (không ảnh hưởng việc upload file — file vẫn lưu được ngoài giờ này, chỉ job không được xử lý cho tới khi vào khung giờ).

- Nút **Thêm Time Window** → mở modal tạo mới.
- Bảng hiển thị: **Tên**, **Từ** / **Đến** (giờ, định dạng `HH:mm`, theo giờ GMT+7), **Ngày** (các tag T2–CN áp dụng), **Bật** (ON/OFF), và 2 nút **Sửa** / **Xoá** (Xoá có xác nhận).
- Modal Thêm/Sửa gồm:
  - **Tên** (bắt buộc)
  - **Giờ bắt đầu (GMT+7)** / **Giờ kết thúc (GMT+7)** — chọn giờ:phút (bắt buộc)
  - **Ngày trong tuần** — chọn nhiều (multi-select: T2, T3, T4, T5, T6, T7, CN), bắt buộc
  - **Bật** — switch on/off

> Nếu **không có Time Window nào đang bật** khớp với thời điểm hiện tại, Dashboard sẽ hiện banner cảnh báo "Ngoài time window" và job `Pending` sẽ bị giữ lại, không được worker đẩy lên ERP.

### 6.2 Tab "ERP Endpoints"

Quản lý danh sách công ty ERP nhận video (mỗi công ty = 1 "target": DND / ZOMZEM / ZOZIN hoặc tự thêm target mới).

- Nút **Thêm ERP Target** → mở modal tạo mới, gồm:
  - **Mã công ty (target)** — tự động viết hoa (vd nhập `dnd` → lưu `DND`)
  - Danh sách **Gợi ý** nhanh (tag bấm để tự điền sẵn URL đã biết): `DND`, `ZOMZEM`, `ZOZIN`
  - **URL** — base URL ERP (không cần tự thêm `/order_id`, hệ thống tự nối vào cuối khi gọi thật)
  - **Token** — token Bearer ERP cấp cho công ty này (bắt buộc lúc tạo mới, lưu mã hoá AES trong DB)
  - **Enabled** — bật/tắt target này
- Danh sách target hiện có ở cột trái (tag màu xanh = đang bật, đỏ = đang tắt); bấm vào 1 target để load form cấu hình bên phải.
- Form cấu hình bên phải cho target đang chọn:
  - **URL** — sửa trực tiếp
  - **Token mới (bỏ trống = giữ nguyên)** — chỉ nhập khi muốn **thay** token cũ; phần "Hiện tại" hiển thị 8 ký tự đầu của token cũ (`tokenPrefix`) để nhận diện, token đầy đủ **không bao giờ** hiển thị lại sau khi tạo.
  - **Enabled** — bật/tắt
  - Nút **Lưu**

### 6.3 Tab "Credentials / Auth"

Quản lý các thông tin xác thực (Bearer token / Basic username-password / API Key) dùng để gọi API hệ thống (kể cả tài khoản đăng nhập web UI).

- Nút **Tạo Credential** → mở modal, gồm:
  - **Tên** (bắt buộc, để nhận diện, vd "admin (web login)")
  - **Loại auth** (bắt buộc): `Bearer` / `Basic` / `ApiKey`
  - **Username** (chỉ áp dụng khi chọn Basic auth)
  - **Quyền Upload** / **Quyền Read Jobs** (mặc định bật) / **Quyền Config** — switch
  - **ERP được phép (rỗng = tất cả)** — nhập danh sách target cách nhau dấu phẩy để giới hạn credential này chỉ được upload cho các ERP đó
- Sau khi tạo, modal **"⚠️ Lưu token – hiện chỉ 1 lần!"** hiện ra với raw token đầy đủ (copy được) — **chỉ hiển thị đúng 1 lần**, phải bật switch "Đã lưu" mới đóng được modal.
- Bảng danh sách credential: **Tên**, **Loại**, **Prefix** (8 ký tự đầu token), **Username**, **Permissions** (tag `upload`/`read`/`config`), **Status** (ON/OFF), **Dùng lần cuối**, và thao tác:
  - **Rotate** (có xác nhận) — sinh token/secret mới, hiện popup token mới 1 lần duy nhất (khuyên dùng ngay sau khi cài đặt lần đầu, đặc biệt với credential `admin`).
  - **Xoá** (có xác nhận, icon thùng rác).

### 6.4 Tab "System Settings"

Toàn bộ tham số vận hành hệ thống (đọc/ghi trực tiếp bảng `SystemSettings` trong DB), nhóm theo 6 khối:

| Nhóm | Field | Ý nghĩa |
|---|---|---|
| **NAS / Đường dẫn** | `logs_dir` | Thư mục lưu log |
| | `min_free_space_bytes` | Dung lượng trống tối thiểu (bytes) trước khi từ chối upload |
| | `upload_dir` | Thư mục lưu file upload trên server/NAS |
| | `video_path_prefix` | Prefix đường dẫn video khi gửi lên ERP (`list_video_path[]`) |
| **Upload** | `allowed_extensions` | Các đuôi file video/audio/ảnh được phép, phân cách bằng dấu phẩy |
| | `max_file_size_bytes` | Dung lượng tối đa mỗi file (bytes) |
| | `max_files_per_request` | Số file tối đa mỗi lần upload |
| **Worker** | `max_concurrent` | Số job chạy song song tối đa |
| | `max_retry` | Số lần retry tối đa trước khi chuyển `Failed` |
| | `retry_delay_minutes` | Thời gian chờ giữa các lần retry (phút) |
| | `tick_interval_seconds` | Chu kỳ worker kiểm tra hàng đợi job (giây) |
| **Rate Limit** | `max_requests` | Số request tối đa trong 1 cửa sổ thời gian |
| | `window_ms` | Độ dài cửa sổ rate limit (mili-giây) |
| **CORS** | `allowed_origins` | Danh sách origin được phép gọi CORS, phân cách bằng dấu phẩy |
| **Hệ thống** | `timezone` | Timezone hiển thị toàn bộ thời gian trên UI (IANA ID, vd `Asia/Ho_Chi_Minh`) |

- Mỗi field có tag nhỏ cạnh tên:
  - 🟢 **hot** — áp dụng ngay lập tức, không cần restart service.
  - 🟠 **restart** — chỉ có hiệu lực sau khi restart service (`system.timezone`).
- Mỗi field có nút **↺ (Reset)** riêng — đưa giá trị đó về mặc định gốc trong code (dùng khi nhập sai/muốn quay lại default).
- Nút **Lưu tất cả cài đặt** ở cuối trang — lưu toàn bộ thay đổi trong tất cả các nhóm cùng lúc (gọi `PATCH /api/config/settings`).

> ⚠️ Lưu ý vận hành: các giá trị mặc định (default) của các field này cũng được định nghĩa cứng trong code backend (EF Core seed data). Nếu đội dev đẩy 1 bản deploy có kèm migration "cập nhật giá trị mặc định", giá trị bạn **đã tự chỉnh tay** trên UI này có thể bị ghi đè trở lại về default mới — nên kiểm tra lại tab này sau mỗi lần deploy có nhắc tới thay đổi System Settings.

---

## 7. Câu hỏi thường gặp

**Đăng nhập được nhưng mọi trang đều báo lỗi 401?**
→ Phiên đăng nhập cũ (lưu trong trình duyệt) không còn khớp với mật khẩu hiện tại trên server (ví dụ sau khi admin bị rotate/reset mật khẩu). Bấm **Đăng xuất** rồi đăng nhập lại bằng mật khẩu hiện hành.

**Upload báo "Vui lòng chọn ít nhất 1 file" dù đã chọn file?**
→ Đây từng là lỗi ở phiên bản cũ (đã fix) do file chọn không được lưu đúng định dạng nội bộ. Nếu vẫn gặp, kiểm tra đã pull code mới nhất và đã build lại frontend (`npm run build`) chưa.

**Vừa sửa System Settings nhưng effect không đổi?**
→ Kiểm tra tag `hot`/`restart` cạnh field đó — field `restart` cần khởi động lại service (`systemctl restart mediaupload-dotnet-api`) mới có hiệu lực.

**ERP báo lỗi 404 khi push job?**
→ Kiểm tra field **URL** ở tab ERP Endpoints — hệ thống tự nối `/{order_id}` vào cuối URL cấu hình khi gọi thật, đảm bảo route phía ERP đúng dạng `POST {URL}/{order_id}`.
