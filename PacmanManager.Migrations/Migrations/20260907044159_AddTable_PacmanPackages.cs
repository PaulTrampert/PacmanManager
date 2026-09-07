using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PacmanManager.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddTable_PacmanPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PacmanPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublisherId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Version = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Base = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Architecture = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Packager = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CompressedSize = table.Column<long>(type: "bigint", nullable: false),
                    InstalledSize = table.Column<long>(type: "bigint", nullable: false),
                    BuildDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Sha256Sum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Md5Sum = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Licenses = table.Column<string[]>(type: "text[]", nullable: false),
                    Groups = table.Column<string[]>(type: "text[]", nullable: false),
                    Provides = table.Column<string[]>(type: "text[]", nullable: false),
                    Replaces = table.Column<string[]>(type: "text[]", nullable: false),
                    Depends = table.Column<string[]>(type: "text[]", nullable: false),
                    OptDepends = table.Column<string[]>(type: "text[]", nullable: false),
                    MakeDepends = table.Column<string[]>(type: "text[]", nullable: false),
                    CheckDepends = table.Column<string[]>(type: "text[]", nullable: false),
                    Conflicts = table.Column<string[]>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PacmanPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PacmanPackages_PacmanRepositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalTable: "PacmanRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PacmanPackages_Users_PublisherId",
                        column: x => x.PublisherId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PacmanPackages_PublisherId",
                table: "PacmanPackages",
                column: "PublisherId");

            migrationBuilder.CreateIndex(
                name: "IX_PacmanPackages_RepositoryId_Name",
                table: "PacmanPackages",
                columns: new[] { "RepositoryId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PacmanPackages");
        }
    }
}
