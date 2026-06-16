using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CanastaCR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWalmartStores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Stores",
                columns: new[] { "Id", "Address", "Chain", "City", "Lat", "Lng", "Name" },
                values: new object[,]
                {
                    { new Guid("11111111-0000-0000-0000-000000000011"), "La Uruca", 5, "San José", 9.9611999999999998, -84.108900000000006, "Walmart San José" },
                    { new Guid("11111111-0000-0000-0000-000000000012"), "Alajuela Centro", 5, "Alajuela", 10.0175, -84.214200000000005, "Walmart Alajuela" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Stores",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "Stores",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000012"));
        }
    }
}
