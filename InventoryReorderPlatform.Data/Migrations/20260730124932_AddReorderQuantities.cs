using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryReorderPlatform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReorderQuantities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RequestedQuantity",
                table: "ReorderEvents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReorderQuantity",
                table: "InventoryItems",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestedQuantity",
                table: "ReorderEvents");

            migrationBuilder.DropColumn(
                name: "ReorderQuantity",
                table: "InventoryItems");
        }
    }
}
