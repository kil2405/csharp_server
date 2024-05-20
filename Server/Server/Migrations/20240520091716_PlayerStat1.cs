using Microsoft.EntityFrameworkCore.Migrations;

namespace Server.Migrations
{
    public partial class PlayerStat1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "level",
                table: "Player",
                newName: "Level");

            migrationBuilder.AlterColumn<float>(
                name: "Speed",
                table: "Player",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Level",
                table: "Player",
                newName: "level");

            migrationBuilder.AlterColumn<int>(
                name: "Speed",
                table: "Player",
                type: "int",
                nullable: false,
                oldClrType: typeof(float));
        }
    }
}
