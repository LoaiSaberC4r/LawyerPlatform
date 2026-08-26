using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LawyerPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetAndCredentialVersion : Migration
    {
        private static readonly string[] UserAccountCreatedOnUtcColumns =
            ["UserAccountId", "CreatedOnUtc"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CredentialVersion",
                table: "UserAccounts",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "PasswordResetChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OtpHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FailedAttemptCount = table.Column<int>(type: "int", nullable: false),
                    VerifiedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvalidatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsumedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResetTokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ResetTokenExpiresOnUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetChallenges_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetChallenges_ExpiresOnUtc",
                table: "PasswordResetChallenges",
                column: "ExpiresOnUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetChallenges_UserAccountId_CreatedOnUtc",
                table: "PasswordResetChallenges",
                columns: UserAccountCreatedOnUtcColumns);

            migrationBuilder.CreateIndex(
                name: "UX_PasswordResetChallenges_Current_UserAccountId",
                table: "PasswordResetChallenges",
                column: "UserAccountId",
                unique: true,
                filter: "[InvalidatedOnUtc] IS NULL AND [ConsumedOnUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasswordResetChallenges");

            migrationBuilder.DropColumn(
                name: "CredentialVersion",
                table: "UserAccounts");
        }
    }
}
