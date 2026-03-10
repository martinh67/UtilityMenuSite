using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UtilityMenuSite.Migrations
{
    /// <inheritdoc />
    public partial class MakeUserIdentityIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the old non-nullable unique index
            migrationBuilder.DropIndex(
                name: "UQ_Users_IdentityId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_IdentityId",
                table: "Users");

            // Make IdentityId nullable
            migrationBuilder.AlterColumn<string>(
                name: "IdentityId",
                table: "Users",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            // Re-create the unique index with a WHERE filter so that multiple NULL values
            // are permitted (SQL Server treats NULLs as distinct in filtered unique indexes).
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX [UQ_Users_IdentityId] ON [Users] ([IdentityId]) WHERE [IdentityId] IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IdentityId",
                table: "Users",
                column: "IdentityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UQ_Users_IdentityId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_IdentityId",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "IdentityId",
                table: "Users",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Users_IdentityId",
                table: "Users",
                column: "IdentityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_IdentityId",
                table: "Users",
                column: "IdentityId");
        }
    }
}
