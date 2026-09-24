using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PacmanManager.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddIndex_IX_PacmanPackages_RepositoryId_Name_Architecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PacmanPackages_RepositoryId_Name",
                table: "PacmanPackages");

            migrationBuilder.CreateIndex(
                name: "IX_PacmanPackages_RepositoryId_Name_Architecture",
                table: "PacmanPackages",
                columns: new[] { "RepositoryId", "Name", "Architecture" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PacmanPackages_RepositoryId_Name_Architecture",
                table: "PacmanPackages");

            migrationBuilder.CreateIndex(
                name: "IX_PacmanPackages_RepositoryId_Name",
                table: "PacmanPackages",
                columns: new[] { "RepositoryId", "Name" },
                unique: true);
        }
    }
}
