using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pryde.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteTripBookingFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TripBookings_TripId_PassengerId",
                table: "TripBookings");

            migrationBuilder.CreateIndex(
                name: "IX_TripBookings_TripId_PassengerId",
                table: "TripBookings",
                columns: new[] { "TripId", "PassengerId" },
                unique: true,
                filter: "\"Status\" IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TripBookings_TripId_PassengerId",
                table: "TripBookings");

            migrationBuilder.CreateIndex(
                name: "IX_TripBookings_TripId_PassengerId",
                table: "TripBookings",
                columns: new[] { "TripId", "PassengerId" });
        }
    }
}
