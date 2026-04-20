using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartExpense.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addPreviousToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviousRefreshTokenHash",
                table: "AspNetUsers",
                type: "nvarchar(88)",
                maxLength: 88,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviousRefreshTokenHash",
                table: "AspNetUsers");
        }
    }
}
