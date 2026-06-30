using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaUpload.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixTimeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ApiCredentials",
                keyColumn: "Id",
                keyValue: 1,
                column: "HashedSecret",
                value: "$2a$12$j26bGbbwpo5vxMegJsWyreZaKFUofY7Tr67.DWR9zl.U1qtwJm08i");

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 27, 10, 406, DateTimeKind.Utc).AddTicks(2863));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 2,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 27, 10, 406, DateTimeKind.Utc).AddTicks(5383));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 3,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 27, 10, 406, DateTimeKind.Utc).AddTicks(5386));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 4,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 27, 10, 406, DateTimeKind.Utc).AddTicks(5387));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 5,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 27, 10, 406, DateTimeKind.Utc).AddTicks(5387));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 6,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 27, 10, 406, DateTimeKind.Utc).AddTicks(5388));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 7,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 27, 10, 406, DateTimeKind.Utc).AddTicks(5389));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 8,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 27, 10, 406, DateTimeKind.Utc).AddTicks(5390));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 9,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 27, 10, 406, DateTimeKind.Utc).AddTicks(5391));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 10,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 27, 10, 406, DateTimeKind.Utc).AddTicks(5392));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 11,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 27, 10, 406, DateTimeKind.Utc).AddTicks(5393));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 12,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 27, 10, 406, DateTimeKind.Utc).AddTicks(5394));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 13,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 27, 10, 406, DateTimeKind.Utc).AddTicks(5395));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 14,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 27, 10, 406, DateTimeKind.Utc).AddTicks(5396));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 15,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 27, 10, 406, DateTimeKind.Utc).AddTicks(5396));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ApiCredentials",
                keyColumn: "Id",
                keyValue: 1,
                column: "HashedSecret",
                value: "$2a$12$FGjZfJHUuTNkFXJIAnBYBOxyqITIIxipi9dJDxliZsLCL1uhYRxRi");

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 21, 22, 588, DateTimeKind.Utc).AddTicks(3765));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 2,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 21, 22, 588, DateTimeKind.Utc).AddTicks(6841));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 3,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 21, 22, 588, DateTimeKind.Utc).AddTicks(6846));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 4,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 21, 22, 588, DateTimeKind.Utc).AddTicks(6847));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 5,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 21, 22, 588, DateTimeKind.Utc).AddTicks(6848));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 6,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 21, 22, 588, DateTimeKind.Utc).AddTicks(6849));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 7,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 21, 22, 588, DateTimeKind.Utc).AddTicks(6850));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 8,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 21, 22, 588, DateTimeKind.Utc).AddTicks(6851));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 9,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 21, 22, 588, DateTimeKind.Utc).AddTicks(6877));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 10,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 21, 22, 588, DateTimeKind.Utc).AddTicks(6878));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 11,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 21, 22, 588, DateTimeKind.Utc).AddTicks(6879));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 12,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 21, 22, 588, DateTimeKind.Utc).AddTicks(6880));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 13,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 21, 22, 588, DateTimeKind.Utc).AddTicks(6881));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 14,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 21, 22, 588, DateTimeKind.Utc).AddTicks(6882));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 15,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 21, 22, 588, DateTimeKind.Utc).AddTicks(6883));
        }
    }
}
