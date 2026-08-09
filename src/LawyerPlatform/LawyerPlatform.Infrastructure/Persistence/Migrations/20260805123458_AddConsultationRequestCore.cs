using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LawyerPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConsultationRequestCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsultationRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ClientProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GuestFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GuestPhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    GuestEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    LawyerProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegalSpecializationId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    PreferredAppointmentOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultationRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsultationRequests_ClientProfiles_ClientProfileId",
                        column: x => x.ClientProfileId,
                        principalTable: "ClientProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsultationRequests_LawyerProfiles_LawyerProfileId",
                        column: x => x.LawyerProfileId,
                        principalTable: "LawyerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsultationRequests_LegalSpecializations_LegalSpecializationId",
                        column: x => x.LegalSpecializationId,
                        principalTable: "LegalSpecializations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsultationRequestStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsultationRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OldStatus = table.Column<int>(type: "int", nullable: false),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsultationRequestStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsultationRequestStatusHistory_ConsultationRequests_ConsultationRequestId",
                        column: x => x.ConsultationRequestId,
                        principalTable: "ConsultationRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsultationRequestStatusHistory_UserAccounts_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationRequests_ClientProfileId_CreatedOnUtc",
                table: "ConsultationRequests",
                columns: ["ClientProfileId", "CreatedOnUtc"]);

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationRequests_LawyerProfileId_Status_CreatedOnUtc",
                table: "ConsultationRequests",
                columns: ["LawyerProfileId", "Status", "CreatedOnUtc"]);

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationRequests_LegalSpecializationId",
                table: "ConsultationRequests",
                column: "LegalSpecializationId");

            migrationBuilder.CreateIndex(
                name: "UX_ConsultationRequests_ReferenceNumber",
                table: "ConsultationRequests",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationRequestStatusHistory_ChangedByUserId",
                table: "ConsultationRequestStatusHistory",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationRequestStatusHistory_ConsultationRequestId_ChangedOnUtc",
                table: "ConsultationRequestStatusHistory",
                columns: ["ConsultationRequestId", "ChangedOnUtc"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsultationRequestStatusHistory");

            migrationBuilder.DropTable(
                name: "ConsultationRequests");
        }
    }
}
