using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalFinanceManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityLogActorRoleAndTarget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "ActivityLogs",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Backfill the role of entries written before the column existed,
            // so the historical trail is readable too.
            migrationBuilder.Sql(
                "UPDATE `ActivityLogs` AS a JOIN `Users` AS u ON u.Id = a.UserId " +
                "SET a.Role = u.Role WHERE a.Role = '';");

            migrationBuilder.AddColumn<int>(
                name: "TargetUserId",
                table: "ActivityLogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_TargetUserId",
                table: "ActivityLogs",
                column: "TargetUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityLogs_Users_TargetUserId",
                table: "ActivityLogs",
                column: "TargetUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityLogs_Users_TargetUserId",
                table: "ActivityLogs");

            migrationBuilder.DropIndex(
                name: "IX_ActivityLogs_TargetUserId",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "ActivityLogs");

            migrationBuilder.DropColumn(
                name: "TargetUserId",
                table: "ActivityLogs");
        }
    }
}
