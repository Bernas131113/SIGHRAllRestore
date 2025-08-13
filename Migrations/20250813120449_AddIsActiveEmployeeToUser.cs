using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGHR.Migrations
{
    /// <inheritdoc />
    public partial class AddIsActiveEmployeeToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActiveEmployee",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActiveEmployee",
                table: "AspNetUsers");
        }
    }
}
