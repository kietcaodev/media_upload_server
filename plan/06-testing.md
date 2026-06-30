# Chiến Lược Kiểm Thử

---

## 1. Unit Test – Backend (xUnit + Moq)

### Cài đặt

```bash
dotnet add package xunit
dotnet add package Moq
dotnet add package FluentAssertions
dotnet add package Microsoft.EntityFrameworkCore.InMemory
```

### TimeWindowChecker Tests

> **Quy tắc test timezone:** Luôn dùng UTC + explicit convert, không dùng `DateTime.Now`.

```csharp
// Tests/Workers/TimeWindowCheckerTests.cs
public class TimeWindowCheckerTests
{
    // Helper: tạo DateTime local GMT+7 từ UTC offset rõ ràng
    private static DateTime LocalGmt7(int year, int month, int day, int hour, int minute)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        // Tạo thời điểm UTC tương đương với giờ GMT+7 mong muốn
        var utc = new DateTime(year, month, day, hour - 7, minute, 0, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
    }

    [Fact]
    public async Task CheckAsync_ShouldReturn_True_WhenInsideWindow()
    {
        // Arrange: Thứ 2 (2026-06-29), 08:30 GMT+7 (trong khung 07:00–12:00)
        var config = new TimeWindowConfig
        {
            DaysOfWeek = "1,2,3,4,5",
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(12, 0),
            IsEnabled = true,
            MaxConcurrent = 5
        };

        var mondayMorningLocal = LocalGmt7(2026, 6, 29, 8, 30); // Thứ 2 08:30 GMT+7
        config.IsActive(mondayMorningLocal).Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_ShouldReturn_False_WhenOutsideWindow()
    {
        var config = new TimeWindowConfig
        {
            DaysOfWeek = "1,2,3,4,5",
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(8, 0),
            IsEnabled = true
        };

        // 09:00 GMT+7 – ngoài khung 07:00–08:00
        var outsideLocal = LocalGmt7(2026, 6, 29, 9, 0);
        config.IsActive(outsideLocal).Should().BeFalse();
    }

    [Fact]
    public async Task CheckAsync_ShouldReturn_False_WhenDisabled()
    {
        var config = new TimeWindowConfig
        {
            DaysOfWeek = "1,2,3,4,5",
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(22, 0),
            IsEnabled = false  // disabled
        };

        var insideLocal = LocalGmt7(2026, 6, 29, 10, 0);
        config.IsActive(insideLocal).Should().BeFalse();
    }

    [Theory]
    [InlineData("6,7", DayOfWeek.Saturday, true)]   // Cuối tuần – trong khung
    [InlineData("6,7", DayOfWeek.Monday, false)]     // Cuối tuần config – ngày thường → false
    public void CheckAsync_DayOfWeek_Scenarios(string daysConfig, DayOfWeek day, bool expected)
    {
        var config = new TimeWindowConfig
        {
            DaysOfWeek = daysConfig,
            StartTime = new TimeOnly(0, 0),
            EndTime = new TimeOnly(23, 59),
            IsEnabled = true
        };
        // Dùng explicit UTC → local, không dùng DateTime.Now
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        var nowUtc = DateTime.UtcNow;
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
        var date = GetNextDayOfWeek(nowLocal, day);
        config.IsActive(date).Should().Be(expected);
    }
}
```

### ErpPushService Tests

```csharp
// Tests/Workers/ErpPushServiceTests.cs
public class ErpPushServiceTests
{
    [Fact]
    public async Task PushAsync_ShouldMarkDone_WhenErpReturns200()
    {
        // Arrange
        var mockHttp = new Mock<HttpMessageHandler>();
        mockHttp.SetupAnyRequest().ReturnsResponse(HttpStatusCode.OK, "{}");

        var job = new UploadJob
        {
            Id = Guid.NewGuid(),
            ErpTarget = "DND",
            Status = JobStatus.Pending,
            SavedPath = "test.mp4"
        };

        // Act & Assert
        // job.Status should be Done after successful push
    }

    [Fact]
    public async Task PushAsync_ShouldIncrementRetry_WhenErpReturns500()
    {
        // RetryCount should increase, Status back to Pending with ScheduledAt set
    }

    [Fact]
    public async Task PushAsync_ShouldMarkFailed_WhenRetryExhausted()
    {
        // After RetryCount >= MaxRetry, Status should be Failed
    }
}
```

### JobService Tests

```csharp
// Tests/Services/JobServiceTests.cs
public class JobServiceTests
{
    private AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task PauseJob_ShouldChangeToPaused_WhenPending()
    {
        using var db = CreateInMemoryDb();
        var job = new UploadJob { Status = JobStatus.Pending };
        db.UploadJobs.Add(job);
        await db.SaveChangesAsync();

        var service = new JobService(db, Mock.Of<IHubContext<JobHub>>());
        await service.PauseJobAsync(job.Id);

        db.UploadJobs.First().Status.Should().Be(JobStatus.Paused);
    }

    [Fact]
    public async Task RetryJob_ShouldResetRetryCount_WhenFailed()
    {
        using var db = CreateInMemoryDb();
        var job = new UploadJob { Status = JobStatus.Failed, RetryCount = 3 };
        db.UploadJobs.Add(job);
        await db.SaveChangesAsync();

        var service = new JobService(db, Mock.Of<IHubContext<JobHub>>());
        await service.RetryJobAsync(job.Id);

        var updated = await db.UploadJobs.FirstAsync();
        updated.Status.Should().Be(JobStatus.Pending);
        updated.RetryCount.Should().Be(0);
    }
}
```

---

## 2. Integration Test – API Endpoints

### Setup

```csharp
// Tests/Integration/ApiIntegrationTests.cs
public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace DB với PostgreSQL test database (hoặc Testcontainers)
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(opt =>
                    opt.UseNpgsql("Host=localhost;Port=5432;Database=media_upload_test;Username=postgres;Password=yourpassword"));
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GetDashboardStats_ShouldReturn200()
    {
        var response = await _client.GetAsync("/api/dashboard/stats");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DashboardStatsDto>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTimeWindows_ShouldReturnSeededData()
    {
        var response = await _client.GetAsync("/api/config/timewindows");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var windows = await response.Content.ReadFromJsonAsync<List<TimeWindowDto>>();
        windows.Should().HaveCountGreaterOrEqualTo(2); // 2 seeded records
    }

    [Fact]
    public async Task PauseNonExistentJob_ShouldReturn404()
    {
        var response = await _client.PostAsync($"/api/jobs/{Guid.NewGuid()}/pause", null);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

---

## 3. Frontend Test (Jest + React Testing Library)

### Setup

```bash
npm install -D jest @testing-library/react @testing-library/user-event
npm install -D @testing-library/jest-dom msw
```

### StatsCards Test

```typescript
// __tests__/StatsCards.test.tsx
import { render, screen } from "@testing-library/react";
import { StatsCards } from "../pages/Dashboard/StatsCards";

test("renders all 4 stat cards", () => {
  const stats = { pending: 5, running: 2, done: 100, failed: 1 };
  render(<StatsCards stats={stats} />);

  expect(screen.getByText("5")).toBeInTheDocument();  // pending
  expect(screen.getByText("2")).toBeInTheDocument();  // running
  expect(screen.getByText("100")).toBeInTheDocument(); // done
  expect(screen.getByText("1")).toBeInTheDocument();  // failed
});
```

### TimeWindowForm Test

```typescript
// __tests__/TimeWindowForm.test.tsx
import { render, screen, fireEvent } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { TimeWindowForm } from "../pages/Config/TimeWindowForm";

test("submits form with correct values", async () => {
  const onSave = jest.fn();
  render(<TimeWindowForm open={true} initial={null} onSave={onSave} onClose={jest.fn()} />);

  await userEvent.type(screen.getByPlaceholderText(/VD:/), "Test Window");
  // ... chọn ngày, giờ
  fireEvent.click(screen.getByText("OK"));

  expect(onSave).toHaveBeenCalledWith(expect.objectContaining({
    name: "Test Window"
  }));
});
```

### MSW Mock API

```typescript
// __tests__/mocks/handlers.ts
import { http, HttpResponse } from "msw";

export const handlers = [
  http.get("/api/dashboard/stats", () => {
    return HttpResponse.json({ pending: 5, running: 2, done: 100, failed: 1 });
  }),
  http.get("/api/jobs", () => {
    return HttpResponse.json({ total: 3, page: 1, data: [] });
  }),
  http.post("/api/jobs/:id/retry", () => {
    return HttpResponse.json({ success: true, newStatus: "Pending" });
  }),
];
```

---

## 4. Postman Collection

File: `plan/postman/MediaUpload_API.postman_collection.json`

### Các test case thủ công theo thứ tự:

```
1. Upload Flow
   ├── POST /api/upload (1 file MP4 nhỏ)
   ├── Verify: response có fileId + jobId + status=Pending
   └── GET /api/jobs → confirm job tồn tại

2. TimeWindow Config
   ├── GET /api/config/timewindows (xem seeded data)
   ├── POST /api/config/timewindows (tạo window mới)
   ├── PUT /api/config/timewindows/{id} (sửa window)
   └── DELETE /api/config/timewindows/{id}

3. Worker Control
   ├── POST /api/worker/pause
   ├── GET /api/worker/status → confirm isPaused=true
   ├── POST /api/worker/resume
   └── GET /api/worker/status → confirm isPaused=false

4. Job Lifecycle
   ├── POST /api/jobs/{id}/pause
   ├── POST /api/jobs/{id}/resume
   ├── POST /api/jobs/{id}/retry (job Failed)
   └── DELETE /api/jobs/{id}

5. Dashboard
   ├── GET /api/dashboard/stats
   └── GET /api/dashboard/timeline?period=today
```

---

## 5. Load Test (k6)

```javascript
// plan/loadtest/upload_stress.js
import http from "k6/http";
import { check, sleep } from "k6";

export const options = {
  stages: [
    { duration: "1m", target: 10 },  // Ramp up to 10 users
    { duration: "3m", target: 10 },  // Hold
    { duration: "1m", target: 0 },   // Ramp down
  ],
  thresholds: {
    http_req_duration: ["p(95)<5000"], // 95% requests < 5s (file upload)
    http_req_failed: ["rate<0.01"],    // <1% failures
  },
};

export default function () {
  // Upload nhỏ để test concurrency
  const file = open("./sample_small.mp4", "b");
  const formData = {
    files: http.file(file, "test.mp4", "video/mp4"),
    userId: "TEST001",
    customerCode: "KHTest",
    erpTarget: "DND",
  };

  const res = http.post("http://localhost:5000/api/upload", formData);
  check(res, { "status 200": (r) => r.status === 200 });
  sleep(1);
}
```

---

## Checklist QA Thủ Công

| # | Test case | Kết quả mong đợi |
|---|-----------|-----------------|
| 1 | Upload file > 1500MB | Báo lỗi 413 |
| 2 | Upload file không phải video/audio | Báo lỗi 400 |
| 3 | Upload 6 files cùng lúc | Báo lỗi vượt giới hạn |
| 4 | Pause worker → upload file → xem job Pending | Job tạo nhưng không push |
| 5 | Resume worker trong giờ → job Pending chạy | Job chuyển Running → Done |
| 6 | ERP endpoint trả 500 | Job retry 3 lần rồi Failed |
| 7 | Retry job Failed | Job về Pending và chạy lại |
| 8 | Tạo TimeWindow ngoài giờ hành chính | Worker push đúng theo lịch |
| 9 | Dashboard realtime khi job Done | Stats cập nhật không cần F5 |
| 10 | Xóa file đang Running | Báo lỗi không cho xóa |
