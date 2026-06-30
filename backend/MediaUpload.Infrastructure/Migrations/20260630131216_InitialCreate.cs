using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MediaUpload.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiCredentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    AuthType = table.Column<int>(type: "integer", nullable: false),
                    HashedSecret = table.Column<string>(type: "text", nullable: false),
                    TokenPrefix = table.Column<string>(type: "text", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: true),
                    CanUpload = table.Column<bool>(type: "boolean", nullable: false),
                    CanReadJobs = table.Column<bool>(type: "boolean", nullable: false),
                    CanConfig = table.Column<bool>(type: "boolean", nullable: false),
                    AllowedErp = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiCredentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ErpEndpointConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Target = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    EncryptedToken = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpEndpointConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    HotReload = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeWindowConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    DaysOfWeek = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeWindowConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UploadJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<string>(type: "text", nullable: false),
                    OriginalFileName = table.Column<string>(type: "text", nullable: false),
                    SavedPath = table.Column<string>(type: "text", nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ErpTarget = table.Column<string>(type: "text", nullable: false),
                    Longitude = table.Column<string>(type: "text", nullable: true),
                    Latitude = table.Column<string>(type: "text", nullable: true),
                    FlowId = table.Column<string>(type: "text", nullable: true),
                    OrderId = table.Column<string>(type: "text", nullable: true),
                    NvktId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    MaxRetry = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadJobs", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "SystemSettings",
                columns: new[] { "Id", "Description", "HotReload", "Key", "UpdatedAtUtc", "Value" },
                values: new object[,]
                {
                    { 1, "Thư mục lưu file upload trên NAS", true, "nas.upload_dir", new DateTime(2026, 6, 30, 13, 12, 10, 282, DateTimeKind.Utc).AddTicks(7713), "/mnt/nas/uploads" },
                    { 2, "Thư mục lưu log", true, "nas.logs_dir", new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4188), "/mnt/nas/logs" },
                    { 3, "Dung lượng trống tối thiểu (bytes)", true, "nas.min_free_space_bytes", new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4198), "1073741824" },
                    { 4, "Prefix đường dẫn video gửi lên ERP", true, "nas.video_path_prefix", new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4199), "/homes/video/uploads/" },
                    { 5, "Dung lượng tối đa mỗi file (bytes)", true, "upload.max_file_size_bytes", new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4200), "1572864000" },
                    { 6, "Số file tối đa mỗi request", true, "upload.max_files_per_request", new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4201), "5" },
                    { 7, "Các đuôi file video được phép", true, "upload.allowed_extensions", new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4202), ".mp4,.avi,.mov,.mkv,.wmv,.flv" },
                    { 8, "Chu kỳ worker kiểm tra hàng đợi (giây)", true, "worker.tick_interval_seconds", new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4203), "30" },
                    { 9, "Số lần retry tối đa trước khi Failed", true, "worker.max_retry", new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4204), "3" },
                    { 10, "Delay giữa các lần retry (phút)", true, "worker.retry_delay_minutes", new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4205), "5" },
                    { 11, "Số job chạy song song tối đa", true, "worker.max_concurrent", new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4206), "3" },
                    { 12, "Cửa sổ rate limit (ms)", true, "ratelimit.window_ms", new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4207), "900000" },
                    { 13, "Số request tối đa trong cửa sổ", true, "ratelimit.max_requests", new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4208), "20" },
                    { 14, "Danh sách origin CORS (phân cách bằng dấu phẩy)", true, "cors.allowed_origins", new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4209), "http://localhost:5173" },
                    { 15, "Timezone hiển thị (IANA ID)", false, "system.timezone", new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4210), "Asia/Ho_Chi_Minh" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErpEndpointConfigs_Target",
                table: "ErpEndpointConfigs",
                column: "Target",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_Key",
                table: "SystemSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UploadJobs_CreatedAtUtc",
                table: "UploadJobs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UploadJobs_Status",
                table: "UploadJobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiCredentials");

            migrationBuilder.DropTable(
                name: "ErpEndpointConfigs");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "TimeWindowConfigs");

            migrationBuilder.DropTable(
                name: "UploadJobs");
        }
    }
}
