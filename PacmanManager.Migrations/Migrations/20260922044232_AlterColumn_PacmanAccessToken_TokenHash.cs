using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PacmanManager.Migrations.Migrations
{
    /// <inheritdoc />
    public partial class AlterColumn_PacmanAccessToken_TokenHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "PacmanAccessTokens",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(44)",
                oldMaxLength: 44);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "PacmanAccessTokens",
                type: "character varying(44)",
                maxLength: 44,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);
        }
    }
}
