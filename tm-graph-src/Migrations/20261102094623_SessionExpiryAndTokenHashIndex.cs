using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace tm_graph_src.Migrations
{
    /// <inheritdoc />
    public partial class SessionExpiryAndTokenHashIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "Sessions",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TokenHash",
                table: "Sessions",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_TokenHash",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "Sessions");
        }
    }
}
