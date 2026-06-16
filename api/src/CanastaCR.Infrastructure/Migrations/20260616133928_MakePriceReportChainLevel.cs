using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanastaCR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakePriceReportChainLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PriceReports_Stores_StoreId",
                table: "PriceReports");

            migrationBuilder.AlterColumn<Guid>(
                name: "StoreId",
                table: "PriceReports",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "Chain",
                table: "PriceReports",
                type: "integer",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceReports_Stores_StoreId",
                table: "PriceReports",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PriceReports_Stores_StoreId",
                table: "PriceReports");

            migrationBuilder.DropColumn(
                name: "Chain",
                table: "PriceReports");

            migrationBuilder.AlterColumn<Guid>(
                name: "StoreId",
                table: "PriceReports",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceReports_Stores_StoreId",
                table: "PriceReports",
                column: "StoreId",
                principalTable: "Stores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
