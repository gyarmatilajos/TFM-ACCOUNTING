using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace TFM_ACCOUNTING.Data;

public static class AccountingDatabase
{
    public const string LocalDatabaseName = "TFM_ACCOUNTING_Development";
    public const string LocalConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=TFM_ACCOUNTING_Development;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15";

    public static string ResolveConnectionString() =>
        Environment.GetEnvironmentVariable("TFM_ACCOUNTING_CONNECTION_STRING") is { Length: > 0 } configured
            ? configured : LocalConnectionString;

    public static DbContextOptions<AccountingDbContext> CreateOptions(string connectionString)
    {
        var connection = new SqlConnectionStringBuilder(connectionString);
        var builder = new DbContextOptionsBuilder<AccountingDbContext>();
        if (connection.DataSource.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase))
            builder.UseSqlServer(connectionString);
        else builder.UseAzureSql(connectionString);
        return builder.Options;
    }

    public static IDbContextFactory<AccountingDbContext> CreateFactory(string connectionString) =>
        new PooledDbContextFactory<AccountingDbContext>(CreateOptions(connectionString));
}
