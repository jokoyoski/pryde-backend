using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pryde.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileSmileIdHostedAttemptSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdentityOptions",
                table: "KycVerificationAttempts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentityType",
                table: "KycVerificationAttempts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationMethod",
                table: "KycVerificationAttempts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE "KycVerificationAttempts"
                    ADD COLUMN IF NOT EXISTS "VerificationUrl" character varying(2048);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdentityOptions",
                table: "KycVerificationAttempts");

            migrationBuilder.DropColumn(
                name: "IdentityType",
                table: "KycVerificationAttempts");

            migrationBuilder.DropColumn(
                name: "VerificationMethod",
                table: "KycVerificationAttempts");

            migrationBuilder.DropColumn(
                name: "VerificationUrl",
                table: "KycVerificationAttempts");
        }
    }
}
