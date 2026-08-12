using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LawyerPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLawyerConsultationSettings : Migration
    {
        private static readonly string[] SettingsDayIndexColumns =
            ["LawyerConsultationSettingsId", "DayOfWeek"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ConsultationPrice",
                table: "ConsultationRequests",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LawyerConsultationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LawyerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsultationPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LawyerConsultationSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LawyerConsultationSettings_LawyerProfiles_LawyerProfileId",
                        column: x => x.LawyerProfileId,
                        principalTable: "LawyerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LawyerAvailabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LawyerConsultationSettingsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LawyerAvailabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LawyerAvailabilities_LawyerConsultationSettings_LawyerConsultationSettingsId",
                        column: x => x.LawyerConsultationSettingsId,
                        principalTable: "LawyerConsultationSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_LawyerAvailabilities_SettingsId_DayOfWeek",
                table: "LawyerAvailabilities",
                columns: SettingsDayIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_LawyerConsultationSettings_LawyerProfileId",
                table: "LawyerConsultationSettings",
                column: "LawyerProfileId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LawyerAvailabilities");

            migrationBuilder.DropTable(
                name: "LawyerConsultationSettings");

            migrationBuilder.DropColumn(
                name: "ConsultationPrice",
                table: "ConsultationRequests");
        }
    }
}
