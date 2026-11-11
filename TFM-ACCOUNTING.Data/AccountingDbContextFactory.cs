using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TFM_ACCOUNTING.Data;

public sealed class AccountingDbContextFactory : IDesignTimeDbContextFactory<AccountingDbContext>
{
    public AccountingDbContext CreateDbContext(string[] args)
    {
        return new AccountingDbContext(AccountingDatabase.CreateOptions(AccountingDatabase.ResolveConnectionString()));
    }
}
