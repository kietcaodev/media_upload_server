# Kiến Trúc Hệ Thống

## Sơ Đồ Tổng Thể

```
┌─────────────────────────────────────────────────────────────────────┐
│                        CLIENT / BROWSER                             │
│                   React 18 + Ant Design 5                           │
│   ┌─────────────┐  ┌──────────────┐  ┌──────────────────────────┐  │
│   │ Upload Form │  │  Job Config  │  │  Dashboard (Realtime)    │  │
│   │             │  │  TimeWindow  │  │  Stats + Table + Chart   │  │
│   └──────┬──────┘  └──────┬───────┘  └────────────┬─────────────┘  │
└──────────┼────────────────┼───────────────────────┼────────────────┘
           │   HTTP/REST    │   HTTP/REST            │  SignalR WS
           ▼                ▼                        ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      .NET 10 WEB API                                │
│  ┌─────────────┐  ┌─────────────┐  ┌──────────────┐               │
│  │ Upload Ctrl │  │ Config Ctrl │  │ Dashboard    │               │
│  └──────┬──────┘  └──────┬──────┘  │ Ctrl         │               │
│         │                │         └──────┬───────┘               │
│         ▼                ▼                │                        │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                   Application Layer                         │   │
│  │   UploadService  │  JobService  │  ConfigService            │   │
│  └──────────────────┬──────────────────────────────────────────┘   │
│                     │                                               │
│  ┌──────────────────▼──────────────────────────────────────────┐   │
│  │               Background Worker                             │   │
│  │   TimeWindowChecker → JobDispatcher → ErpPushService        │   │
│  │                              │                              │   │
│  │                    SignalR Hub (broadcast)                  │   │
│  └──────────────────────────────────────────────────────────────┘   │
└──────────┬────────────────────────────┬──────────────────────────────┘
           │                            │
    ┌──────▼──────┐              ┌──────▼──────────────────────────┐
    │ PostgreSQL  │              │         NAS Storage             │
    │             │              │  /mnt/nas/uploads/              │
    │             │              │  /mnt/nas/logs/                 │
    └─────────────┘              └─────────────────────────────────┘
                                          │
                              ┌───────────▼───────────┐
                              │    ERP External APIs   │
                              │  DND / Zomzem / Zozin  │
                              └───────────────────────┘
```

---

## Luồng Dữ Liệu Upload

```
[User chọn file]
      │
      ▼
[POST /api/upload] ──→ [Validate: size, type, disk space]
      │
      ▼
[Lưu file lên NAS] ──→ /mnt/nas/uploads/{userId}_{customerCode}_{filename}
      │
      ▼
[Tạo UploadJob – Status: Pending] ──→ DB
      │
      ▼
[Return 200: { fileId, jobId, status }] ──→ Frontend
      │
      ▼
[SignalR push] ──→ Dashboard cập nhật realtime
```

---

## Luồng Worker – Push ERP

```
[Worker tick mỗi 30s]
        │
        ▼
[Kiểm tra TimeWindowConfig]
        │
   ┌────┴─────────────────┐
   │ Trong khung giờ?     │
   └────┬─────────────────┘
     YES │              NO │
        ▼                  ▼
[Lấy jobs Pending]    [Log: ngoài giờ, sleep]
        │
        ▼
[Kiểm tra MaxConcurrent (SemaphoreSlim)]
        │
        ▼
[Push ERP qua HttpClient]
        │
   ┌────┴────────────┐
   │ Response?       │
   └────┬────────────┘
   200 ↙       ↘ Error
      ▼            ▼
[Status: Done] [RetryCount++]
[SignalR push]      │
               RetryCount < 3?
                YES ↙     ↘ NO
            [Retry]   [Status: Failed]
           [Delay 5m] [SignalR push]
```

---

## Cấu Trúc Solution .NET 10

```
MediaUpload.sln
│
├── MediaUpload.API/                   ← Entry point, Controllers, Hubs, Middleware
│   ├── Controllers/
│   │   ├── UploadController.cs
│   │   ├── JobsController.cs
│   │   ├── ConfigController.cs
│   │   ├── WorkerController.cs
│   │   └── DashboardController.cs
│   ├── Hubs/
│   │   └── JobHub.cs                  ← SignalR
│   ├── Middlewares/
│   │   ├── RateLimitMiddleware.cs
│   │   └── ErrorHandlingMiddleware.cs
│   ├── appsettings.json
│   └── Program.cs
│
├── MediaUpload.Application/           ← Business logic, Services, Workers
│   ├── Services/
│   │   ├── UploadService.cs
│   │   ├── JobService.cs
│   │   ├── ConfigService.cs
│   │   └── DashboardService.cs
│   ├── Workers/
│   │   ├── JobWorkerService.cs        ← IHostedService
│   │   ├── TimeWindowChecker.cs
│   │   └── ErpPushService.cs
│   └── DTOs/
│       ├── UploadDto.cs
│       ├── JobDto.cs
│       └── ConfigDto.cs
│
├── MediaUpload.Domain/                ← Entities, Enums, Interfaces
│   ├── Entities/
│   │   ├── UploadJob.cs
│   │   ├── TimeWindowConfig.cs
│   │   └── ErpEndpointConfig.cs
│   ├── Enums/
│   │   └── JobStatus.cs
│   └── Interfaces/
│       ├── IJobRepository.cs
│       └── IConfigRepository.cs
│
└── MediaUpload.Infrastructure/        ← EF Core, Repositories, HTTP clients
    ├── Data/
    │   ├── AppDbContext.cs
    │   └── Migrations/
    ├── Repositories/
    │   ├── JobRepository.cs
    │   └── ConfigRepository.cs
    └── ExternalClients/
        └── ErpHttpClient.cs
```

---

## Cấu Trúc Frontend React

```
media-upload-ui/
├── src/
│   ├── pages/
│   │   ├── Dashboard/
│   │   │   ├── index.tsx              ← Trang chính dashboard
│   │   │   ├── StatsCards.tsx         ← 4 cards: Pending/Running/Done/Failed
│   │   │   ├── JobsTable.tsx          ← Bảng jobs realtime
│   │   │   └── TimelineChart.tsx      ← Biểu đồ theo giờ/ngày
│   │   ├── Config/
│   │   │   ├── index.tsx
│   │   │   ├── TimeWindowForm.tsx     ← Form tạo/sửa khung giờ
│   │   │   ├── TimeWindowList.tsx     ← Danh sách khung giờ
│   │   │   └── ErpConfigList.tsx      ← Cấu hình ERP endpoints
│   │   └── Upload/
│   │       └── index.tsx              ← Upload form (Ant Design Dragger)
│   ├── components/
│   │   ├── WorkerStatusBar.tsx        ← Banner trạng thái worker
│   │   └── JobStatusTag.tsx           ← Tag màu theo status
│   ├── services/
│   │   ├── api.ts                     ← Axios instance + interceptors
│   │   ├── uploadService.ts
│   │   ├── jobService.ts
│   │   ├── configService.ts
│   │   └── signalrService.ts          ← SignalR hub connection
│   ├── store/
│   │   ├── jobStore.ts                ← Zustand: jobs state
│   │   ├── workerStore.ts             ← Zustand: worker status
│   │   └── configStore.ts             ← Zustand: configs
│   ├── App.tsx
│   └── main.tsx
└── package.json
```
