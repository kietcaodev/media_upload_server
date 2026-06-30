# Hệ Thống Upload Video – Tổng Quan Dự Án

> Nâng cấp từ Node.js → **.NET 10 Web API + React Ant Design**  
> Mục tiêu: Upload video lên NAS, tự động push sang ERP theo khung giờ, dashboard realtime.

---

## Mục Lục

| File | Nội dung |
|------|----------|
| [01-architecture.md](./01-architecture.md) | Kiến trúc tổng thể, sơ đồ luồng dữ liệu |
| [02-database.md](./02-database.md) | Thiết kế Database (EF Core + SQLite/PostgreSQL) |
| [03-backend-api.md](./03-backend-api.md) | Toàn bộ API Endpoints (.NET 10) |
| [04-worker.md](./04-worker.md) | Background Worker – Pause/Resume, TimeWindow Logic |
| [05-frontend.md](./05-frontend.md) | Frontend React + Ant Design – UI/UX Chi Tiết |
| [06-testing.md](./06-testing.md) | Chiến lược kiểm thử – Unit, Integration, QA |
| [07-roadmap.md](./07-roadmap.md) | Lộ trình thực hiện theo tuần |
| [08-authentication.md](./08-authentication.md) | Credentials Management – Bearer Token, Basic Auth, API Key |
| [09-system-settings.md](./09-system-settings.md) | Cấu Hình Hệ Thống qua UI – Upload, Worker, Rate Limit, CORS, NAS |

---

## Tóm Tắt Công Nghệ

| Layer | Công nghệ |
|-------|-----------|
| **Backend** | .NET 10, ASP.NET Core Web API, EF Core 10, SignalR, BCrypt.Net |
| **Database** | PostgreSQL |
| **Frontend** | React 18, Ant Design 5, Zustand, Axios, @microsoft/signalr |
| **Storage** | NAS (giữ nguyên từ hệ thống cũ) |
| **External** | ERP: DND, Zomzem, Zozin (HTTP push) |

---

## Các Tính Năng Chính

- Upload file video/audio lên NAS (giữ nguyên flow cũ, nâng cấp backend)
- **Job Queue**: mỗi file upload tạo 1 job, quản lý vòng đời Pending → Running → Done/Failed
- **TimeWindow Config**: cấu hình khung giờ cho phép push ERP (tránh giờ hành chính)
- **Pause/Resume**: tạm dừng/tiếp tục worker thủ công hoặc tự động theo lịch
- **Dashboard Realtime**: SignalR broadcast trạng thái job trực tiếp lên UI
- **Retry Logic**: tự động retry tối đa 3 lần khi push ERP thất bại
- **ERP Multi-target**: hỗ trợ nhiều endpoint ERP, cấu hình qua UI
- **System Settings UI**: toàn bộ cấu hình vận hành (Upload limits, Worker, Rate Limit, CORS, NAS) được quản lý qua DB và UI — không còn hard-code trong `appsettings.json`
