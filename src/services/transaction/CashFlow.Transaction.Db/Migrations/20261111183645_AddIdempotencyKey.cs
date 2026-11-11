using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CashFlow.Transaction.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "transaction",
                table: "Transactions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_IdempotencyKey",
                schema: "transaction",
                table: "Transactions",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_IdempotencyKey",
                schema: "transaction",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "transaction",
                table: "Transactions");
        }
    }
}
