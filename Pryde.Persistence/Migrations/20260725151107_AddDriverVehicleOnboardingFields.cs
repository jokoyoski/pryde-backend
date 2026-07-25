using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pryde.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverVehicleOnboardingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VehicleImages_VehicleId",
                table: "VehicleImages");

            migrationBuilder.AddColumn<string>(
                name: "AdditionalDetails",
                table: "Vehicles",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LuggageCapacity",
                table: "Vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OnboardingStatus",
                table: "Vehicles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PassengerSeatCount",
                table: "Vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RegistrationType",
                table: "Vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VehicleOwnerName",
                table: "Vehicles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WalkAroundVideoUrl",
                table: "Vehicles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImageType",
                table: "VehicleImages",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                """UPDATE "Vehicles" SET "OnboardingStatus" = 2 WHERE "IsActive" = TRUE;""");

            migrationBuilder.CreateTable(
                name: "VehicleAmenities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmenityType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleAmenities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleAmenities_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleImages_VehicleId_ImageType",
                table: "VehicleImages",
                columns: new[] { "VehicleId", "ImageType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleAmenities_VehicleId_AmenityType",
                table: "VehicleAmenities",
                columns: new[] { "VehicleId", "AmenityType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleAmenities");

            migrationBuilder.DropIndex(
                name: "IX_VehicleImages_VehicleId_ImageType",
                table: "VehicleImages");

            migrationBuilder.DropColumn(
                name: "AdditionalDetails",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "LuggageCapacity",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "OnboardingStatus",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "PassengerSeatCount",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "RegistrationType",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "VehicleOwnerName",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "WalkAroundVideoUrl",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ImageType",
                table: "VehicleImages");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleImages_VehicleId",
                table: "VehicleImages",
                column: "VehicleId");
        }
    }
}
