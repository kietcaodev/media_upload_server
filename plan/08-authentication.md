# Authentication & Credentials Management

> Hệ thống hỗ trợ **nhiều loại xác thực** cho từng client/ERP caller.  
> Quản lý credential qua UI, có thể tạo/thu hồi mà không cần restart server.

---

## Các Loại Auth Được Hỗ Trợ

| Loại | Header | Ví dụ | Dùng khi |
|------|--------|-------|----------|
| **Bearer Token** | `Authorization: Bearer <token>` | `Bearer abc123xyz` | Mặc định cho ERP, mobile app |
| **Basic Auth** | `Authorization: Basic <base64>` | `Basic dXNlcjpwYXNz` | Hệ thống cũ, tích hợp đơn giản |
| **API Key Header** | `X-Api-Key: <key>` | `X-Api-Key: key_abc123` | IoT, thiết bị không hỗ trợ Bearer |
| **No Auth** | _(không có header)_ | — | Chỉ cho môi trường nội bộ/dev |

---

## Bảng 4: `ApiCredentials` (bổ sung vào DB)

```sql
CREATE TABLE api_credentials (
    id              TEXT        PRIMARY KEY,   -- GUID
    name            TEXT        NOT NULL,      -- "ERP DND Client", "Mobile App"
    auth_type       INTEGER     NOT NULL,      -- 0=BearerToken, 1=BasicAuth, 2=ApiKey
    -- Bearer Token
    token_hash      TEXT,                      -- bcrypt hash của token
    token_prefix    TEXT,                      -- 8 ký tự đầu để hiển thị UI
    -- Basic Auth
    username        TEXT,
    password_hash   TEXT,                      -- bcrypt hash
    -- API Key
    api_key_hash    TEXT,
    api_key_prefix  TEXT,
    -- Phân quyền
    allowed_erp     TEXT,                      -- NULL=tất cả, "DND,Zomzem"
    can_upload      INTEGER NOT NULL DEFAULT 1,
    can_read_jobs   INTEGER NOT NULL DEFAULT 1,
    can_config      INTEGER NOT NULL DEFAULT 0, -- Chỉ admin mới config được
    -- Metadata
    description     TEXT,
    is_enabled      INTEGER NOT NULL DEFAULT 1,
    last_used_at    TEXT,
    expires_at      TEXT,                       -- NULL = không hết hạn
    created_at      TEXT NOT NULL DEFAULT (datetime('now')),
    updated_at      TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX idx_credentials_enabled ON api_credentials(is_enabled);
```

### Entity C#

```csharp
// Domain/Entities/ApiCredential.cs
public class ApiCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public AuthType AuthType { get; set; }

    // Bearer Token
    public string? TokenHash { get; set; }
    public string? TokenPrefix { get; set; }   // hiển thị "abc12345..."

    // Basic Auth
    public string? Username { get; set; }
    public string? PasswordHash { get; set; }

    // API Key
    public string? ApiKeyHash { get; set; }
    public string? ApiKeyPrefix { get; set; }

    // Phân quyền
    public string? AllowedErp { get; set; }   // null = tất cả
    public bool CanUpload { get; set; } = true;
    public bool CanReadJobs { get; set; } = true;
    public bool CanConfig { get; set; } = false;

    // Metadata
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum AuthType
{
    BearerToken = 0,
    BasicAuth   = 1,
    ApiKey      = 2,
    NoAuth      = 99  // chỉ dùng nội bộ/dev
}
```

---

## Middleware Xác Thực

### AuthMiddleware.cs

```csharp
// API/Middlewares/AuthMiddleware.cs
public class AuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthMiddleware> _logger;

    public AuthMiddleware(RequestDelegate next, ILogger<AuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICredentialService credentialService)
    {
        // Bỏ qua auth cho health check và SignalR hub
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/health") || path.StartsWith("/hubs/"))
        {
            await _next(context);
            return;
        }

        // Cho phép NO_AUTH nếu cấu hình env = Development (chỉ localhost)
        if (IsNoAuthAllowed(context))
        {
            context.Items["AuthType"] = "NoAuth";
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        var apiKeyHeader = context.Request.Headers["X-Api-Key"].FirstOrDefault();

        ApiCredential? credential = null;

        // 1. Thử Bearer Token
        if (authHeader?.StartsWith("Bearer ") == true)
        {
            var token = authHeader["Bearer ".Length..].Trim();
            credential = await credentialService.ValidateBearerTokenAsync(token);
        }
        // 2. Thử Basic Auth
        else if (authHeader?.StartsWith("Basic ") == true)
        {
            var encoded = authHeader["Basic ".Length..].Trim();
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            var parts = decoded.Split(':', 2);
            if (parts.Length == 2)
                credential = await credentialService.ValidateBasicAuthAsync(parts[0], parts[1]);
        }
        // 3. Thử API Key header
        else if (!string.IsNullOrEmpty(apiKeyHeader))
        {
            credential = await credentialService.ValidateApiKeyAsync(apiKeyHeader);
        }

        if (credential is null || !credential.IsEnabled)
        {
            _logger.LogWarning("Unauthorized request to {Path} from {IP}",
                path, context.Connection.RemoteIpAddress);

            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                code = "UNAUTHORIZED",
                message = "Token hoặc API Key không hợp lệ"
            });
            return;
        }

        // Kiểm tra expiry
        if (credential.ExpiresAt.HasValue && credential.ExpiresAt < DateTime.UtcNow)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                code = "TOKEN_EXPIRED",
                message = "Credential đã hết hạn"
            });
            return;
        }

        // Đưa credential vào context để controller dùng
        context.Items["Credential"] = credential;

        // Cập nhật LastUsedAt (fire-and-forget)
        _ = credentialService.UpdateLastUsedAsync(credential.Id);

        await _next(context);
    }

    private static bool IsNoAuthAllowed(HttpContext context)
    {
        var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var ip = context.Connection.RemoteIpAddress?.ToString();
        return env.IsDevelopment() &&
               (ip == "127.0.0.1" || ip == "::1");
    }
}
```

### Authorization Attribute

```csharp
// API/Attributes/RequirePermissionAttribute.cs
public class RequirePermissionAttribute : ActionFilterAttribute
{
    private readonly string _permission;

    public RequirePermissionAttribute(string permission) => _permission = permission;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var credential = context.HttpContext.Items["Credential"] as ApiCredential;

        var hasPermission = _permission switch
        {
            "upload"     => credential?.CanUpload ?? false,
            "read_jobs"  => credential?.CanReadJobs ?? false,
            "config"     => credential?.CanConfig ?? false,
            _            => false
        };

        if (!hasPermission)
        {
            context.Result = new ObjectResult(new
            {
                success = false,
                code = "FORBIDDEN",
                message = $"Credential không có quyền: {_permission}"
            })
            { StatusCode = 403 };
        }
    }
}
```

### Dùng trong Controller

```csharp
[HttpPost("upload")]
[RequirePermission("upload")]
public async Task<IActionResult> Upload(...)

[HttpGet("jobs")]
[RequirePermission("read_jobs")]
public async Task<IActionResult> GetJobs(...)

[HttpPost("config/timewindows")]
[RequirePermission("config")]
public async Task<IActionResult> CreateTimeWindow(...)
```

---

## CredentialService

```csharp
// Application/Services/CredentialService.cs
public class CredentialService : ICredentialService
{
    private readonly AppDbContext _db;

    // Cache ngắn hạn 30s để giảm DB query (credential ít thay đổi)
    private readonly IMemoryCache _cache;

    public async Task<ApiCredential?> ValidateBearerTokenAsync(string rawToken)
    {
        // Lấy tất cả BearerToken credentials đang active
        var cacheKey = "credentials:bearer";
        var candidates = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            return await _db.ApiCredentials
                .Where(c => c.AuthType == AuthType.BearerToken && c.IsEnabled)
                .ToListAsync();
        });

        // So sánh bcrypt hash
        return candidates?.FirstOrDefault(c =>
            BCrypt.Net.BCrypt.Verify(rawToken, c.TokenHash));
    }

    public async Task<ApiCredential?> ValidateBasicAuthAsync(string username, string password)
    {
        var credential = await _db.ApiCredentials
            .FirstOrDefaultAsync(c => c.AuthType == AuthType.BasicAuth
                                   && c.Username == username
                                   && c.IsEnabled);

        if (credential is null) return null;
        return BCrypt.Net.BCrypt.Verify(password, credential.PasswordHash)
            ? credential : null;
    }

    public async Task<ApiCredential?> ValidateApiKeyAsync(string rawKey)
    {
        var prefix = rawKey.Length >= 8 ? rawKey[..8] : rawKey;
        var candidates = await _db.ApiCredentials
            .Where(c => c.AuthType == AuthType.ApiKey
                     && c.ApiKeyPrefix == prefix
                     && c.IsEnabled)
            .ToListAsync();

        return candidates.FirstOrDefault(c =>
            BCrypt.Net.BCrypt.Verify(rawKey, c.ApiKeyHash));
    }

    public async Task<(ApiCredential credential, string rawValue)> CreateBearerTokenAsync(
        string name, CreateCredentialDto dto)
    {
        var rawToken = GenerateSecureToken(48); // 48 bytes → 64 char base64
        var credential = new ApiCredential
        {
            Name = name,
            AuthType = AuthType.BearerToken,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(rawToken),
            TokenPrefix = rawToken[..8],
            CanUpload = dto.CanUpload,
            CanReadJobs = dto.CanReadJobs,
            CanConfig = dto.CanConfig,
            AllowedErp = dto.AllowedErp,
            Description = dto.Description,
            ExpiresAt = dto.ExpiresAt
        };

        _db.ApiCredentials.Add(credential);
        await _db.SaveChangesAsync();

        // Trả về raw token CHỈ 1 LẦN DUY NHẤT, sau đó không thể lấy lại
        return (credential, rawToken);
    }

    public async Task<(ApiCredential credential, string rawValue)> CreateBasicAuthAsync(
        string name, string username, string password, CreateCredentialDto dto)
    {
        var credential = new ApiCredential
        {
            Name = name,
            AuthType = AuthType.BasicAuth,
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CanUpload = dto.CanUpload,
            CanReadJobs = dto.CanReadJobs,
            CanConfig = dto.CanConfig,
            Description = dto.Description
        };

        _db.ApiCredentials.Add(credential);
        await _db.SaveChangesAsync();

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        return (credential, $"Basic {base64}");
    }

    public async Task<(ApiCredential credential, string rawValue)> CreateApiKeyAsync(
        string name, CreateCredentialDto dto)
    {
        var rawKey = $"key_{GenerateSecureToken(32)}"; // prefix "key_" dễ nhận biết
        var credential = new ApiCredential
        {
            Name = name,
            AuthType = AuthType.ApiKey,
            ApiKeyHash = BCrypt.Net.BCrypt.HashPassword(rawKey),
            ApiKeyPrefix = rawKey[..8],
            CanUpload = dto.CanUpload,
            CanReadJobs = dto.CanReadJobs,
            CanConfig = dto.CanConfig,
            Description = dto.Description,
            ExpiresAt = dto.ExpiresAt
        };

        _db.ApiCredentials.Add(credential);
        await _db.SaveChangesAsync();

        return (credential, rawKey);
    }

    private static string GenerateSecureToken(int byteLength)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteLength);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    public Task UpdateLastUsedAsync(Guid id)
        => _db.ApiCredentials
              .Where(c => c.Id == id)
              .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastUsedAt, DateTime.UtcNow));
}
```

---

## API Endpoints – Credentials

### `GET /api/credentials`
Danh sách credentials (không trả về secret, chỉ metadata).

```json
[
  {
    "id": "3fa85f64-...",
    "name": "ERP DND Client",
    "authType": "BearerToken",
    "tokenPrefix": "abc12345...",
    "canUpload": true,
    "canReadJobs": true,
    "canConfig": false,
    "allowedErp": null,
    "isEnabled": true,
    "lastUsedAt": "2026-06-30T07:00:00Z",
    "expiresAt": null,
    "createdAt": "2026-01-01T00:00:00Z"
  }
]
```

---

### `POST /api/credentials/bearer`
Tạo Bearer Token mới.

**Body:**
```json
{
  "name": "ERP DND Client",
  "description": "Dùng cho app mobile DND",
  "canUpload": true,
  "canReadJobs": true,
  "canConfig": false,
  "allowedErp": "DND",
  "expiresAt": "2027-01-01T00:00:00Z"
}
```

**Response 201** *(token chỉ trả về 1 lần duy nhất)*:
```json
{
  "id": "3fa85f64-...",
  "name": "ERP DND Client",
  "authType": "BearerToken",
  "token": "Abc123XYZ...64chars",
  "usage": "Authorization: Bearer Abc123XYZ...64chars",
  "warning": "Lưu token ngay bây giờ. Sau này không thể xem lại."
}
```

---

### `POST /api/credentials/basic`
Tạo Basic Auth credential.

**Body:**
```json
{
  "name": "Zomzem Integration",
  "username": "zomzem_uploader",
  "password": "StrongPassword123!",
  "canUpload": true,
  "canReadJobs": false,
  "canConfig": false
}
```

**Response 201:**
```json
{
  "id": "...",
  "name": "Zomzem Integration",
  "authType": "BasicAuth",
  "username": "zomzem_uploader",
  "base64Value": "em9temVtX3VwbG9hZGVyOlN0cm9uZ1Bhc3N3b3JkMTIzIQ==",
  "usage": "Authorization: Basic em9temVtX3VwbG9hZGVyOlN0cm9uZ1Bhc3N3b3JkMTIzIQ==",
  "warning": "Lưu thông tin ngay bây giờ. Sau này không thể xem lại."
}
```

---

### `POST /api/credentials/apikey`
Tạo API Key.

**Body:**
```json
{
  "name": "NAS Scanner Device",
  "description": "Thiết bị quét tự động",
  "canUpload": true,
  "canReadJobs": false,
  "canConfig": false,
  "expiresAt": null
}
```

**Response 201:**
```json
{
  "id": "...",
  "name": "NAS Scanner Device",
  "authType": "ApiKey",
  "apiKey": "key_Abc123XYZ...48chars",
  "usage": "X-Api-Key: key_Abc123XYZ...48chars",
  "warning": "Lưu API key ngay bây giờ. Sau này không thể xem lại."
}
```

---

### `PATCH /api/credentials/{id}`
Cập nhật quyền hoặc bật/tắt (không đổi được secret).

**Body:**
```json
{
  "isEnabled": false,
  "canConfig": true,
  "expiresAt": "2027-06-30T00:00:00Z"
}
```

---

### `DELETE /api/credentials/{id}`
Thu hồi credential vĩnh viễn.

---

### `POST /api/credentials/{id}/rotate`
Tạo secret mới, vô hiệu hóa secret cũ (chỉ Bearer Token và API Key).

**Response 200:**
```json
{
  "newToken": "NewAbc123XYZ...64chars",
  "usage": "Authorization: Bearer NewAbc123XYZ...",
  "warning": "Secret cũ đã bị vô hiệu ngay lập tức."
}
```

---

## Frontend – Trang Credentials (`/config/credentials`)

```
┌──────────────────────────────────────────────────────────────┐
│  QUẢN LÝ CREDENTIALS                [+ Tạo Bearer Token]     │
│                                     [+ Tạo Basic Auth]       │
│                                     [+ Tạo API Key]          │
├──────────────────────────────────────────────────────────────┤
│  Tên              │ Loại         │ Prefix     │ Quyền  │ ... │
│  ──────────────────────────────────────────────────────────  │
│  ERP DND Client   │ 🔑 Bearer   │ abc12345...│ U R    │ ... │
│  Zomzem Integ.    │ 🔐 Basic    │ zomzem_u.. │ U      │ ... │
│  NAS Scanner      │ 🗝 API Key  │ key_abc1...│ U      │ ... │
│                                                              │
│  U=Upload  R=ReadJobs  C=Config                             │
│  Mỗi row: [Bật/Tắt] [Rotate] [Xóa]                        │
├──────────────────────────────────────────────────────────────┤
│  ⚠️  Modal tạo credential: hiển thị secret 1 lần, có nút   │
│      [Copy] và checkbox "Đã lưu lại rồi" mới cho đóng      │
└──────────────────────────────────────────────────────────────┘
```

### Modal Hiển Thị Secret (1 lần duy nhất)

```tsx
// components/CredentialRevealModal.tsx
export function CredentialRevealModal({ credential, onClose }) {
  const [confirmed, setConfirmed] = useState(false);

  return (
    <Modal
      title="⚠️ Lưu Credential Ngay Bây Giờ"
      open={true}
      closable={false}           // Không cho đóng bằng X
      maskClosable={false}       // Không cho click outside để đóng
      footer={
        <Button
          type="primary" danger
          disabled={!confirmed}
          onClick={onClose}
        >
          Đóng
        </Button>
      }
    >
      <Alert
        type="warning"
        message="Đây là lần duy nhất bạn thấy secret này. Sau khi đóng modal, bạn KHÔNG THỂ xem lại."
        style={{ marginBottom: 16 }}
      />

      <Form.Item label="Dùng trong header">
        <Input.Password
          readOnly
          value={credential.usage}
          addonAfter={
            <CopyOutlined onClick={() => {
              navigator.clipboard.writeText(credential.usage);
              message.success("Đã copy!");
            }} />
          }
        />
      </Form.Item>

      <Checkbox
        onChange={(e) => setConfirmed(e.target.checked)}
        style={{ marginTop: 16 }}
      >
        Tôi đã lưu lại secret này rồi
      </Checkbox>
    </Modal>
  );
}
```

---

## Security Notes

| Yêu cầu | Cách thực hiện |
|---------|---------------|
| Không lưu secret dạng plaintext | Dùng **bcrypt** (cost=12) hash toàn bộ |
| Secret chỉ hiển thị 1 lần | Trả về raw value ngay khi tạo, sau đó chỉ lưu hash |
| Ngăn timing attack | `BCrypt.Verify` tự xử lý constant-time compare |
| Token có thể thu hồi ngay lập tức | `IsEnabled = false` → cache tự expire sau 30s |
| Không log secret | Middleware không log giá trị token/key, chỉ log `prefix` |
| Quyền tối thiểu | Mỗi credential chỉ được cấp quyền cần thiết |
| ERP token riêng biệt | `AllowedErp` giới hạn credential chỉ push được đến ERP chỉ định |

---

## Packages Cần Cài (Backend)

```bash
dotnet add MediaUpload.Infrastructure package BCrypt.Net-Next
dotnet add MediaUpload.API package Microsoft.Extensions.Caching.Memory
```
