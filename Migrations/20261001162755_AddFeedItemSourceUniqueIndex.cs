using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalLifeApi.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedItemSourceUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FeedItems_Source_SourceId",
                table: "FeedItems",
                columns: new[] { "Source", "SourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FeedItems_Source_SourceId",
                table: "FeedItems");
        }
    }
}
