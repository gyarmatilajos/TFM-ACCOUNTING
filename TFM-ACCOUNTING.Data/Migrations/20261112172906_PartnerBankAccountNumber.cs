using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TFM_ACCOUNTING.Data.Migrations
{
    /// <inheritdoc />
    public partial class PartnerBankAccountNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankAccountNumber",
                table: "Partners",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankAccountNumber",
                table: "Partners");
        }
    }
}
