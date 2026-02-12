using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PacmanManager.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class PacmanRepositoryAndPackageIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PacmanPackages_RepositoryId",
                table: "PacmanPackages");

            migrationBuilder.DropColumn(
                name: "FileSystemPath",
                table: "PacmanRepositories");

            migrationBuilder.CreateIndex(
                name: "IX_PacmanRepositories_Name_Architecture",
                table: "PacmanRepositories",
                columns: new[] { "Name", "Architecture" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PacmanPackages_RepositoryId_Name",
                table: "PacmanPackages",
                columns: new[] { "RepositoryId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PacmanRepositories_Name_Architecture",
                table: "PacmanRepositories");

            migrationBuilder.DropIndex(
                name: "IX_PacmanPackages_RepositoryId_Name",
                table: "PacmanPackages");

            migrationBuilder.AddColumn<string>(
                name: "FileSystemPath",
                table: "PacmanRepositories",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PacmanPackages_RepositoryId",
                table: "PacmanPackages",
                column: "RepositoryId");
        }
    }
}
