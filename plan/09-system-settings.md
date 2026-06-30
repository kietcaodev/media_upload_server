# System Settings – Cấu Hình Hệ Thống Qua UI

> Toàn bộ cấu hình vận hành được lưu trong DB và chỉnh sửa qua giao diện Admin.  
> Không còn cấu hình cứng trong `appsettings.json` ngoài Connection String và AES Key.

---

## Bảng 5: `SystemSettings` (Key-Value Store)

```sql
CREATE TABLE system_settings (
    key         TEXT    PRIMARY KEY,   -- "upload.max_file_size_mb"
    value       TEXT    NOT NULL,      -- "1500"
    description TEXT,                  -- Mô tả hiển thị trên UI
    data_type   TEXT    NOT NULL,      -- "int" | "string" | "bool" | "json" | "stringlist"
    category    TEXT    NOT NULL,      -- "upload" | "worker" | "nas" | "ratelimit" | "cors" | "filetype"
    is_sensitive INTEGER NOT NULL DEFAULT 0,  -- 1 = ẩn giá trị trên UI (như password)
    updated_at  TEXT    NOT NULL DEFAULT (datetime('now')),
    updated_by  TEXT                   -- credential name thực hiện thay đổi
);
```

### Seed Data – Giá Trị Mặc Định

```sql
-- ===== NHÓM: NAS / STORAGE =====
INSERT INTO system_settings VALUES
('nas.upload_dir',       '/mnt/nas/uploads', 'Thư mục lưu file upload trên NAS',   'string', 'nas',       0, ...),
('nas.logs_dir',         '/mnt/nas/logs',    'Thư mục lưu log trên NAS',           'string', 'nas',       0, ...),
('nas.min_disk_space_gb','1',                'Dung lượng tối thiểu trên NAS (GB)', 'int',    'nas',       0, ...),

-- ===== NHÓM: UPLOAD =====
('upload.max_file_size_mb',      '1500', 'Dung lượng tối đa mỗi file (MB)',         'int',        'upload', 0, ...),
('upload.max_files_per_request', '5',    'Số file tối đa trong 1 lần upload',       'int',        'upload', 0, ...),
('upload.allowed_extensions',
  '.mp4,.avi,.mov,.mkv,.flv,.wmv,.webm,.3gp,.mp3,.wav,.ogg,.aac,.flac,.m4a,.wma,.jpg,.jpeg,.png,.gif,.bmp,.webp,.heic',
  'Các đuôi file được phép upload (phân cách bởi dấu phẩy)',
  'stringlist', 'upload', 0, ...),
('upload.allowed_mimetypes',
  'video/*,audio/*,image/*',
  'MIME types được phép (dùng wildcard)',
  'stringlist', 'upload', 0, ...),

-- ===== NHÓM: WORKER =====
('worker.tick_interval_seconds', '30',  'Tần suất worker kiểm tra job (giây)',          'int', 'worker', 0, ...),
('worker.max_retry',             '3',   'Số lần retry tối đa khi push ERP thất bại',    'int', 'worker', 0, ...),
('worker.retry_delay_minutes',   '5',   'Thời gian chờ giữa các lần retry (phút)',      'int', 'worker', 0, ...),

-- ===== NHÓM: RATE LIMIT =====
('ratelimit.upload_permit_limit',   '20', 'Số request upload tối đa trong 1 window',   'int', 'ratelimit', 0, ...),
('ratelimit.upload_window_minutes', '15', 'Khoảng thời gian tính rate limit (phút)',   'int', 'ratelimit', 0, ...),
('ratelimit.api_permit_limit',      '60', 'Số request API chung tối đa trong 1 window','int', 'ratelimit', 0, ...),
('ratelimit.api_window_minutes',    '1',  'Khoảng thời gian tính rate limit API (phút)','int','ratelimit', 0, ...),

-- ===== NHÓM: CORS =====
('cors.allowed_origins',
  'http://localhost:5173,http://localhost:3000',
  'Danh sách origin được phép CORS (phân cách bởi dấu phẩy)',
  'stringlist', 'cors', 0, ...),

-- ===== NHÓM: SYSTEM =====
('system.timezone',
  'Asia/Ho_Chi_Minh',
  'Timezone hiển thị trên UI và dùng để kiểm tra TimeWindow (IANA timezone ID)',
  'string', 'system', 0, ...);
```

---

## Entity C#

```csharp
// Domain/Entities/SystemSetting.cs
public class SystemSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DataType { get; set; } = "string"; // int|string|bool|json|stringlist
    public string Category { get; set; } = string.Empty;
    public bool IsSensitive { get; set; } = false;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedBy { get; set; }
}
```

---

## SettingsService – Runtime Cache

```csharp
// Application/Services/SettingsService.cs
public class SettingsService : ISettingsService
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private const string CachePrefix = "setting:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    // Đọc setting theo key, trả về kiểu T
    public async Task<T> GetAsync<T>(string key, T defaultValue)
    {
        var cacheKey = CachePrefix + key;
        if (_cache.TryGetValue(cacheKey, out T? cached)) return cached!;

        var setting = await _db.SystemSettings.FindAsync(key);
        if (setting is null) return defaultValue;

        var value = Convert.ChangeType(setting.Value, typeof(T));
        _cache.Set(cacheKey, value, CacheTtl);
        return (T)value;
    }

    // Shortcut helpers
    public Task<int>    GetIntAsync(string key, int def = 0)       => GetAsync(key, def);
    public Task<string> GetStringAsync(string key, string def = "") => GetAsync(key, def);
    public Task<bool>   GetBoolAsync(string key, bool def = false)  => GetAsync(key, def);
    public async Task<List<string>> GetStringListAsync(string key)
    {
        var raw = await GetStringAsync(key, "");
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                  .Select(s => s.Trim()).ToList();
    }

    // Lưu setting + invalidate cache ngay lập tức
    public async Task SetAsync(string key, string value, string? updatedBy = null)
    {
        var setting = await _db.SystemSettings.FindAsync(key);
        if (setting is null) throw new KeyNotFoundException($"Setting '{key}' không tồn tại");

        setting.Value = value;
        setting.UpdatedAt = DateTime.UtcNow;
        setting.UpdatedBy = updatedBy;
        await _db.SaveChangesAsync();

        // Xóa cache → lần đọc tiếp theo sẽ lấy giá trị mới
        _cache.Remove(CachePrefix + key);
    }

    // Timezone helpers — dùng khắp nơi, không hardcode "Asia/Ho_Chi_Minh"
    public async Task<TimeZoneInfo> GetTimezoneAsync()
    {
        var tzId = await GetStringAsync("system.timezone", "Asia/Ho_Chi_Minh");
        return TimeZoneInfo.FindSystemTimeZoneById(tzId);
    }

    public async Task<DateTime> ToLocalTimeAsync(DateTime utcTime)
    {
        var tz = await GetTimezoneAsync();
        return TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcTime, DateTimeKind.Utc), tz);
    }
}
```

---

## API Endpoints – System Settings

### `GET /api/config/settings`
Trả về tất cả settings, nhóm theo category.

**Query:** `?category=worker` (optional)

```json
{
  "nas": [
    { "key": "nas.upload_dir",        "value": "/mnt/nas/uploads", "description": "...", "dataType": "string" },
    { "key": "nas.logs_dir",          "value": "/mnt/nas/logs",    "description": "...", "dataType": "string" },
    { "key": "nas.min_disk_space_gb", "value": "1",                "description": "...", "dataType": "int" }
  ],
  "upload": [
    { "key": "upload.max_file_size_mb",      "value": "1500", "dataType": "int" },
    { "key": "upload.max_files_per_request", "value": "5",    "dataType": "int" },
    { "key": "upload.allowed_extensions",    "value": ".mp4,.avi,...", "dataType": "stringlist" },
    { "key": "upload.allowed_mimetypes",     "value": "video/*,...",   "dataType": "stringlist" }
  ],
  "worker": [
    { "key": "worker.tick_interval_seconds", "value": "30", "dataType": "int" },
    { "key": "worker.max_retry",             "value": "3",  "dataType": "int" },
    { "key": "worker.retry_delay_minutes",   "value": "5",  "dataType": "int" }
  ],
  "ratelimit": [
    { "key": "ratelimit.upload_permit_limit",   "value": "20", "dataType": "int" },
    { "key": "ratelimit.upload_window_minutes", "value": "15", "dataType": "int" },
    { "key": "ratelimit.api_permit_limit",      "value": "60", "dataType": "int" },
    { "key": "ratelimit.api_window_minutes",    "value": "1",  "dataType": "int" }
  ],
  "cors": [
    { "key": "cors.allowed_origins", "value": "http://localhost:5173", "dataType": "stringlist" }
  ]
}
```

---

### `PATCH /api/config/settings`
Cập nhật nhiều settings cùng lúc.

**Body:**
```json
[
  { "key": "upload.max_file_size_mb",      "value": "2000" },
  { "key": "worker.max_retry",             "value": "5"    },
  { "key": "ratelimit.upload_permit_limit","value": "30"   }
]
```

**Response 200:**
```json
{
  "updated": 3,
  "requiresRestart": ["ratelimit.upload_permit_limit", "cors.allowed_origins"],
  "hotReload": ["upload.max_file_size_mb", "worker.max_retry"]
}
```

> `requiresRestart`: một số setting (CORS, rate limit) cần restart app để có hiệu lực.  
> `hotReload`: các setting được áp dụng ngay lập tức qua cache invalidation.

---

### `POST /api/config/settings/reset/{key}`
Reset 1 setting về giá trị mặc định.

### `POST /api/config/settings/reset-all`
Reset toàn bộ settings về mặc định (yêu cầu confirm).

---

## Cách Dùng Settings Trong Code

```csharp
// UploadController – dùng SettingsService thay vì IOptions<>
[HttpPost("upload")]
public async Task<IActionResult> Upload(...)
{
    var maxSizeMb  = await _settings.GetIntAsync("upload.max_file_size_mb", 1500);
    var maxFiles   = await _settings.GetIntAsync("upload.max_files_per_request", 5);
    var allowedExt = await _settings.GetStringListAsync("upload.allowed_extensions");
    // ...
}

// JobWorkerService – worker tick interval
protected override async Task ExecuteAsync(CancellationToken ct)
{
    while (!ct.IsCancellationRequested)
    {
        var intervalSec = await _settings.GetIntAsync("worker.tick_interval_seconds", 30);
        await Task.Delay(TimeSpan.FromSeconds(intervalSec), ct);
        await DispatchAsync(ct);
    }
}

// ErpPushService – max retry
var maxRetry = await _settings.GetIntAsync("worker.max_retry", 3);
var delayMin = await _settings.GetIntAsync("worker.retry_delay_minutes", 5);
```

---

## Frontend – Trang System Settings (`/config/settings`)

```
┌──────────────────────────────────────────────────────────────┐
│  CẤU HÌNH HỆ THỐNG                         [💾 Lưu Tất Cả]  │
│                                                              │
│  ┌─ NAS / STORAGE ──────────────────────────────────────┐   │
│  │ Thư mục upload NAS   [/mnt/nas/uploads          ]    │   │
│  │ Thư mục logs NAS     [/mnt/nas/logs             ]    │   │
│  │ Dung lượng tối thiểu [1      ] GB                    │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              │
│  ┌─ UPLOAD ─────────────────────────────────────────────┐   │
│  │ Dung lượng tối đa/file  [1500 ] MB                   │   │
│  │ Số file tối đa/request  [5    ]                      │   │
│  │ Đuôi file cho phép:                                  │   │
│  │ [.mp4] [.avi] [.mov] [.mp3] [.wav] ... [+ Thêm]     │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              │
│  ┌─ WORKER ─────────────────────────────────────────────┐   │
│  │ Kiểm tra job mỗi    [30  ] giây                      │   │
│  │ Số lần retry tối đa [3   ]                           │   │
│  │ Thời gian chờ retry [5   ] phút (nhân với lần retry) │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              │
│  ┌─ RATE LIMIT ─────────────────────────────────────────┐   │
│  │ Upload: tối đa [20 ] request / [15 ] phút            │   │
│  │ API:    tối đa [60 ] request / [1  ] phút            │   │
│  │ ⚠️ Thay đổi rate limit cần restart server            │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              │
│  ┌─ CORS ───────────────────────────────────────────────┐   │
│  │ Origins cho phép:                                    │   │
│  │ [http://localhost:5173      ] [✕]                    │   │
│  │ [http://your-domain.com     ] [✕]                    │   │
│  │ [+ Thêm origin]                                      │   │
│  │ ⚠️ Thay đổi CORS cần restart server                  │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              ││  ┌─ HỆ THỐNG ───────────────────────────────────────────────┐   │
│  │ Timezone [Asia/Ho_Chi_Minh (GMT+7)          ▼]       │   │
│  │ Dùng cho: hiển thị UI + kiểm tra khung giờ       │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              ││                [↺ Reset Về Mặc Định]  [💾 Lưu Tất Cả]      │
└──────────────────────────────────────────────────────────────┘
```

### SystemSettingsPage Component

```tsx
// pages/Config/SystemSettings/index.tsx
import { Form, InputNumber, Input, Button, Card, Tag, Alert, Space } from "antd";
import { useSettingsStore } from "../../../store/settingsStore";

export function SystemSettingsPage() {
  const [form] = Form.useForm();
  const { settings, loading, saveSettings, resetAll } = useSettingsStore();
  const [requiresRestart, setRequiresRestart] = useState<string[]>([]);

  const handleSave = async () => {
    const values = await form.validateFields();
    const patches = flattenFormValues(values); // { key, value }[]
    const result = await saveSettings(patches);
    if (result.requiresRestart.length > 0) {
      setRequiresRestart(result.requiresRestart);
    }
  };

  return (
    <Form form={form} layout="vertical" initialValues={mapSettingsToForm(settings)}>
      {requiresRestart.length > 0 && (
        <Alert
          type="warning"
          message={`Một số thay đổi cần restart server: ${requiresRestart.join(", ")}`}
          closable
          style={{ marginBottom: 16 }}
        />
      )}

      {/* NAS */}
      <Card title="NAS / Storage" style={{ marginBottom: 16 }}>
        <Form.Item name={["nas", "upload_dir"]} label="Thư mục upload NAS">
          <Input placeholder="/mnt/nas/uploads" />
        </Form.Item>
        <Form.Item name={["nas", "logs_dir"]} label="Thư mục logs NAS">
          <Input placeholder="/mnt/nas/logs" />
        </Form.Item>
        <Form.Item name={["nas", "min_disk_space_gb"]} label="Dung lượng tối thiểu (GB)">
          <InputNumber min={1} max={100} addonAfter="GB" />
        </Form.Item>
      </Card>

      {/* Upload */}
      <Card title="Upload" style={{ marginBottom: 16 }}>
        <Form.Item name={["upload", "max_file_size_mb"]} label="Dung lượng tối đa / file">
          <InputNumber min={1} max={5000} addonAfter="MB" />
        </Form.Item>
        <Form.Item name={["upload", "max_files_per_request"]} label="Số file tối đa / request">
          <InputNumber min={1} max={20} />
        </Form.Item>
        <Form.Item name={["upload", "allowed_extensions"]} label="Đuôi file được phép">
          <TagsInput placeholder="Thêm đuôi file..." />
        </Form.Item>
        <Form.Item name={["upload", "allowed_mimetypes"]} label="MIME types được phép">
          <TagsInput placeholder="video/mp4, audio/*, ..." />
        </Form.Item>
      </Card>

      {/* Worker */}
      <Card title="Worker" style={{ marginBottom: 16 }}>
        <Form.Item name={["worker", "tick_interval_seconds"]} label="Kiểm tra job mỗi (giây)">
          <InputNumber min={5} max={300} addonAfter="giây" />
        </Form.Item>
        <Form.Item name={["worker", "max_retry"]} label="Số lần retry tối đa">
          <InputNumber min={0} max={10} />
        </Form.Item>
        <Form.Item name={["worker", "retry_delay_minutes"]}
          label="Thời gian chờ retry (phút × số lần retry)">
          <InputNumber min={1} max={60} addonAfter="phút" />
        </Form.Item>
      </Card>

      {/* Rate Limit */}
      <Card title="Rate Limit" style={{ marginBottom: 16 }}
        extra={<Tag color="orange">⚠ Cần restart server</Tag>}>
        <Form.Item label="Giới hạn Upload">
          <Space>
            <Form.Item name={["ratelimit", "upload_permit_limit"]} noStyle>
              <InputNumber min={1} max={1000} addonBefore="Tối đa" addonAfter="request" />
            </Form.Item>
            <Form.Item name={["ratelimit", "upload_window_minutes"]} noStyle>
              <InputNumber min={1} max={60} addonBefore="/" addonAfter="phút" />
            </Form.Item>
          </Space>
        </Form.Item>
        <Form.Item label="Giới hạn API chung">
          <Space>
            <Form.Item name={["ratelimit", "api_permit_limit"]} noStyle>
              <InputNumber min={1} max={1000} addonBefore="Tối đa" addonAfter="request" />
            </Form.Item>
            <Form.Item name={["ratelimit", "api_window_minutes"]} noStyle>
              <InputNumber min={1} max={60} addonBefore="/" addonAfter="phút" />
            </Form.Item>
          </Space>
        </Form.Item>
      </Card>

      {/* CORS */}
      <Card title="CORS – Origins Được Phép"
        extra={<Tag color="orange">⚠ Cần restart server</Tag>}
        style={{ marginBottom: 16 }}>
        <Form.Item name={["cors", "allowed_origins"]}>
          <TagsInput placeholder="http://your-domain.com" />
        </Form.Item>
      </Card>

      {/* System */}
      <Card title="Hệ Thống" style={{ marginBottom: 16 }}>
        <Form.Item name={["system", "timezone"]} label="Timezone"
          help="IANA timezone ID. Dùng cho hiển thị UI và kiểm tra khung giờ">
          <Select
            showSearch
            options={[
              { label: "GMT+7 – Asia/Ho_Chi_Minh", value: "Asia/Ho_Chi_Minh" },
              { label: "GMT+8 – Asia/Singapore",    value: "Asia/Singapore" },
              { label: "UTC – Etc/UTC",             value: "Etc/UTC" },
            ]}
          />
        </Form.Item>
      </Card>

      <Space>
        <Button danger onClick={() => resetAll()}>↺ Reset Về Mặc Định</Button>
        <Button type="primary" loading={loading} onClick={handleSave}>💾 Lưu Tất Cả</Button>
      </Space>
    </Form>
  );
}
```

---

## Cập Nhật Điều Hướng Frontend

Trang Settings được đặt trong **Tab Config**, thêm vào các tab hiện có:

```
/config
├── /config/timewindows     ← Tab: Khung Giờ (đã có)
├── /config/erp             ← Tab: ERP Endpoints (đã có)
├── /config/credentials     ← Tab: Credentials (đã có)
└── /config/settings        ← Tab: Cài Đặt Hệ Thống (MỚI) ⬅
```

```tsx
// pages/Config/index.tsx – thêm tab mới
const tabs = [
  { key: "timewindows",  label: "⏰ Khung Giờ",         children: <TimeWindowList /> },
  { key: "erp",          label: "🌐 ERP Endpoints",      children: <ErpConfigList /> },
  { key: "credentials",  label: "🔐 Credentials",        children: <CredentialList /> },
  { key: "settings",     label: "⚙️ Cài Đặt Hệ Thống",  children: <SystemSettingsPage /> },
];
```

---

## Những Gì KHÔNG Đưa Lên UI (Lý Do Bảo Mật)

| Config | Lý do giữ trong file/env |
|--------|--------------------------|
| `ConnectionStrings` | Chứa password DB – chỉ dùng secrets/env var |
| `Encryption.AesKey` | Key mã hóa – tuyệt đối không lưu DB hoặc hiển thị UI |
| `Logging.Level` | Devops concern, không phải business config |

---

## appsettings.json Sau Khi Dọn Dẹp

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=media_upload;Username=postgres;Password=yourpassword"
  },
  "Encryption": {
    "AesKey": "** set via environment variable ENCRYPTION__AESKEY **"
  }
}
```

> Tất cả config khác đều lấy từ DB qua `SettingsService` với fallback về giá trị mặc định.
