using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PacmanManager.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddIndex_IX_UserMappings_ExternalAuthority_ExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserMappings_ExternalAuthority_ExternalId",
                table: "UserMappings",
                columns: new[] { "ExternalAuthority", "ExternalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserMappings_ExternalAuthority_ExternalId",
                table: "UserMappings");
        }
    }
}
