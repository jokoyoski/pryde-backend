using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pryde.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSmileIdKycAttemptFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttemptGroupReference",
                table: "KycVerificationAttempts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CallbackPayloadHash",
                table: "KycVerificationAttempts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalUserReference",
                table: "KycVerificationAttempts",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FlowType",
                table: "KycVerificationAttempts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderEventTimestamp",
                table: "KycVerificationAttempts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResultText",
                table: "KycVerificationAttempts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SmileActionSucceeded",
                table: "KycVerificationAttempts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SmileIdentitySucceeded",
                table: "KycVerificationAttempts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "VerificationUrl",
                table: "KycVerificationAttempts",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttemptGroupReference",
                table: "KycVerificationAttempts");

            migrationBuilder.DropColumn(
                name: "CallbackPayloadHash",
                table: "KycVerificationAttempts");

            migrationBuilder.DropColumn(
                name: "ExternalUserReference",
                table: "KycVerificationAttempts");

            migrationBuilder.DropColumn(
                name: "FlowType",
                table: "KycVerificationAttempts");

            migrationBuilder.DropColumn(
                name: "ProviderEventTimestamp",
                table: "KycVerificationAttempts");

            migrationBuilder.DropColumn(
                name: "ResultText",
                table: "KycVerificationAttempts");

            migrationBuilder.DropColumn(
                name: "SmileActionSucceeded",
                table: "KycVerificationAttempts");

            migrationBuilder.DropColumn(
                name: "SmileIdentitySucceeded",
                table: "KycVerificationAttempts");

            migrationBuilder.DropColumn(
                name: "VerificationUrl",
                table: "KycVerificationAttempts");
        }
    }
}
