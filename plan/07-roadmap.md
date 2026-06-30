# Lộ Trình Thực Hiện

> Tổng thời gian dự kiến: **4 tuần**  
> Mỗi tuần ≈ 5 ngày làm việc

---

## TUẦN 1 – Backend Foundation

**Mục tiêu:** Solution .NET 10 chạy được, DB migrate, Upload API hoạt động.

| Ngày | Task | Assignee |
|------|------|----------|
| **T2** | Setup solution .NET 10 (4 projects). Cài EF Core, SQLite, Swashbuckle. | Backend Dev |
| **T3** | Tạo Domain Entities + Enums. Viết AppDbContext + Migration InitialCreate. Seed data TimeWindow mặc định. | Backend Dev |
| **T4** | Viết UploadController + UploadService: nhận file, validate, lưu NAS, tạo UploadJob. | Backend Dev |
| **T5** | Viết FilesController: GET list + DELETE. Cấu hình Rate Limiter, CORS, Error Middleware. | Backend Dev |
| **T6** | Test thủ công Upload API bằng Postman. Fix bugs. Viết unit test UploadService. | Backend Dev |

**Deliverable:** API upload nhận file, lưu NAS, tạo job Pending trong DB. Swagger UI chạy được.

---

## TUẦN 2 – Worker & Job Control

**Mục tiêu:** Worker chạy, push ERP theo TimeWindow, Pause/Resume hoạt động, SignalR live.

| Ngày | Task | Assignee |
|------|------|----------|
| **T2** | Viết `WorkerStateService` (singleton). Viết `TimeWindowChecker`. | Backend Dev |
| **T3** | Viết `ErpPushService`: HTTP push + retry logic + AES token decrypt. | Backend Dev |
| **T4** | Viết `JobWorkerService` (IHostedService): Channel producer/consumer, SemaphoreSlim. | Backend Dev |
| **T5** | Viết `JobHub` (SignalR). Tích hợp broadcast vào ErpPushService. Viết `WorkerController` (pause/resume). | Backend Dev |
| **T6** | Test end-to-end: upload → worker tự push ERP (dùng mock server). Viết unit test TimeWindowChecker + ErpPushService. | Backend Dev |

**Deliverable:** Worker tự động push job theo khung giờ. Pause/Resume API hoạt động. SignalR emit event khi job đổi status.

---

## TUẦN 3 – Frontend Core

**Mục tiêu:** Dashboard và realtime SignalR hiển thị trực quan.

| Ngày | Task | Assignee |
|------|------|----------|
| **T2** | Setup React + Vite + TypeScript + Ant Design 5. Cấu hình Axios (base URL, interceptors). Layout App (Header, Sidebar, Router). | Frontend Dev |
| **T3** | Viết `signalrService`, `workerStore`, `jobStore` (Zustand). Kết nối SignalR với backend. | Frontend Dev |
| **T4** | Trang Dashboard: `StatsCards`, `JobsTable` (có filter, phân trang). WorkerStatusBar. | Frontend Dev |
| **T5** | `TimelineChart` (biểu đồ theo giờ/ngày). Các actions trong bảng: Pause/Resume/Retry/Cancel job. | Frontend Dev |
| **T6** | Test Dashboard realtime: upload → xem job xuất hiện ngay, status đổi realtime. Fix bugs. | Fullstack Dev |

**Deliverable:** Dashboard hiển thị jobs realtime, stats cập nhật tự động qua SignalR.

---

## TUẦN 4 – Config UI + Testing + Deploy

**Mục tiêu:** Config UI hoàn chỉnh, test đầy đủ, sẵn sàng deploy.

| Ngày | Task | Assignee |
|------|------|----------|
| **T2** | Trang Config – Tab TimeWindow: `TimeWindowList`, `TimeWindowForm` (Modal). Calendar preview khung giờ. | Frontend Dev |
| **T3** | Trang Config – Tab ERP: `ErpConfigList`, form sửa URL/Token. Trang Upload: Ant Design Dragger + progress per file. | Frontend Dev |
| **T4** | Viết integration test API (WebApplicationFactory). Viết frontend test (Jest + RTL + MSW). | Backend Dev + Frontend Dev |
| **T5** | Manual QA toàn bộ checklist 10 test case. Load test k6 (upload đồng thời). Fix bugs. | QA |
| **T6** | Review code, cleanup. Viết `docker-compose.yml` (backend + frontend + PostgreSQL). Demo cho khách hàng. | Backend Dev |

**Deliverable:** Hệ thống hoàn chỉnh, test pass, sẵn sàng chạy production.

---

## Gantt Chart

```
Task                              T1  T2  T3  T4
─────────────────────────────────────────────────
Backend Foundation                ████
Database + Migrations             ████
Upload API                        ████
Worker + TimeWindow               ████
ErpPushService + Retry                ████
SignalR + WorkerController            ████
Frontend Setup + Stores               ████
Dashboard UI                          ████
Dashboard Realtime                        ████
Config UI (TimeWindow + ERP)              ████
Upload Page                               ████
Unit Tests                            ████████
Integration Tests                             ████
QA + Bug Fix                                  ████
Docker + Deploy                               ████
```

---

## Điều Kiện Tiên Quyết

Trước khi bắt đầu implement cần chuẩn bị:

- [ ] Cài .NET 10 SDK (`dotnet --version` ≥ 10.0)
- [ ] Cài Node.js 20+ (`node --version` ≥ 20)
- [ ] Có quyền truy cập NAS mount point `/mnt/nas/uploads`
- [ ] Lấy token API của DND / Zomzem / Zozin từ team
- [ ] Cấu hình AES encryption key (lưu trong appsettings secrets, không commit git)

---

## Thứ Tự Ưu Tiên Nếu Trễ Timeline

1. **Bắt buộc (MVP):** Upload API + Worker + Dashboard realtime
2. **Quan trọng:** TimeWindow Config UI + Pause/Resume UI  
3. **Nice-to-have:** Timeline chart, Load test, Docker compose

---

## Cấu Trúc File Config

> Chỉ còn 2 mục trong `appsettings.json`. Toàn bộ config vận hành được quản lý qua UI (xem [09-system-settings.md](./09-system-settings.md)).

```json
// appsettings.json (backend) – CHỈ còn 2 mục này
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=media_upload;Username=postgres;Password=yourpassword"
  },
  "Encryption": {
    "AesKey": "** set via environment variable ENCRYPTION__AESKEY **"
  }
}
```

```env
# frontend .env
VITE_API_BASE_URL=http://localhost:5000
VITE_SIGNALR_HUB_URL=http://localhost:5000/hubs/jobs
```

> Các config sau đây được lưu trong DB và sửa qua UI:
> NAS paths, Upload limits, File types, Worker tick/retry, Rate limit, CORS origins
