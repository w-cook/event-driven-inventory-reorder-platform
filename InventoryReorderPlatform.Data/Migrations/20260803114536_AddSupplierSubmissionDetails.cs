using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InventoryReorderPlatform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierSubmissionDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SupplierAcceptedAtUtc",
                table: "ReorderEvents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierOrderId",
                table: "ReorderEvents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierOrderStatus",
                table: "ReorderEvents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplierRejectionReason",
                table: "ReorderEvents",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupplierAcceptedAtUtc",
                table: "ReorderEvents");

            migrationBuilder.DropColumn(
                name: "SupplierOrderId",
                table: "ReorderEvents");

            migrationBuilder.DropColumn(
                name: "SupplierOrderStatus",
                table: "ReorderEvents");

            migrationBuilder.DropColumn(
                name: "SupplierRejectionReason",
                table: "ReorderEvents");
        }
    }
}
