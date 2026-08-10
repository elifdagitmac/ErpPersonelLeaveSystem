using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpPersonelLeaveSystem.Migrations
{
    public partial class RenameSWorkStatusToWorkStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Sütunu adlandırmak yerine SIFIRDAN EKLİYORUZ
            migrationBuilder.AddColumn<int>(
                name: "WorkStatus",
                table: "Employees",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkStatus",
                table: "Employees");
        }
    }
}
