using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpPersonelLeaveSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkStatusToEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "EducationLevek",
                table: "employees",
                newName: "EducationLevel");

            migrationBuilder.AddColumn<int>(
                name: "SWorkStatus",
                table: "employees",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SWorkStatus",
                table: "employees");

            migrationBuilder.RenameColumn(
                name: "EducationLevel",
                table: "employees",
                newName: "EducationLevek");
        }
    }
}
