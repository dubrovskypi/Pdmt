using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pdmt.Api.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddIndexws : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "RefreshTokens",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "FailedLoginAttempts",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FailedLoginAttempts_Email",
                table: "FailedLoginAttempts",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_FailedLoginAttempts_OccurredAtUtc",
                table: "FailedLoginAttempts",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Timestamp",
                table: "Events",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Events_UserId_Timestamp",
                table: "Events",
                columns: new[] { "UserId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_FailedLoginAttempts_Email",
                table: "FailedLoginAttempts");

            migrationBuilder.DropIndex(
                name: "IX_FailedLoginAttempts_OccurredAtUtc",
                table: "FailedLoginAttempts");

            migrationBuilder.DropIndex(
                name: "IX_Events_Timestamp",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_UserId_Timestamp",
                table: "Events");

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "RefreshTokens",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "FailedLoginAttempts",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
