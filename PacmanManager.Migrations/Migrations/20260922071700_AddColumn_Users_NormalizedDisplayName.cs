using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PacmanManager.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AddColumn_Users_NormalizedDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedDisplayName",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            // Postgres's lower() stands in for string.ToLowerInvariant() here. The two agree on ASCII
            // but can differ on some non-ASCII characters depending on the database's collation; a
            // row that differs is corrected the next time its display name is written. See
            // docs/user-management.md, "Why a display name has a normalized copy".
            migrationBuilder.Sql("""UPDATE "Users" SET "NormalizedDisplayName" = lower("DisplayName");""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NormalizedDisplayName",
                table: "Users");
        }
    }
}
