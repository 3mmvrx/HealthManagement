using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthCareManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class BookingAndSecurityImprovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Doctors_Clinics_ClinicId",
                table: "Doctors");

            migrationBuilder.DropForeignKey(
                name: "FK_MedicalFiles_Doctors_DoctorId",
                table: "MedicalFiles");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "Age",
                table: "Patients");

            // SQL Server cannot change a "date" column to "int" in place (the scaffolded AlterColumn
            // fails with "Operand type clash"), so the weekday is copied into a new column first.
            // 7 January 1900 was a Sunday, which is DayOfWeek 0.
            migrationBuilder.AddColumn<int>(
                name: "DayOfWeekNumber",
                table: "DoctorSchedules",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE [DoctorSchedules] SET [DayOfWeekNumber] = DATEDIFF(DAY, '19000107', [DayOfWeek]) % 7");

            migrationBuilder.DropColumn(
                name: "DayOfWeek",
                table: "DoctorSchedules");

            migrationBuilder.RenameColumn(
                name: "DayOfWeekNumber",
                table: "DoctorSchedules",
                newName: "DayOfWeek");

            migrationBuilder.AddColumn<int>(
                name: "SlotMinutes",
                table: "DoctorSchedules",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "Appointments",
                type: "int",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Appointments",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "Appointments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedAttempts",
                table: "ApplicationUserOtps",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId_AppointmentDate",
                table: "Appointments",
                columns: new[] { "DoctorId", "AppointmentDate" },
                unique: true,
                filter: "[Status] <> 2");

            migrationBuilder.AddForeignKey(
                name: "FK_Doctors_Clinics_ClinicId",
                table: "Doctors",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MedicalFiles_Doctors_DoctorId",
                table: "MedicalFiles",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Doctors_Clinics_ClinicId",
                table: "Doctors");

            migrationBuilder.DropForeignKey(
                name: "FK_MedicalFiles_Doctors_DoctorId",
                table: "MedicalFiles");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId_AppointmentDate",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "SlotMinutes",
                table: "DoctorSchedules");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "FailedAttempts",
                table: "ApplicationUserOtps");

            migrationBuilder.AddColumn<int>(
                name: "Age",
                table: "Patients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // The weekday cannot be turned back into a date, so the column is recreated (same reason as in Up).
            migrationBuilder.DropColumn(
                name: "DayOfWeek",
                table: "DoctorSchedules");

            migrationBuilder.AddColumn<DateOnly>(
                name: "DayOfWeek",
                table: "DoctorSchedules",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AlterColumn<bool>(
                name: "Status",
                table: "Appointments",
                type: "bit",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId",
                table: "Appointments",
                column: "DoctorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Doctors_Clinics_ClinicId",
                table: "Doctors",
                column: "ClinicId",
                principalTable: "Clinics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_MedicalFiles_Doctors_DoctorId",
                table: "MedicalFiles",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
