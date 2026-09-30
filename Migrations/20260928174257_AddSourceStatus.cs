using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalLifeApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SourceStatuses",
                columns: table => new
                {
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    TimeStampUpdated = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceStatuses", x => x.Source);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourceStatuses");
        }
    }
}
