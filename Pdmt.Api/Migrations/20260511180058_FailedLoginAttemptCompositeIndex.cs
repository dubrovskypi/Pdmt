using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pdmt.Api.Migrations
{
    /// <inheritdoc />
    public partial class FailedLoginAttemptCompositeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FailedLoginAttempts_Email",
                table: "FailedLoginAttempts");

            migrationBuilder.DropIndex(
                name: "IX_FailedLoginAttempts_OccurredAtUtc",
                table: "FailedLoginAttempts");

            migrationBuilder.CreateIndex(
                name: "IX_FailedLoginAttempts_Email_OccurredAtUtc",
                table: "FailedLoginAttempts",
                columns: new[] { "Email", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FailedLoginAttempts_Email_OccurredAtUtc",
                table: "FailedLoginAttempts");

            migrationBuilder.CreateIndex(
                name: "IX_FailedLoginAttempts_Email",
                table: "FailedLoginAttempts",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_FailedLoginAttempts_OccurredAtUtc",
                table: "FailedLoginAttempts",
                column: "OccurredAtUtc");
        }
    }
}
