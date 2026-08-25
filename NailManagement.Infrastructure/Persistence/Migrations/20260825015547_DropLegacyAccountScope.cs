using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NailManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyAccountScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BranchCode",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "BranchName",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "TenantName",
                table: "AppUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BranchCode",
                table: "AppUsers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BranchName",
                table: "AppUsers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "AppUsers",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantName",
                table: "AppUsers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
