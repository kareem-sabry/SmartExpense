using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartExpense.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_UserId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_RecurringTransactions_UserId",
                table: "RecurringTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Budgets_UserId",
                table: "Budgets");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId_CategoryId",
                table: "Transactions",
                columns: new[] { "UserId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId_TransactionDate",
                table: "Transactions",
                columns: new[] { "UserId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId_TransactionType",
                table: "Transactions",
                columns: new[] { "UserId", "TransactionType" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_UserId_IsActive",
                table: "RecurringTransactions",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_UserId_Month_Year",
                table: "Budgets",
                columns: new[] { "UserId", "Month", "Year" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_UserId_CategoryId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_UserId_TransactionDate",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_UserId_TransactionType",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_RecurringTransactions_UserId_IsActive",
                table: "RecurringTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Budgets_UserId_Month_Year",
                table: "Budgets");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId",
                table: "Transactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTransactions_UserId",
                table: "RecurringTransactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_UserId",
                table: "Budgets",
                column: "UserId");
        }
    }
}
