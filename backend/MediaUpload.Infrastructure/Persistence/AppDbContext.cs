using Microsoft.EntityFrameworkCore;
using MediaUpload.Domain.Entities;

namespace MediaUpload.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<UploadJob> UploadJobs => Set<UploadJob>();
    public DbSet<TimeWindowConfig> TimeWindowConfigs => Set<TimeWindowConfig>();
    public DbSet<ErpEndpointConfig> ErpEndpointConfigs => Set<ErpEndpointConfig>();
    public DbSet<ApiCredential> ApiCredentials => Set<ApiCredential>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<UploadJob>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAtUtc);
            e.Property(x => x.Status).HasConversion<int>();
        });

        model.Entity<TimeWindowConfig>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
        });

        model.Entity<ErpEndpointConfig>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.HasIndex(x => x.Target).IsUnique();
        });

        model.Entity<ApiCredential>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.AuthType).HasConversion<int>();
        });

        model.Entity<SystemSetting>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.HasIndex(x => x.Key).IsUnique();
        });

        SeedData(model);
    }

    private static void SeedData(ModelBuilder model)
    {
        // QUAN TRỌNG: SystemSetting.UpdatedAtUtc có initializer mặc định là
        // DateTime.UtcNow – nếu không set rõ ràng ở đây, mỗi lần EF build model
        // (tức mỗi lần app khởi động) giá trị seed sẽ khác nhau, khiến EF Core
        // coi model là "non-deterministic" và báo lỗi PendingModelChangesWarning
        // liên tục. => Luôn gán cố định UpdatedAtUtc cho seed data.
        var seedTimestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Seed system settings
        model.Entity<SystemSetting>().HasData(
            new SystemSetting { Id = 1,  Key = "nas.upload_dir",               Value = "/mnt/nas/uploads",        Description = "Thư mục lưu file upload trên NAS",           HotReload = true, UpdatedAtUtc = seedTimestamp },
            new SystemSetting { Id = 2,  Key = "nas.logs_dir",                 Value = "/mnt/nas/logs",           Description = "Thư mục lưu log",                            HotReload = true, UpdatedAtUtc = seedTimestamp },
            new SystemSetting { Id = 3,  Key = "nas.min_free_space_bytes",     Value = "1073741824",              Description = "Dung lượng trống tối thiểu (bytes)",          HotReload = true, UpdatedAtUtc = seedTimestamp },
            new SystemSetting { Id = 4,  Key = "nas.video_path_prefix",        Value = "/homes/video/uploads/",   Description = "Prefix đường dẫn video gửi lên ERP",          HotReload = true, UpdatedAtUtc = seedTimestamp },
            new SystemSetting { Id = 5,  Key = "upload.max_file_size_bytes",   Value = "1572864000",              Description = "Dung lượng tối đa mỗi file (bytes)",          HotReload = true, UpdatedAtUtc = seedTimestamp },
            new SystemSetting { Id = 6,  Key = "upload.max_files_per_request", Value = "5",                       Description = "Số file tối đa mỗi request",                  HotReload = true, UpdatedAtUtc = seedTimestamp },
            new SystemSetting { Id = 7,  Key = "upload.allowed_extensions",    Value = ".mp4,.avi,.mov,.mkv,.wmv,.flv", Description = "Các đuôi file video được phép",        HotReload = true, UpdatedAtUtc = seedTimestamp },
            new SystemSetting { Id = 8,  Key = "worker.tick_interval_seconds", Value = "30",                      Description = "Chu kỳ worker kiểm tra hàng đợi (giây)",      HotReload = true, UpdatedAtUtc = seedTimestamp },
            new SystemSetting { Id = 9,  Key = "worker.max_retry",             Value = "3",                       Description = "Số lần retry tối đa trước khi Failed",        HotReload = true, UpdatedAtUtc = seedTimestamp },
            new SystemSetting { Id = 10, Key = "worker.retry_delay_minutes",   Value = "5",                       Description = "Delay giữa các lần retry (phút)",             HotReload = true, UpdatedAtUtc = seedTimestamp },
            new SystemSetting { Id = 11, Key = "worker.max_concurrent",        Value = "3",                       Description = "Số job chạy song song tối đa",                HotReload = true, UpdatedAtUtc = seedTimestamp },
            new SystemSetting { Id = 12, Key = "ratelimit.window_ms",          Value = "900000",                  Description = "Cửa sổ rate limit (ms)",                      HotReload = true, UpdatedAtUtc = seedTimestamp },
            new SystemSetting { Id = 13, Key = "ratelimit.max_requests",       Value = "20",                      Description = "Số request tối đa trong cửa sổ",              HotReload = true, UpdatedAtUtc = seedTimestamp },
            new SystemSetting { Id = 14, Key = "cors.allowed_origins",         Value = "http://localhost:5173",   Description = "Danh sách origin CORS (phân cách bằng dấu phẩy)", HotReload = true, UpdatedAtUtc = seedTimestamp },
            new SystemSetting { Id = 15, Key = "system.timezone",              Value = "Asia/Ho_Chi_Minh",        Description = "Timezone hiển thị (IANA ID)",                 HotReload = false, UpdatedAtUtc = seedTimestamp }
        );

        // Seed admin credential
        // Raw token: CHANGE_THIS_ON_FIRST_LOGIN (rotate via UI immediately)
        const string adminRawToken = "MediaUploadAdmin2024!ChangeMe";
        // QUAN TRỌNG: KHÔNG gọi BCrypt.HashPassword(...) trực tiếp trong HasData –
        // hàm này sinh salt NGẪU NHIÊN mỗi lần build model, khiến EF Core coi
        // seed data là "non-deterministic" và báo lỗi PendingModelChangesWarning
        // mỗi khi app khởi động (dù không có migration nào thay đổi thật sự).
        // => Hash cố định, tính sẵn 1 lần cho đúng adminRawToken ở trên
        //    (workFactor: 12). Nếu đổi adminRawToken, phải tính lại hash này.
        const string adminHashedSecret = "$2a$12$5jSzye5F9/NFOW9NR9U1qOGL0yz.2MKdsRTyeoozkykcrgbkSgRSy";
        model.Entity<ApiCredential>().HasData(new ApiCredential
        {
            Id = 1,
            Name = "admin",
            AuthType = Domain.Enums.AuthType.Bearer,
            HashedSecret = adminHashedSecret,
            TokenPrefix = adminRawToken[..8],
            CanUpload = true,
            CanReadJobs = true,
            CanConfig = true,
            AllowedErp = "",
            Enabled = true,
            CreatedAtUtc = seedTimestamp,
        });
    }
}
