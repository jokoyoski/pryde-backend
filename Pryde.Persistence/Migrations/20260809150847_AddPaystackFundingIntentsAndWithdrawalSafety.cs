using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pryde.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaystackFundingIntentsAndWithdrawalSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "WithdrawableBalance",
                table: "Wallets",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "DriverBankAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaystackFundingIntents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExpectedAmountKobo = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CustomerEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaystackTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaystackFundingIntents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaystackFundingIntents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                UPDATE "DriverBankAccounts"
                SET "VerifiedAt" = "CreatedAt"
                WHERE "VerifiedAt" IS NULL
                    AND "RecipientCode" <> '';
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Wallets" AS wallet
                SET "WithdrawableBalance" = GREATEST(
                    0,
                    COALESCE((
                        SELECT SUM(transaction."Amount")
                        FROM "WalletTransactions" AS transaction
                        WHERE transaction."WalletId" = wallet."Id"
                            AND transaction."Type" = 4
                    ), 0) - COALESCE((
                        SELECT SUM(transaction."Amount")
                        FROM "WalletTransactions" AS transaction
                        WHERE transaction."WalletId" = wallet."Id"
                            AND transaction."Type" = 5
                            AND transaction."Status" IN (1, 2)
                    ), 0));
                """);

            migrationBuilder.Sql(
                """
                UPDATE "LedgerTransactions"
                SET "ExternalReference" =
                    'reversal:' || "ExternalReference"
                WHERE "TransactionType" = 7
                    AND "ExternalReference" IS NOT NULL
                    AND "ExternalReference" NOT LIKE 'reversal:%';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_Provider_Reference",
                table: "WalletTransactions",
                columns: new[] { "Provider", "Reference" },
                unique: true,
                filter: "\"Provider\" IS NOT NULL AND \"Reference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_ExternalReference",
                table: "LedgerTransactions",
                column: "ExternalReference",
                unique: true,
                filter: "\"ExternalReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaystackFundingIntents_PaystackTransactionId",
                table: "PaystackFundingIntents",
                column: "PaystackTransactionId",
                unique: true,
                filter: "\"PaystackTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaystackFundingIntents_Reference",
                table: "PaystackFundingIntents",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaystackFundingIntents_UserId_CreatedAt",
                table: "PaystackFundingIntents",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaystackFundingIntents");

            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_Provider_Reference",
                table: "WalletTransactions");

            migrationBuilder.DropIndex(
                name: "IX_LedgerTransactions_ExternalReference",
                table: "LedgerTransactions");

            migrationBuilder.DropColumn(
                name: "WithdrawableBalance",
                table: "Wallets");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "DriverBankAccounts");
        }
    }
}
