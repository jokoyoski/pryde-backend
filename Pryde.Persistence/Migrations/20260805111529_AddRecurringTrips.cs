using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pryde.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringTrips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trips_RecurringTripId",
                table: "Trips");

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "TripSubscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "TripSubscriptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowLuggage",
                table: "RecurringTrips",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AvailableSeats",
                table: "RecurringTrips",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BookingWindowHours",
                table: "RecurringTrips",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "RecurringTrips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "DepartureTime",
                table: "RecurringTrips",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "DestinationAddress",
                table: "RecurringTrips",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "DestinationLatitude",
                table: "RecurringTrips",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "DestinationLongitude",
                table: "RecurringTrips",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "DistanceKm",
                table: "RecurringTrips",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedDurationMinutes",
                table: "RecurringTrips",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OriginAddress",
                table: "RecurringTrips",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "OriginLatitude",
                table: "RecurringTrips",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "OriginLongitude",
                table: "RecurringTrips",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "RoutePolyline",
                table: "RecurringTrips",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VehicleId",
                table: "RecurringTrips",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_RecurringTripId_DepartureTime",
                table: "Trips",
                columns: new[] { "RecurringTripId", "DepartureTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTrips_IsActive_StartDate_EndDate",
                table: "RecurringTrips",
                columns: new[] { "IsActive", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTrips_VehicleId",
                table: "RecurringTrips",
                column: "VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringTrips_Vehicles_VehicleId",
                table: "RecurringTrips",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecurringTrips_Vehicles_VehicleId",
                table: "RecurringTrips");

            migrationBuilder.DropIndex(
                name: "IX_Trips_RecurringTripId_DepartureTime",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_RecurringTrips_IsActive_StartDate_EndDate",
                table: "RecurringTrips");

            migrationBuilder.DropIndex(
                name: "IX_RecurringTrips_VehicleId",
                table: "RecurringTrips");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "TripSubscriptions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "TripSubscriptions");

            migrationBuilder.DropColumn(
                name: "AllowLuggage",
                table: "RecurringTrips");

            migrationBuilder.DropColumn(
                name: "AvailableSeats",
                table: "RecurringTrips");

            migrationBuilder.DropColumn(
                name: "BookingWindowHours",
                table: "RecurringTrips");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "RecurringTrips");

            migrationBuilder.DropColumn(
                name: "DepartureTime",
                table: "RecurringTrips");

            migrationBuilder.DropColumn(
                name: "DestinationAddress",
                table: "RecurringTrips");

            migrationBuilder.DropColumn(
                name: "DestinationLatitude",
                table: "RecurringTrips");

            migrationBuilder.DropColumn(
                name: "DestinationLongitude",
                table: "RecurringTrips");

            migrationBuilder.DropColumn(
                name: "DistanceKm",
                table: "RecurringTrips");

            migrationBuilder.DropColumn(
                name: "EstimatedDurationMinutes",
                table: "RecurringTrips");

            migrationBuilder.DropColumn(
                name: "OriginAddress",
                table: "RecurringTrips");

            migrationBuilder.DropColumn(
                name: "OriginLatitude",
                table: "RecurringTrips");

            migrationBuilder.DropColumn(
                name: "OriginLongitude",
                table: "RecurringTrips");

            migrationBuilder.DropColumn(
                name: "RoutePolyline",
                table: "RecurringTrips");

            migrationBuilder.DropColumn(
                name: "VehicleId",
                table: "RecurringTrips");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_RecurringTripId",
                table: "Trips",
                column: "RecurringTripId");
        }
    }
}
