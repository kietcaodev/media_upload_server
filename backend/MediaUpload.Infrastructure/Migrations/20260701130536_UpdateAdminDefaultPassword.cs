using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaUpload.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAdminDefaultPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ApiCredentials",
                keyColumn: "Id",
                keyValue: 1,
                column: "HashedSecret",
                value: "$2a$12$9ablaBC2kGW/GEpm/Lml3.v.956o.gNZY68.jUg.I9Ps/tvoEIRcO");

            migrationBuilder.UpdateData(
                table: "ApiCredentials",
                keyColumn: "Id",
                keyValue: 2,
                column: "HashedSecret",
                value: "$2a$12$9ablaBC2kGW/GEpm/Lml3.v.956o.gNZY68.jUg.I9Ps/tvoEIRcO");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "ApiCredentials",
                keyColumn: "Id",
                keyValue: 1,
                column: "HashedSecret",
                value: "$2a$12$5jSzye5F9/NFOW9NR9U1qOGL0yz.2MKdsRTyeoozkykcrgbkSgRSy");

            migrationBuilder.UpdateData(
                table: "ApiCredentials",
                keyColumn: "Id",
                keyValue: 2,
                column: "HashedSecret",
                value: "$2a$12$5jSzye5F9/NFOW9NR9U1qOGL0yz.2MKdsRTyeoozkykcrgbkSgRSy");
        }
    }
}
