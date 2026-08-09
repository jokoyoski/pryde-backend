using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pryde.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenamePaystackFundingIntentsToWalletFundingRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaystackFundingIntents_Users_UserId",
                table: "PaystackFundingIntents");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PaystackFundingIntents",
                table: "PaystackFundingIntents");

            migrationBuilder.RenameTable(
                name: "PaystackFundingIntents",
                newName: "PaystackWalletFundingRequests");

            migrationBuilder.RenameIndex(
                name: "IX_PaystackFundingIntents_UserId_CreatedAt",
                table: "PaystackWalletFundingRequests",
                newName: "IX_PaystackWalletFundingRequests_UserId_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_PaystackFundingIntents_Reference",
                table: "PaystackWalletFundingRequests",
                newName: "IX_PaystackWalletFundingRequests_Reference");

            migrationBuilder.RenameIndex(
                name: "IX_PaystackFundingIntents_PaystackTransactionId",
                table: "PaystackWalletFundingRequests",
                newName: "IX_PaystackWalletFundingRequests_PaystackTransactionId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PaystackWalletFundingRequests",
                table: "PaystackWalletFundingRequests",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PaystackWalletFundingRequests_Users_UserId",
                table: "PaystackWalletFundingRequests",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaystackWalletFundingRequests_Users_UserId",
                table: "PaystackWalletFundingRequests");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PaystackWalletFundingRequests",
                table: "PaystackWalletFundingRequests");

            migrationBuilder.RenameTable(
                name: "PaystackWalletFundingRequests",
                newName: "PaystackFundingIntents");

            migrationBuilder.RenameIndex(
                name: "IX_PaystackWalletFundingRequests_UserId_CreatedAt",
                table: "PaystackFundingIntents",
                newName: "IX_PaystackFundingIntents_UserId_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_PaystackWalletFundingRequests_Reference",
                table: "PaystackFundingIntents",
                newName: "IX_PaystackFundingIntents_Reference");

            migrationBuilder.RenameIndex(
                name: "IX_PaystackWalletFundingRequests_PaystackTransactionId",
                table: "PaystackFundingIntents",
                newName: "IX_PaystackFundingIntents_PaystackTransactionId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PaystackFundingIntents",
                table: "PaystackFundingIntents",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PaystackFundingIntents_Users_UserId",
                table: "PaystackFundingIntents",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
