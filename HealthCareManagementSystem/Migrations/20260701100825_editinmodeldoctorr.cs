using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthCareManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class editinmodeldoctorr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageName",
                table: "Doctors",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageName",
                table: "Doctors");
        }
    }
}
