using Microsoft.EntityFrameworkCore;
using TFM_ACCOUNTING.Data.Entities;

namespace TFM_ACCOUNTING.Data;

public sealed class AccountingDbContext(DbContextOptions<AccountingDbContext> options) : DbContext(options)
{
    public DbSet<OwnCompany> OwnCompanies => Set<OwnCompany>();
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<Obligation> Obligations => Set<Obligation>();
    public DbSet<InvoiceDetails> InvoiceDetails => Set<InvoiceDetails>();
    public DbSet<CostCategory> CostCategories => Set<CostCategory>();
    public DbSet<PaymentEntry> PaymentEntries => Set<PaymentEntry>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<PartnerType> PartnerTypes => Set<PartnerType>();
    public DbSet<ObligationType> ObligationTypes => Set<ObligationType>();
    public DbSet<InvoiceType> InvoiceTypes => Set<InvoiceType>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<SettlementType> SettlementTypes => Set<SettlementType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Latin1_General_100_CI_AS");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccountingDbContext).Assembly);
        Configurations.LookupSeedData.Configure(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PrepareChanges()
    {
        ChangeTracker.DetectChanges();
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            foreach (var property in entry.Properties.Where(p => p.Metadata.ClrType == typeof(string)))
                if (property.CurrentValue is string value)
                    property.CurrentValue = value.Trim();

            if (entry.Entity is IAuditable auditable)
            {
                if (entry.State == EntityState.Added) auditable.CreatedAtUtc = now;
                else entry.Property(nameof(IAuditable.CreatedAtUtc)).IsModified = false;
                auditable.UpdatedAtUtc = now;
            }
        }
    }
}
