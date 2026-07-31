using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pryde.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripAutoCompletionSafeguards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AutoCompletedAt",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmationDeadline",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DriverEndedAt",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WasAutoCompleted",
                table: "Trips",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_Status_ConfirmationDeadline",
                table: "Trips",
                columns: new[] { "Status", "ConfirmationDeadline" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trips_Status_ConfirmationDeadline",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "AutoCompletedAt",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ConfirmationDeadline",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DriverEndedAt",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "WasAutoCompleted",
                table: "Trips");
        }
    }
}
