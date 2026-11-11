using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeiterfassung.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPauseEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PauseEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Start = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    End = table.Column<TimeOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PauseEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PauseEntries_Date",
                table: "PauseEntries",
                column: "Date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PauseEntries");
        }
    }
}
