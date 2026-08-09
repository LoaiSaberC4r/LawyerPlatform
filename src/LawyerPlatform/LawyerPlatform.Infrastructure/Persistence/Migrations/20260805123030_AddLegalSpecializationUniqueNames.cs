using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LawyerPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalSpecializationUniqueNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_LegalSpecializations_NameAr",
                table: "LegalSpecializations",
                column: "NameAr",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_LegalSpecializations_NameEn",
                table: "LegalSpecializations",
                column: "NameEn",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_LegalSpecializations_NameAr",
                table: "LegalSpecializations");

            migrationBuilder.DropIndex(
                name: "UX_LegalSpecializations_NameEn",
                table: "LegalSpecializations");
        }
    }
}
