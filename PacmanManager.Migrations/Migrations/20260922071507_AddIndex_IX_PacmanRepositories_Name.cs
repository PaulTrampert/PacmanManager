using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PacmanManager.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddIndex_IX_PacmanRepositories_Name : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PacmanRepositories_OwnerId_Name_Architecture",
                table: "PacmanRepositories");

            migrationBuilder.CreateIndex(
                name: "IX_PacmanRepositories_Name",
                table: "PacmanRepositories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PacmanRepositories_OwnerId",
                table: "PacmanRepositories",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PacmanRepositories_Name",
                table: "PacmanRepositories");

            migrationBuilder.DropIndex(
                name: "IX_PacmanRepositories_OwnerId",
                table: "PacmanRepositories");

            migrationBuilder.CreateIndex(
                name: "IX_PacmanRepositories_OwnerId_Name_Architecture",
                table: "PacmanRepositories",
                columns: new[] { "OwnerId", "Name", "Architecture" },
                unique: true);
        }
    }
}
