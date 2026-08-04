using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pryde.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Trips"
                SET "CompletedAt" = COALESCE(
                    "AutoCompletedAt",
                    "UpdatedAt",
                    "CreatedAt")
                WHERE "Status" = 4
                  AND "CompletedAt" IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "TripRatings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    RaterId = table.Column<Guid>(type: "uuid", nullable: false),
                    RatedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripRatings", x => x.Id);
                    table.CheckConstraint("CK_TripRatings_Value", "\"Value\" >= 1 AND \"Value\" <= 5");
                    table.ForeignKey(
                        name: "FK_TripRatings_TripBookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "TripBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TripRatings_Users_RatedUserId",
                        column: x => x.RatedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TripRatings_Users_RaterId",
                        column: x => x.RaterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TripRatings_BookingId_RaterId",
                table: "TripRatings",
                columns: new[] { "BookingId", "RaterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripRatings_RatedUserId",
                table: "TripRatings",
                column: "RatedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TripRatings_RaterId",
                table: "TripRatings",
                column: "RaterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TripRatings");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Trips");
        }
    }
}
