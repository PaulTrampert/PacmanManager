using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PacmanManager.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerIdAndIsPublic_PacmanRepository : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PacmanRepositories_Name_Architecture",
                table: "PacmanRepositories");

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                table: "PacmanRepositories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "PacmanRepositories",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PacmanRepositories_OwnerId_Name_Architecture",
                table: "PacmanRepositories",
                columns: new[] { "OwnerId", "Name", "Architecture" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PacmanRepositories_OwnerId_Name_Architecture",
                table: "PacmanRepositories");

            migrationBuilder.DropColumn(
                name: "IsPublic",
                table: "PacmanRepositories");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "PacmanRepositories");

            migrationBuilder.CreateIndex(
                name: "IX_PacmanRepositories_Name_Architecture",
                table: "PacmanRepositories",
                columns: new[] { "Name", "Architecture" },
                unique: true);
        }
    }
}
