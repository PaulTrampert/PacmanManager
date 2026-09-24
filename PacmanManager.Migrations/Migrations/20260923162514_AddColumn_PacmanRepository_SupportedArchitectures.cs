using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PacmanManager.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddColumn_PacmanRepository_SupportedArchitectures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Architecture",
                table: "PacmanRepositories");

            migrationBuilder.AddColumn<string[]>(
                name: "SupportedArchitectures",
                table: "PacmanRepositories",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupportedArchitectures",
                table: "PacmanRepositories");

            migrationBuilder.AddColumn<string>(
                name: "Architecture",
                table: "PacmanRepositories",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");
        }
    }
}
