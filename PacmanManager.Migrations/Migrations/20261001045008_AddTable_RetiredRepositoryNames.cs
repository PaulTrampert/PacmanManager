using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PacmanManager.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTable_RetiredRepositoryNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RetiredRepositoryNames",
                columns: table => new
                {
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    RetiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RedirectUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastRequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastRequesterId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetiredRepositoryNames", x => x.Name);
                    table.ForeignKey(
                        name: "FK_RetiredRepositoryNames_PacmanRepositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "PacmanRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RetiredRepositoryNames_Users_LastRequesterId",
                        column: x => x.LastRequesterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RetiredRepositoryNames_LastRequesterId",
                table: "RetiredRepositoryNames",
                column: "LastRequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_RetiredRepositoryNames_RepositoryId",
                table: "RetiredRepositoryNames",
                column: "RepositoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetiredRepositoryNames");
        }
    }
}
