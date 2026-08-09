using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LawyerPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationReferenceDataManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_Governorates_NameAr",
                table: "Governorates",
                column: "NameAr",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Governorates_NameEn",
                table: "Governorates",
                column: "NameEn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Cities_GovernorateId_NameAr",
                table: "Cities",
                columns: ["GovernorateId", "NameAr"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Cities_GovernorateId_NameEn",
                table: "Cities",
                columns: ["GovernorateId", "NameEn"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Areas_CityId_NameAr",
                table: "Areas",
                columns: ["CityId", "NameAr"],
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Areas_CityId_NameEn",
                table: "Areas",
                columns: ["CityId", "NameEn"],
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Governorates_NameAr",
                table: "Governorates");

            migrationBuilder.DropIndex(
                name: "UX_Governorates_NameEn",
                table: "Governorates");

            migrationBuilder.DropIndex(
                name: "UX_Cities_GovernorateId_NameAr",
                table: "Cities");

            migrationBuilder.DropIndex(
                name: "UX_Cities_GovernorateId_NameEn",
                table: "Cities");

            migrationBuilder.DropIndex(
                name: "UX_Areas_CityId_NameAr",
                table: "Areas");

            migrationBuilder.DropIndex(
                name: "UX_Areas_CityId_NameEn",
                table: "Areas");
        }
    }
}
