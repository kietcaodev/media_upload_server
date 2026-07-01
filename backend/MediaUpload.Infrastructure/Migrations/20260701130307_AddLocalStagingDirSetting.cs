using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaUpload.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalStagingDirSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "SystemSettings",
                columns: new[] { "Id", "Description", "HotReload", "Key", "UpdatedAtUtc", "Value" },
                values: new object[] { 16, "Thư mục lưu tạm local trước khi chuyển sang NAS (ngoài time window)", true, "nas.local_staging_dir", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "/opt/media-upload/staging" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 16);
        }
    }
}
