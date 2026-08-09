using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pryde.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConvertBookingWindowHoursToMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BookingWindowHours",
                table: "Trips",
                newName: "BookingWindowMinutes");

            migrationBuilder.RenameColumn(
                name: "BookingWindowHours",
                table: "RecurringTrips",
                newName: "BookingWindowMinutes");

            migrationBuilder.Sql(
                """
                UPDATE "Trips"
                SET "BookingWindowMinutes" = "BookingWindowMinutes" * 60;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "RecurringTrips"
                SET "BookingWindowMinutes" = "BookingWindowMinutes" * 60;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Trips"
                SET "BookingWindowMinutes" = CEILING("BookingWindowMinutes" / 60.0)::integer;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "RecurringTrips"
                SET "BookingWindowMinutes" = CEILING("BookingWindowMinutes" / 60.0)::integer;
                """);

            migrationBuilder.RenameColumn(
                name: "BookingWindowMinutes",
                table: "Trips",
                newName: "BookingWindowHours");

            migrationBuilder.RenameColumn(
                name: "BookingWindowMinutes",
                table: "RecurringTrips",
                newName: "BookingWindowHours");
        }
    }
}
