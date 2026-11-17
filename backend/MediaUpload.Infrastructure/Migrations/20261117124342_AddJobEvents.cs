using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MediaUpload.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJobEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Step = table.Column<string>(type: "text", nullable: false),
                    Detail = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobEvents_UploadJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "UploadJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobEvents_JobId",
                table: "JobEvents",
                column: "JobId");

            // Backfill timeline cho job có sẵn từ các cột thời gian hiện có (chỉ có mốc
            // nhận file / lần gửi ERP cuối / kết quả – không có lịch sử từng lần retry).
            // Status: 2=Success, 3=Failed, 4=Cancelled.
            migrationBuilder.Sql("""
                INSERT INTO "JobEvents" ("JobId", "AtUtc", "Step", "Detail")
                SELECT "Id", "CreatedAtUtc", 'Received', 'Dữ liệu cũ (trước khi có timeline chi tiết)' FROM "UploadJobs";

                INSERT INTO "JobEvents" ("JobId", "AtUtc", "Step", "Detail")
                SELECT "Id", "ProcessedAtUtc", 'Processing', 'Lần gửi ERP gần nhất' FROM "UploadJobs"
                WHERE "ProcessedAtUtc" IS NOT NULL;

                INSERT INTO "JobEvents" ("JobId", "AtUtc", "Step", "Detail")
                SELECT "Id", "CompletedAtUtc",
                       CASE "Status" WHEN 2 THEN 'ErpSuccess' WHEN 3 THEN 'GaveUp' ELSE 'Cancelled' END,
                       "LastError"
                FROM "UploadJobs"
                WHERE "CompletedAtUtc" IS NOT NULL AND "Status" IN (2, 3, 4);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobEvents");
        }
    }
}
