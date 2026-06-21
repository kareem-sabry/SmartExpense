using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartExpense.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueRecurringDateIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_RecurringTransactionId",
                table: "Transactions");

            migrationBuilder.CreateIndex(
                name: "UIX_Transactions_RecurringId_Date",
                table: "Transactions",
                columns: new[] { "RecurringTransactionId", "TransactionDate" },
                unique: true,
                filter: "RecurringTransactionId IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UIX_Transactions_RecurringId_Date",
                table: "Transactions");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_RecurringTransactionId",
                table: "Transactions",
                column: "RecurringTransactionId");
        }
    }
}
