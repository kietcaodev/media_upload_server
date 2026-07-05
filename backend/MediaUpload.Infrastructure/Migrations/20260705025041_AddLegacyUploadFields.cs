using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaUpload.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLegacyUploadFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompanyId",
                table: "UploadJobs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomFilename",
                table: "UploadJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrdCode",
                table: "UploadJobs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RelativePath",
                table: "UploadJobs",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "UploadJobs",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "UploadJobs");

            migrationBuilder.DropColumn(
                name: "CustomFilename",
                table: "UploadJobs");

            migrationBuilder.DropColumn(
                name: "OrdCode",
                table: "UploadJobs");

            migrationBuilder.DropColumn(
                name: "RelativePath",
                table: "UploadJobs");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "UploadJobs");
        }
    }
}
