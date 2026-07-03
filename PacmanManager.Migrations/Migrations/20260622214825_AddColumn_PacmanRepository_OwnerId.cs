using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PacmanManager.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddColumn_PacmanRepository_OwnerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PacmanRepositories_Name_Architecture",
                table: "PacmanRepositories");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "PacmanRepositories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_PacmanRepositories_OwnerId_Name_Architecture",
                table: "PacmanRepositories",
                columns: new[] { "OwnerId", "Name", "Architecture" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PacmanRepositories_Users_OwnerId",
                table: "PacmanRepositories",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PacmanRepositories_Users_OwnerId",
                table: "PacmanRepositories");

            migrationBuilder.DropIndex(
                name: "IX_PacmanRepositories_OwnerId_Name_Architecture",
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
