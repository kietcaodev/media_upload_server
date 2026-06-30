# Thiết Kế Database

> **ORM:** Entity Framework Core 10  
> **Database:** PostgreSQL (cả dev lẫn prod)

---

## Bảng 1: `UploadJobs`

Lưu thông tin từng file đã upload và trạng thái job push ERP.

```sql
CREATE TABLE UploadJobs (
    Id              TEXT        PRIMARY KEY,   -- GUID
    FileId          TEXT        NOT NULL UNIQUE,
    OriginalName    TEXT        NOT NULL,
    SavedPath       TEXT        NOT NULL,      -- /mnt/nas/uploads/...
    MimeType        TEXT        NOT NULL,      -- video/mp4, audio/mpeg
    SizeBytes       INTEGER     NOT NULL,
    CustomerCode    TEXT        NOT NULL,
    UserId          TEXT        NOT NULL,
    ErpTarget       TEXT        NOT NULL,      -- DND | Zomzem | Zozin
    Status          INTEGER     NOT NULL DEFAULT 0,
    -- 0=Pending, 1=Running, 2=Paused, 3=Done, 4=Failed, 5=Cancelled
    RetryCount      INTEGER     NOT NULL DEFAULT 0,
    MaxRetry        INTEGER     NOT NULL DEFAULT 3,
    ScheduledAt     TEXT,                      -- ISO8601 datetime
    StartedAt       TEXT,
    CompletedAt     TEXT,
    ErrorMessage    TEXT,
    UploadIp        TEXT,
    CreatedAt       TEXT        NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX idx_jobs_status    ON UploadJobs(Status);
CREATE INDEX idx_jobs_erp       ON UploadJobs(ErpTarget);
CREATE INDEX idx_jobs_created   ON UploadJobs(CreatedAt DESC);
```

### Entity C#

```csharp
// Domain/Entities/UploadJob.cs
public class UploadJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileId { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string SavedPath { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ErpTarget { get; set; } = string.Empty;
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public int RetryCount { get; set; } = 0;
    public int MaxRetry { get; set; } = 3;
    public DateTime? ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? UploadIp { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

---

## Bảng 2: `TimeWindowConfigs`

Định nghĩa các khung giờ được phép push ERP.

```sql
CREATE TABLE TimeWindowConfigs (
    Id              INTEGER     PRIMARY KEY AUTOINCREMENT,
    Name            TEXT        NOT NULL,
    DaysOfWeek      TEXT        NOT NULL,  -- "1,2,3,4,5" (1=Mon, 7=Sun)
    StartTime       TEXT        NOT NULL,  -- "08:00"
    EndTime         TEXT        NOT NULL,  -- "12:00"
    IsEnabled       INTEGER     NOT NULL DEFAULT 1,
    MaxConcurrent   INTEGER     NOT NULL DEFAULT 3,
    ErpTarget       TEXT,                  -- NULL = áp dụng tất cả ERP
    CreatedAt       TEXT        NOT NULL DEFAULT (datetime('now')),
    UpdatedAt       TEXT        NOT NULL DEFAULT (datetime('now'))
);
```

### Entity C#

```csharp
// Domain/Entities/TimeWindowConfig.cs
public class TimeWindowConfig
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DaysOfWeek { get; set; } = "1,2,3,4,5"; // Mon–Fri

    // StartTime và EndTime lưu dưới dạng "HH:mm" biểu diễn giờ địa phương GMT+7
    // Không bao giờ lưu UTC vào đây — người dùng nhìn thấy và nhập theo giờ GMT+7
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int MaxConcurrent { get; set; } = 3;
    public string? ErpTarget { get; set; } // null = all

    /// <summary>
    /// Kiểm tra khung giờ có đang active không.
    /// <param name="nowLocal">Thời điểm hiện tại đã được convert sang giờ địa phương (GMT+7) bởi caller.</param>
    /// </summary>
    public bool IsActive(DateTime nowLocal)
    {
        if (!IsEnabled) return false;
        var days = DaysOfWeek.Split(',').Select(int.Parse).ToList();
        // DayOfWeek: Sunday=0 → map sang 7, Monday=1..Saturday=6
        var dow = (int)nowLocal.DayOfWeek == 0 ? 7 : (int)nowLocal.DayOfWeek;
        if (!days.Contains(dow)) return false;
        var time = TimeOnly.FromDateTime(nowLocal);
        return time >= StartTime && time <= EndTime;
    }
}
```

---

## Bảng 3: `ErpEndpointConfigs`

Cấu hình các endpoint ERP, có thể thêm/sửa qua UI.

```sql
CREATE TABLE ErpEndpointConfigs (
    Id              INTEGER     PRIMARY KEY AUTOINCREMENT,
    Name            TEXT        NOT NULL UNIQUE,  -- DND, Zomzem, Zozin
    Url             TEXT        NOT NULL,
    Token           TEXT        NOT NULL,          -- Encrypted AES-256
    IsEnabled       INTEGER     NOT NULL DEFAULT 1,
    TimeoutSeconds  INTEGER     NOT NULL DEFAULT 30,
    CreatedAt       TEXT        NOT NULL DEFAULT (datetime('now')),
    UpdatedAt       TEXT        NOT NULL DEFAULT (datetime('now'))
);
```

### Entity C#

```csharp
// Domain/Entities/ErpEndpointConfig.cs
public class ErpEndpointConfig
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty; // stored encrypted
    public bool IsEnabled { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

---

## Bảng 5: `SystemSettings`

Lưu toàn bộ cấu hình vận hành — xem chi tiết tại [09-system-settings.md](./09-system-settings.md).

```sql
CREATE TABLE system_settings (
    key         TEXT    PRIMARY KEY,   -- "upload.max_file_size_mb"
    value       TEXT    NOT NULL,
    description TEXT,
    data_type   TEXT    NOT NULL,      -- "int"|"string"|"bool"|"stringlist"
    category    TEXT    NOT NULL,      -- "upload"|"worker"|"nas"|"ratelimit"|"cors"
    is_sensitive INTEGER NOT NULL DEFAULT 0,
    updated_at  TEXT    NOT NULL DEFAULT (datetime('now')),
    updated_by  TEXT
);
```

### Seed – Giá Trị Mặc Định

| Key | Mặc định | Category |
|-----|----------|----------|
| `nas.upload_dir` | `/mnt/nas/uploads` | nas |
| `nas.logs_dir` | `/mnt/nas/logs` | nas |
| `nas.min_disk_space_gb` | `1` | nas |
| `upload.max_file_size_mb` | `1500` | upload |
| `upload.max_files_per_request` | `5` | upload |
| `upload.allowed_extensions` | `.mp4,.avi,.mov,...` | upload |
| `upload.allowed_mimetypes` | `video/*,audio/*,image/*` | upload |
| `worker.tick_interval_seconds` | `30` | worker |
| `worker.max_retry` | `3` | worker |
| `worker.retry_delay_minutes` | `5` | worker |
| `ratelimit.upload_permit_limit` | `20` | ratelimit |
| `ratelimit.upload_window_minutes` | `15` | ratelimit |
| `ratelimit.api_permit_limit` | `60` | ratelimit |
| `ratelimit.api_window_minutes` | `1` | ratelimit |
| `cors.allowed_origins` | `http://localhost:5173` | cors |

---

## Enum: `JobStatus`

```csharp
// Domain/Enums/JobStatus.cs
public enum JobStatus
{
    Pending   = 0,  // Chờ xử lý
    Running   = 1,  // Đang push ERP
    Paused    = 2,  // Tạm dừng (thủ công hoặc ngoài giờ)
    Done      = 3,  // Push ERP thành công
    Failed    = 4,  // Hết retry, thất bại
    Cancelled = 5   // Bị hủy thủ công
}
```

---

## AppDbContext

```csharp
// Infrastructure/Data/AppDbContext.cs
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UploadJob> UploadJobs => Set<UploadJob>();
    public DbSet<TimeWindowConfig> TimeWindowConfigs => Set<TimeWindowConfig>();
    public DbSet<ErpEndpointConfig> ErpEndpointConfigs => Set<ErpEndpointConfig>();
    public DbSet<ApiCredential> ApiCredentials => Set<ApiCredential>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UploadJob>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.FileId).IsUnique();
            b.HasIndex(x => x.Status);
            b.Property(x => x.Status).HasConversion<int>();
        });

        // Seed default TimeWindow: Mon–Fri 07:00–08:00 và 18:00–22:00
        modelBuilder.Entity<TimeWindowConfig>().HasData(
            new TimeWindowConfig
            {
                Id = 1, Name = "Ngoài giờ hành chính – Sáng sớm",
                DaysOfWeek = "1,2,3,4,5",
                StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(8, 0),
                IsEnabled = true, MaxConcurrent = 5
            },
            new TimeWindowConfig
            {
                Id = 2, Name = "Ngoài giờ hành chính – Tối",
                DaysOfWeek = "1,2,3,4,5",
                StartTime = new TimeOnly(18, 0),
                EndTime = new TimeOnly(22, 0),
                IsEnabled = true, MaxConcurrent = 10
            }
        );
    }
}
```

---

## Migrations

```bash
# Cài package PostgreSQL cho EF Core
dotnet add MediaUpload.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL

# Tạo migration đầu tiên
dotnet ef migrations add InitialCreate --project MediaUpload.Infrastructure --startup-project MediaUpload.API

# Apply migration
dotnet ef database update --project MediaUpload.Infrastructure --startup-project MediaUpload.API
```

---

## Connection String (appsettings.json)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=media_upload;Username=postgres;Password=yourpassword"
  }
}
```

> Khuyến nghị: lưu password qua **environment variable** hoặc **dotnet user-secrets**, không commit vào git.

```bash
# Lưu password an toàn khi dev
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=media_upload;Username=postgres;Password=yourpassword" --project MediaUpload.API
```
