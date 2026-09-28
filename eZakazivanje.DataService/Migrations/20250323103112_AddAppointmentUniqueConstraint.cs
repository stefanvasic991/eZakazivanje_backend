using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eZakazivanje.DataService.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create unique index to prevent double bookings
            // This ensures no overlapping appointments for the same employee on the same date
            migrationBuilder.CreateIndex(
                name: "IX_Appointments_EmployeeId_AppointmentDate_StartTime_EndTime",
                table: "Appointments",
                columns: new[] { "EmployeeId", "AppointmentDate", "StartTime", "EndTime" },
                unique: true,
                filter: "\"IsCancelled\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Appointments_EmployeeId_AppointmentDate_StartTime_EndTime",
                table: "Appointments");
        }
    }
} 