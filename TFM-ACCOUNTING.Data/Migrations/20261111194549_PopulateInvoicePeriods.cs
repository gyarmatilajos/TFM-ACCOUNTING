using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TFM_ACCOUNTING.Data.Migrations
{
    /// <inheritdoc />
    public partial class PopulateInvoicePeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE obligation
                SET PeriodYear = YEAR(invoice.PerformanceDate),
                    PeriodMonth = MONTH(invoice.PerformanceDate)
                FROM Obligations AS obligation
                INNER JOIN InvoiceDetails AS invoice ON invoice.ObligationId = obligation.Id
                WHERE obligation.PeriodYear IS NULL OR obligation.PeriodMonth IS NULL
                   OR obligation.PeriodYear <> YEAR(invoice.PerformanceDate)
                   OR obligation.PeriodMonth <> MONTH(invoice.PerformanceDate);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Derived periods remain valid when reverting the migration.
        }
    }
}
