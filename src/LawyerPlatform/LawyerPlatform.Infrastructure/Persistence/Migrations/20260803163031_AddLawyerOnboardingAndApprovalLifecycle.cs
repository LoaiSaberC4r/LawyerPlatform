using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF Core generates inline column arrays for migration indexes.

namespace LawyerPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLawyerOnboardingAndApprovalLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LawyerProfiles_ApprovalStatus",
                table: "LawyerProfiles");

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedOnUtc",
                table: "LawyerProfiles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LawyerApprovalStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LawyerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OldStatus = table.Column<int>(type: "int", nullable: false),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LawyerApprovalStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LawyerApprovalStatusHistory_LawyerProfiles_LawyerProfileId",
                        column: x => x.LawyerProfileId,
                        principalTable: "LawyerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LawyerDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LawyerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RestoredOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LawyerDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LawyerDocuments_LawyerProfiles_LawyerProfileId",
                        column: x => x.LawyerProfileId,
                        principalTable: "LawyerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LawyerOffices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LawyerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GovernorateId = table.Column<int>(type: "int", nullable: false),
                    CityId = table.Column<int>(type: "int", nullable: false),
                    AreaId = table.Column<int>(type: "int", nullable: false),
                    DetailedAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PublicPhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LawyerOffices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LawyerOffices_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LawyerOffices_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LawyerOffices_Governorates_GovernorateId",
                        column: x => x.GovernorateId,
                        principalTable: "Governorates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LawyerOffices_LawyerProfiles_LawyerProfileId",
                        column: x => x.LawyerProfileId,
                        principalTable: "LawyerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LawyerSpecializations",
                columns: table => new
                {
                    LawyerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegalSpecializationId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LawyerSpecializations", x => new { x.LawyerProfileId, x.LegalSpecializationId });
                    table.ForeignKey(
                        name: "FK_LawyerSpecializations_LawyerProfiles_LawyerProfileId",
                        column: x => x.LawyerProfileId,
                        principalTable: "LawyerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LawyerSpecializations_LegalSpecializations_LegalSpecializationId",
                        column: x => x.LegalSpecializationId,
                        principalTable: "LegalSpecializations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LawyerProfiles_ApprovalStatus_SubmittedOnUtc",
                table: "LawyerProfiles",
                columns: new[] { "ApprovalStatus", "SubmittedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LawyerApprovalStatusHistory_LawyerProfileId_ChangedOnUtc",
                table: "LawyerApprovalStatusHistory",
                columns: new[] { "LawyerProfileId", "ChangedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LawyerDocuments_LawyerProfileId_DocumentType_IsDeleted",
                table: "LawyerDocuments",
                columns: new[] { "LawyerProfileId", "DocumentType", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_LawyerOffices_AreaId",
                table: "LawyerOffices",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_LawyerOffices_CityId",
                table: "LawyerOffices",
                column: "CityId");

            migrationBuilder.CreateIndex(
                name: "IX_LawyerOffices_GovernorateId_CityId_AreaId",
                table: "LawyerOffices",
                columns: new[] { "GovernorateId", "CityId", "AreaId" });

            migrationBuilder.CreateIndex(
                name: "UX_LawyerOffices_LawyerProfileId_Primary",
                table: "LawyerOffices",
                column: "LawyerProfileId",
                unique: true,
                filter: "[IsPrimary] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_LawyerSpecializations_LegalSpecializationId_LawyerProfileId",
                table: "LawyerSpecializations",
                columns: new[] { "LegalSpecializationId", "LawyerProfileId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LawyerApprovalStatusHistory");

            migrationBuilder.DropTable(
                name: "LawyerDocuments");

            migrationBuilder.DropTable(
                name: "LawyerOffices");

            migrationBuilder.DropTable(
                name: "LawyerSpecializations");

            migrationBuilder.DropIndex(
                name: "IX_LawyerProfiles_ApprovalStatus_SubmittedOnUtc",
                table: "LawyerProfiles");

            migrationBuilder.DropColumn(
                name: "SubmittedOnUtc",
                table: "LawyerProfiles");

            migrationBuilder.CreateIndex(
                name: "IX_LawyerProfiles_ApprovalStatus",
                table: "LawyerProfiles",
                column: "ApprovalStatus");
        }
    }
}
#pragma warning restore CA1861
