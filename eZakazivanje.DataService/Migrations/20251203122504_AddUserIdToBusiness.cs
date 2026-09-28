using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace eZakazivanje.DataService.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToBusiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add UserId column as nullable first
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Businesses",
                type: "text",
                nullable: true);

            // Step 2: Populate existing data - only for businesses that have matching users
            // This maintains the 1:1 relationship for existing data
            migrationBuilder.Sql(@"
                UPDATE ""Businesses"" b
                SET ""UserId"" = b.""Id""::text
                WHERE ""UserId"" IS NULL
                AND EXISTS (
                    SELECT 1 FROM ""AspNetUsers"" u 
                    WHERE u.""Id"" = b.""Id""::text
                );
            ");

            // Step 3: Delete orphaned businesses (businesses without matching users)
            // This ensures all remaining businesses have valid UserId values
            migrationBuilder.Sql(@"
                DELETE FROM ""Businesses""
                WHERE ""UserId"" IS NULL;
            ");

            // Step 4: Make UserId NOT NULL after populating and cleaning
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Businesses",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Step 5: Create index for performance
            migrationBuilder.CreateIndex(
                name: "IX_Businesses_UserId",
                table: "Businesses",
                column: "UserId");

            // Step 6: Add foreign key constraint (now safe since all UserIds are valid)
            migrationBuilder.AddForeignKey(
                name: "FK_Businesses_AspNetUsers_UserId",
                table: "Businesses",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Businesses_AspNetUsers_UserId",
                table: "Businesses");

            migrationBuilder.DropIndex(
                name: "IX_Businesses_UserId",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Businesses");
        }
    }
}
