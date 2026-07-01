using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaUpload.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminBasicLoginCredential : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ApiCredentials",
                columns: new[] { "Id", "AllowedErp", "AuthType", "CanConfig", "CanReadJobs", "CanUpload", "CreatedAtUtc", "Enabled", "HashedSecret", "LastUsedAtUtc", "Name", "TokenPrefix", "Username" },
                values: new object[] { 2, "", 1, true, true, true, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "$2a$12$5jSzye5F9/NFOW9NR9U1qOGL0yz.2MKdsRTyeoozkykcrgbkSgRSy", null, "admin (web login)", "MediaUpl", "admin" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ApiCredentials",
                keyColumn: "Id",
                keyValue: 2);
        }
    }
}
