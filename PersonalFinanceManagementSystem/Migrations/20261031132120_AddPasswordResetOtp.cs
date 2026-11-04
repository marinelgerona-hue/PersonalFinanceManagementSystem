using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinanceManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetOtp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PasswordResetOtpAttempts",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime?>(
                name: "PasswordResetOtpExpires",
                table: "Users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetOtpHash",
                table: "Users",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime?>(
                name: "PasswordResetOtpRequestedAt",
                table: "Users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PasswordResetOtpVerified",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PasswordResetOtpAttempts", table: "Users");
            migrationBuilder.DropColumn(name: "PasswordResetOtpExpires", table: "Users");
            migrationBuilder.DropColumn(name: "PasswordResetOtpHash", table: "Users");
            migrationBuilder.DropColumn(name: "PasswordResetOtpRequestedAt", table: "Users");
            migrationBuilder.DropColumn(name: "PasswordResetOtpVerified", table: "Users");
        }
    }
}
