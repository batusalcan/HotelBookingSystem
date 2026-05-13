using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace HotelService.Data.Migrations.Catalog
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hotels",
                columns: table => new
                {
                    HotelId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Destination = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", nullable: false),
                    BaseRating = table.Column<decimal>(type: "numeric(3,1)", nullable: true),
                    TotalReviews = table.Column<int>(type: "integer", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hotels", x => x.HotelId);
                });

            migrationBuilder.CreateTable(
                name: "RoomTypes",
                columns: table => new
                {
                    RoomTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    HotelId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MaxGuests = table.Column<int>(type: "integer", nullable: false),
                    BasePricePerNight = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomTypes", x => x.RoomTypeId);
                    table.ForeignKey(
                        name: "FK_RoomTypes_Hotels_HotelId",
                        column: x => x.HotelId,
                        principalTable: "Hotels",
                        principalColumn: "HotelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryBlocks",
                columns: table => new
                {
                    InventoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalCount = table.Column<int>(type: "integer", nullable: false),
                    AvailableCount = table.Column<int>(type: "integer", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryBlocks", x => x.InventoryId);
                    table.ForeignKey(
                        name: "FK_InventoryBlocks_RoomTypes_RoomTypeId",
                        column: x => x.RoomTypeId,
                        principalTable: "RoomTypes",
                        principalColumn: "RoomTypeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Hotels",
                columns: new[] { "HotelId", "BaseRating", "Destination", "ImageUrl", "IsActive", "Latitude", "Longitude", "Name", "TotalReviews" },
                values: new object[,]
                {
                    { new Guid("11111111-0000-0000-0000-000000000001"), 9.2m, "Istanbul", null, true, 41.0423m, 29.0082m, "Swissôtel The Bosphorus Istanbul", 1284 },
                    { new Guid("11111111-0000-0000-0000-000000000002"), 8.8m, "Istanbul", null, true, 41.0603m, 28.9844m, "Hilton Istanbul Bomonti", 876 },
                    { new Guid("11111111-0000-0000-0000-000000000003"), 9.0m, "Izmir", null, true, 38.4192m, 27.1287m, "Swissôtel Büyük Efes Izmir", 643 },
                    { new Guid("11111111-0000-0000-0000-000000000004"), 9.6m, "Bodrum", null, true, 37.0344m, 27.4305m, "Hyde Bodrum - Yetişkin Oteli", 163 },
                    { new Guid("11111111-0000-0000-0000-000000000005"), 9.4m, "Bodrum", null, true, 37.1041m, 27.2866m, "MGallery The Bodrum Hotel Yalıkavak", 302 },
                    { new Guid("11111111-0000-0000-0000-000000000006"), 9.1m, "Antalya", null, true, 36.8531m, 30.7512m, "Regnum Carya Golf & Spa Resort", 529 },
                    { new Guid("11111111-0000-0000-0000-000000000007"), 9.3m, "Antalya", null, true, 36.8642m, 31.0667m, "Rixos Premium Belek", 1102 }
                });

            migrationBuilder.InsertData(
                table: "RoomTypes",
                columns: new[] { "RoomTypeId", "BasePricePerNight", "HotelId", "MaxGuests", "TypeName" },
                values: new object[,]
                {
                    { new Guid("22222222-0000-0000-0000-000000000001"), 4200m, new Guid("11111111-0000-0000-0000-000000000001"), 2, "Standard" },
                    { new Guid("22222222-0000-0000-0000-000000000002"), 7800m, new Guid("11111111-0000-0000-0000-000000000001"), 4, "Family" },
                    { new Guid("22222222-0000-0000-0000-000000000003"), 3500m, new Guid("11111111-0000-0000-0000-000000000002"), 2, "Standard" },
                    { new Guid("22222222-0000-0000-0000-000000000004"), 6200m, new Guid("11111111-0000-0000-0000-000000000002"), 4, "Family" },
                    { new Guid("22222222-0000-0000-0000-000000000005"), 3100m, new Guid("11111111-0000-0000-0000-000000000003"), 2, "Standard" },
                    { new Guid("22222222-0000-0000-0000-000000000006"), 5800m, new Guid("11111111-0000-0000-0000-000000000003"), 4, "Family" },
                    { new Guid("22222222-0000-0000-0000-000000000007"), 10948m, new Guid("11111111-0000-0000-0000-000000000004"), 2, "Standard" },
                    { new Guid("22222222-0000-0000-0000-000000000008"), 18500m, new Guid("11111111-0000-0000-0000-000000000004"), 4, "Family" },
                    { new Guid("22222222-0000-0000-0000-000000000009"), 9458m, new Guid("11111111-0000-0000-0000-000000000005"), 2, "Standard" },
                    { new Guid("22222222-0000-0000-0000-000000000010"), 15900m, new Guid("11111111-0000-0000-0000-000000000005"), 4, "Family" },
                    { new Guid("22222222-0000-0000-0000-000000000011"), 6500m, new Guid("11111111-0000-0000-0000-000000000006"), 2, "Standard" },
                    { new Guid("22222222-0000-0000-0000-000000000012"), 11200m, new Guid("11111111-0000-0000-0000-000000000006"), 4, "Family" },
                    { new Guid("22222222-0000-0000-0000-000000000013"), 8200m, new Guid("11111111-0000-0000-0000-000000000007"), 2, "Standard" },
                    { new Guid("22222222-0000-0000-0000-000000000014"), 14600m, new Guid("11111111-0000-0000-0000-000000000007"), 4, "Family" }
                });

            migrationBuilder.InsertData(
                table: "InventoryBlocks",
                columns: new[] { "InventoryId", "AvailableCount", "EndDate", "IsAvailable", "RoomTypeId", "StartDate", "TotalCount" },
                values: new object[,]
                {
                    { new Guid("33333333-0000-0000-0000-000000000001"), 10, new DateOnly(2026, 6, 9), true, new Guid("22222222-0000-0000-0000-000000000001"), new DateOnly(2026, 5, 10), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000002"), 1, new DateOnly(2026, 7, 9), true, new Guid("22222222-0000-0000-0000-000000000001"), new DateOnly(2026, 6, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000003"), 8, new DateOnly(2026, 8, 8), true, new Guid("22222222-0000-0000-0000-000000000001"), new DateOnly(2026, 7, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000004"), 10, new DateOnly(2026, 6, 9), true, new Guid("22222222-0000-0000-0000-000000000002"), new DateOnly(2026, 5, 10), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000005"), 1, new DateOnly(2026, 7, 9), true, new Guid("22222222-0000-0000-0000-000000000002"), new DateOnly(2026, 6, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000006"), 8, new DateOnly(2026, 8, 8), true, new Guid("22222222-0000-0000-0000-000000000002"), new DateOnly(2026, 7, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000007"), 10, new DateOnly(2026, 6, 9), true, new Guid("22222222-0000-0000-0000-000000000003"), new DateOnly(2026, 5, 10), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000008"), 1, new DateOnly(2026, 7, 9), true, new Guid("22222222-0000-0000-0000-000000000003"), new DateOnly(2026, 6, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000009"), 8, new DateOnly(2026, 8, 8), true, new Guid("22222222-0000-0000-0000-000000000003"), new DateOnly(2026, 7, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000010"), 10, new DateOnly(2026, 6, 9), true, new Guid("22222222-0000-0000-0000-000000000004"), new DateOnly(2026, 5, 10), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000011"), 1, new DateOnly(2026, 7, 9), true, new Guid("22222222-0000-0000-0000-000000000004"), new DateOnly(2026, 6, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000012"), 8, new DateOnly(2026, 8, 8), true, new Guid("22222222-0000-0000-0000-000000000004"), new DateOnly(2026, 7, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000013"), 10, new DateOnly(2026, 6, 9), true, new Guid("22222222-0000-0000-0000-000000000005"), new DateOnly(2026, 5, 10), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000014"), 1, new DateOnly(2026, 7, 9), true, new Guid("22222222-0000-0000-0000-000000000005"), new DateOnly(2026, 6, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000015"), 8, new DateOnly(2026, 8, 8), true, new Guid("22222222-0000-0000-0000-000000000005"), new DateOnly(2026, 7, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000016"), 10, new DateOnly(2026, 6, 9), true, new Guid("22222222-0000-0000-0000-000000000006"), new DateOnly(2026, 5, 10), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000017"), 1, new DateOnly(2026, 7, 9), true, new Guid("22222222-0000-0000-0000-000000000006"), new DateOnly(2026, 6, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000018"), 8, new DateOnly(2026, 8, 8), true, new Guid("22222222-0000-0000-0000-000000000006"), new DateOnly(2026, 7, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000019"), 10, new DateOnly(2026, 6, 9), true, new Guid("22222222-0000-0000-0000-000000000007"), new DateOnly(2026, 5, 10), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000020"), 1, new DateOnly(2026, 7, 9), true, new Guid("22222222-0000-0000-0000-000000000007"), new DateOnly(2026, 6, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000021"), 8, new DateOnly(2026, 8, 8), true, new Guid("22222222-0000-0000-0000-000000000007"), new DateOnly(2026, 7, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000022"), 10, new DateOnly(2026, 6, 9), true, new Guid("22222222-0000-0000-0000-000000000008"), new DateOnly(2026, 5, 10), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000023"), 1, new DateOnly(2026, 7, 9), true, new Guid("22222222-0000-0000-0000-000000000008"), new DateOnly(2026, 6, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000024"), 8, new DateOnly(2026, 8, 8), true, new Guid("22222222-0000-0000-0000-000000000008"), new DateOnly(2026, 7, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000025"), 10, new DateOnly(2026, 6, 9), true, new Guid("22222222-0000-0000-0000-000000000009"), new DateOnly(2026, 5, 10), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000026"), 1, new DateOnly(2026, 7, 9), true, new Guid("22222222-0000-0000-0000-000000000009"), new DateOnly(2026, 6, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000027"), 8, new DateOnly(2026, 8, 8), true, new Guid("22222222-0000-0000-0000-000000000009"), new DateOnly(2026, 7, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000028"), 10, new DateOnly(2026, 6, 9), true, new Guid("22222222-0000-0000-0000-000000000010"), new DateOnly(2026, 5, 10), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000029"), 1, new DateOnly(2026, 7, 9), true, new Guid("22222222-0000-0000-0000-000000000010"), new DateOnly(2026, 6, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000030"), 8, new DateOnly(2026, 8, 8), true, new Guid("22222222-0000-0000-0000-000000000010"), new DateOnly(2026, 7, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000031"), 10, new DateOnly(2026, 6, 9), true, new Guid("22222222-0000-0000-0000-000000000011"), new DateOnly(2026, 5, 10), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000032"), 1, new DateOnly(2026, 7, 9), true, new Guid("22222222-0000-0000-0000-000000000011"), new DateOnly(2026, 6, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000033"), 8, new DateOnly(2026, 8, 8), true, new Guid("22222222-0000-0000-0000-000000000011"), new DateOnly(2026, 7, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000034"), 10, new DateOnly(2026, 6, 9), true, new Guid("22222222-0000-0000-0000-000000000012"), new DateOnly(2026, 5, 10), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000035"), 1, new DateOnly(2026, 7, 9), true, new Guid("22222222-0000-0000-0000-000000000012"), new DateOnly(2026, 6, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000036"), 8, new DateOnly(2026, 8, 8), true, new Guid("22222222-0000-0000-0000-000000000012"), new DateOnly(2026, 7, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000037"), 10, new DateOnly(2026, 6, 9), true, new Guid("22222222-0000-0000-0000-000000000013"), new DateOnly(2026, 5, 10), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000038"), 1, new DateOnly(2026, 7, 9), true, new Guid("22222222-0000-0000-0000-000000000013"), new DateOnly(2026, 6, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000039"), 8, new DateOnly(2026, 8, 8), true, new Guid("22222222-0000-0000-0000-000000000013"), new DateOnly(2026, 7, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000040"), 10, new DateOnly(2026, 6, 9), true, new Guid("22222222-0000-0000-0000-000000000014"), new DateOnly(2026, 5, 10), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000041"), 1, new DateOnly(2026, 7, 9), true, new Guid("22222222-0000-0000-0000-000000000014"), new DateOnly(2026, 6, 9), 10 },
                    { new Guid("33333333-0000-0000-0000-000000000042"), 8, new DateOnly(2026, 8, 8), true, new Guid("22222222-0000-0000-0000-000000000014"), new DateOnly(2026, 7, 9), 10 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hotels_Destination",
                table: "Hotels",
                column: "Destination");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBlocks_RoomTypeId",
                table: "InventoryBlocks",
                column: "RoomTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryBlocks_StartDate_EndDate",
                table: "InventoryBlocks",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RoomTypes_HotelId",
                table: "RoomTypes",
                column: "HotelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryBlocks");

            migrationBuilder.DropTable(
                name: "RoomTypes");

            migrationBuilder.DropTable(
                name: "Hotels");
        }
    }
}
