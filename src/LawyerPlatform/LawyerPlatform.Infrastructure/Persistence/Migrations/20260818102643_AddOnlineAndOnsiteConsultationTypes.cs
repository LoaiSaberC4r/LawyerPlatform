using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LawyerPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOnlineAndOnsiteConsultationTypes : Migration
    {
        private static readonly string[] AvailabilityIndexColumns =
            ["LawyerConsultationSettingsId", "ConsultationType", "DayOfWeek"];
        private static readonly string[] LegacyAvailabilityIndexColumns =
            ["LawyerConsultationSettingsId", "DayOfWeek"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_LawyerAvailabilities_SettingsId_DayOfWeek",
                table: "LawyerAvailabilities");

            migrationBuilder.AddColumn<int>(
                name: "ConsultationType",
                table: "LawyerAvailabilities",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ConsultationType",
                table: "ConsultationRequests",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "UX_LawyerAvailabilities_SettingsId_Type_DayOfWeek",
                table: "LawyerAvailabilities",
                columns: AvailabilityIndexColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DELETE FROM [LawyerAvailabilities] WHERE [ConsultationType] = 2;");

            migrationBuilder.DropIndex(
                name: "UX_LawyerAvailabilities_SettingsId_Type_DayOfWeek",
                table: "LawyerAvailabilities");

            migrationBuilder.DropColumn(
                name: "ConsultationType",
                table: "LawyerAvailabilities");

            migrationBuilder.DropColumn(
                name: "ConsultationType",
                table: "ConsultationRequests");

            migrationBuilder.CreateIndex(
                name: "UX_LawyerAvailabilities_SettingsId_DayOfWeek",
                table: "LawyerAvailabilities",
                columns: LegacyAvailabilityIndexColumns,
                unique: true);
        }
    }
}
