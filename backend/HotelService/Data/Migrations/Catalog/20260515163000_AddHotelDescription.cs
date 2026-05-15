#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace HotelService.Data.Migrations.Catalog
{
    public partial class AddHotelDescription : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Hotels",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Hotels");
        }
    }
}
