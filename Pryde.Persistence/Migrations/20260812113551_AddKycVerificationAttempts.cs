using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pryde.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKycVerificationAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KycVerificationAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KycVerificationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CorrelationReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RawStatus = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResultCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProviderUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KycVerificationAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KycVerificationAttempts_KycVerifications_KycVerificationId",
                        column: x => x.KycVerificationId,
                        principalTable: "KycVerifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "KycVerificationAttempts" (
                    "Id",
                    "KycVerificationId",
                    "ProviderName",
                    "CorrelationReference",
                    "ProviderReference",
                    "Status",
                    "RawStatus",
                    "ResultCode",
                    "FailureReason",
                    "StartedAt",
                    "ProviderUpdatedAt",
                    "CompletedAt",
                    "CreatedAt",
                    "UpdatedAt",
                    "IsDeleted")
                SELECT
                    md5("Id"::text || ':' || "ProviderReference")::uuid,
                    "Id",
                    COALESCE(NULLIF("ProviderName", ''), 'Dojah'),
                    "ProviderReference",
                    "DojahReference",
                    "Status",
                    "ProviderStatus",
                    "ProviderStatus",
                    "RejectionReason",
                    "CreatedAt",
                    "LastProviderUpdatedAt",
                    CASE
                        WHEN "Status" IN (3, 4)
                            THEN COALESCE("VerifiedAt", "LastProviderUpdatedAt")
                        ELSE NULL
                    END,
                    "CreatedAt",
                    "UpdatedAt",
                    FALSE
                FROM "KycVerifications"
                WHERE "ProviderReference" IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_KycVerificationAttempts_KycVerificationId",
                table: "KycVerificationAttempts",
                column: "KycVerificationId");

            migrationBuilder.CreateIndex(
                name: "IX_KycVerificationAttempts_ProviderName_CorrelationReference",
                table: "KycVerificationAttempts",
                columns: new[] { "ProviderName", "CorrelationReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KycVerificationAttempts_ProviderName_ProviderReference",
                table: "KycVerificationAttempts",
                columns: new[] { "ProviderName", "ProviderReference" },
                unique: true,
                filter: "\"ProviderReference\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KycVerificationAttempts");
        }
    }
}
