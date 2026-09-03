using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LawyerPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceDataSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateSequence<int>(
                name: "SEQ_Areas_Id",
                schema: "dbo",
                startValue: 1000000000L);

            migrationBuilder.CreateSequence<int>(
                name: "SEQ_Cities_Id",
                schema: "dbo",
                startValue: 100000L);

            migrationBuilder.CreateSequence<int>(
                name: "SEQ_Governorates_Id",
                schema: "dbo",
                startValue: 10000L);

            migrationBuilder.CreateSequence<int>(
                name: "SEQ_LegalSpecializations_Id",
                schema: "dbo",
                startValue: 10000L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "SEQ_Areas_Id",
                schema: "dbo");

            migrationBuilder.DropSequence(
                name: "SEQ_Cities_Id",
                schema: "dbo");

            migrationBuilder.DropSequence(
                name: "SEQ_Governorates_Id",
                schema: "dbo");

            migrationBuilder.DropSequence(
                name: "SEQ_LegalSpecializations_Id",
                schema: "dbo");
        }
    }
}
