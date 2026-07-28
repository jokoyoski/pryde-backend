using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pryde.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverWithdrawals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_WalletId",
                table: "WalletTransactions");

            migrationBuilder.AddColumn<string>(
                name: "AccountName",
                table: "WalletTransactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BankName",
                table: "WalletTransactions",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "WalletTransactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "WalletTransactions",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "WalletTransactions",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaskedAccountNumber",
                table: "WalletTransactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "WalletTransactions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "WalletTransactions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_WalletId_Type_CreatedAt",
                table: "WalletTransactions",
                columns: new[] { "WalletId", "Type", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WalletTransactions_WalletId_Type_CreatedAt",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "AccountName",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "BankName",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "MaskedAccountNumber",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "WalletTransactions");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_WalletId",
                table: "WalletTransactions",
                column: "WalletId");
        }
    }
}
