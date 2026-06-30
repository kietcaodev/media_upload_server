using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaUpload.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminCredentialSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ApiCredentials",
                columns: new[] { "Id", "AllowedErp", "AuthType", "CanConfig", "CanReadJobs", "CanUpload", "CreatedAtUtc", "Enabled", "HashedSecret", "LastUsedAtUtc", "Name", "TokenPrefix", "Username" },
                values: new object[] { 1, "", 0, true, true, true, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "$2a$12$FGjZfJHUuTNkFXJIAnBYBOxyqITIIxipi9dJDxliZsLCL1uhYRxRi", null, "admin", "MediaUpl", null });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ApiCredentials",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 12, 10, 282, DateTimeKind.Utc).AddTicks(7713));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 2,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4188));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 3,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4198));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 4,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4199));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 5,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4200));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 6,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4201));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 7,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4202));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 8,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4203));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 9,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4204));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 10,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4205));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 11,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4206));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 12,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4207));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 13,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4208));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 14,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4209));

            migrationBuilder.UpdateData(
                table: "SystemSettings",
                keyColumn: "Id",
                keyValue: 15,
                column: "UpdatedAtUtc",
                value: new DateTime(2026, 6, 30, 13, 12, 10, 284, DateTimeKind.Utc).AddTicks(4210));
        }
    }
}
