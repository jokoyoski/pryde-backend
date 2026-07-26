using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pryde.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDojahWebhookReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DojahReference",
                table: "KycVerifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KycVerifications_DojahReference",
                table: "KycVerifications",
                column: "DojahReference",
                unique: true,
                filter: "\"DojahReference\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KycVerifications_DojahReference",
                table: "KycVerifications");

            migrationBuilder.DropColumn(
                name: "DojahReference",
                table: "KycVerifications");
        }
    }
}
