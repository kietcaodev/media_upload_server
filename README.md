# Media Upload System

## Cấu trúc project

```
backend/                    ← .NET 10 Web API
  MediaUpload.Domain/       ← Entities, Enums, Interfaces
  MediaUpload.Infrastructure/ ← EF Core, Repositories, AES Encryption
  MediaUpload.Application/  ← Services, Worker (BackgroundService), DTOs
  MediaUpload.API/          ← Controllers, Middleware, SignalR Hub

frontend/
  media-upload-ui/          ← React 18 + TypeScript + Ant Design 5
```

## Yêu cầu

- .NET 10 SDK
- PostgreSQL 15+
- Node.js 20+

## Setup Backend

### 1. Tạo AES Key
```bash
node -e "console.log(require('crypto').randomBytes(32).toString('base64'))"
```

### 2. Cấu hình `backend/MediaUpload.API/appsettings.json`
```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=media_upload;Username=postgres;Password=YOUR_PG_PASSWORD"
  },
  "Encryption": {
    "AesKey": "BASE64_32_BYTES_KEY_FROM_STEP_1"
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:5173"]
  }
}
```

### 3. Migrate & Run
```bash
cd backend
dotnet ef database update --project MediaUpload.Infrastructure --startup-project MediaUpload.API
dotnet run --project MediaUpload.API
```
API chạy tại: http://localhost:5000  
Swagger UI: http://localhost:5000/swagger

## Setup Frontend

```bash
cd frontend/media-upload-ui
# Sửa .env nếu backend ở port khác
# VITE_API_URL=http://localhost:5000
npm run dev
```
UI chạy tại: http://localhost:5173

## Lần đầu chạy

1. Tạo credential đầu tiên bằng API trực tiếp (vì tất cả endpoint đều yêu cầu auth):

```bash
# Tạo credential admin qua Swagger (/api/credentials)
# Hoặc dùng script seed sau:
curl -X POST http://localhost:5000/api/credentials \
  -H "Authorization: Bearer INITIAL_SETUP_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name":"admin","authType":0,"canUpload":true,"canReadJobs":true,"canConfig":true,"allowedErp":""}'
```

> **Lưu ý**: Lần đầu tiên cần tắt AuthMiddleware tạm thời để tạo credential đầu tiên,
> hoặc thêm 1 seed credential trong AppDbContext.

## Features

- ✅ Upload video → tạo job → worker tự động push lên ERP (DND/ZOMZEM/ZOZIN)
- ✅ Time Window: chỉ push trong khung giờ cấu hình (GMT+7)
- ✅ Retry tự động với exponential back-off (delay × retryCount)
- ✅ Realtime dashboard qua SignalR
- ✅ Multi-auth: Bearer Token, Basic Auth, API Key
- ✅ BCrypt hash, token chỉ hiện 1 lần khi tạo
- ✅ Tất cả config qua DB/UI – không hardcode
- ✅ Timestamps: UTC trong DB, GMT+7 hiển thị

## API Endpoints

| Group          | Method | Path                          |
|----------------|--------|-------------------------------|
| Upload         | POST   | /api/upload                   |
| Jobs           | GET    | /api/jobs                     |
| Jobs           | PATCH  | /api/jobs/{id}/cancel         |
| Jobs           | PATCH  | /api/jobs/{id}/retry          |
| Dashboard      | GET    | /api/dashboard/stats          |
| Dashboard      | GET    | /api/dashboard/timeline       |
| Worker         | GET    | /api/worker/status            |
| Worker         | POST   | /api/worker/pause             |
| Worker         | POST   | /api/worker/resume            |
| Time Windows   | CRUD   | /api/config/timewindows       |
| ERP Config     | GET/PUT| /api/config/erp               |
| Settings       | GET    | /api/config/settings          |
| Settings       | PATCH  | /api/config/settings          |
| Credentials    | CRUD   | /api/credentials              |
| Credentials    | POST   | /api/credentials/{id}/rotate  |
| SignalR Hub    | WS     | /hubs/jobs                    |
