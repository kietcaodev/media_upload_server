using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaUpload.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAllowedExtensionsDefault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Description", "Value" },
                values: new object[] { "Các đuôi file video/audio/ảnh được phép", ".mp4,.avi,.mov,.mkv,.flv,.wmv,.webm,.3gp,.mp3,.wav,.ogg,.aac,.flac,.m4a,.wma,.jpg,.jpeg,.png,.gif,.bmp,.webp,.tiff,.tif,.ico,.heic,.heif" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Description", "Value" },
                values: new object[] { "Các đuôi file video được phép", ".mp4,.avi,.mov,.mkv,.wmv,.flv" });
        }
    }
}
