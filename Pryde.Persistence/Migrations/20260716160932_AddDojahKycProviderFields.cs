using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pryde.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDojahKycProviderFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastProviderUpdatedAt",
                table: "KycVerifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderName",
                table: "KycVerifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderReference",
                table: "KycVerifications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderStatus",
                table: "KycVerifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "KycVerifications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KycVerifications_ProviderReference",
                table: "KycVerifications",
                column: "ProviderReference",
                unique: true,
                filter: "\"ProviderReference\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KycVerifications_ProviderReference",
                table: "KycVerifications");

            migrationBuilder.DropColumn(
                name: "LastProviderUpdatedAt",
                table: "KycVerifications");

            migrationBuilder.DropColumn(
                name: "ProviderName",
                table: "KycVerifications");

            migrationBuilder.DropColumn(
                name: "ProviderReference",
                table: "KycVerifications");

            migrationBuilder.DropColumn(
                name: "ProviderStatus",
                table: "KycVerifications");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "KycVerifications");
        }
    }
}
