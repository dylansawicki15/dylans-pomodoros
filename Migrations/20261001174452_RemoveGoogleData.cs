using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalLifeApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGoogleData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // we remove google data because i lost interest in doing anything from google calendar just like habitica.             
            migrationBuilder.Sql("DELETE FROM FeedItems WHERE Source = 'google'");

            migrationBuilder.Sql("DELETE FROM SourceStatuses WHERE Source = 'google'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // we aint gonna do this cos its basically like how are wemeant to restore possibly hundreds of rows and hardcode that bruh
        }
    }
}
