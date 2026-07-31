using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pryde.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingPaymentExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentExpiresAt",
                table: "TripBookings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripBookings_Status_PaidAt_PaymentExpiresAt",
                table: "TripBookings",
                columns: new[] { "Status", "PaidAt", "PaymentExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TripBookings_Status_PaidAt_PaymentExpiresAt",
                table: "TripBookings");

            migrationBuilder.DropColumn(
                name: "PaymentExpiresAt",
                table: "TripBookings");
        }
    }
}
