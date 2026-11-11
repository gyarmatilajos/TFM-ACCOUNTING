using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TFM_ACCOUNTING.Data.Entities;

namespace TFM_ACCOUNTING.Data.Configurations;

internal static class LookupConfiguration
{
    public static void Configure<T>(EntityTypeBuilder<T> b, string table) where T : class, ILookupEntry
    {
        ConfigurationDefaults.Common(b);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Code).HasMaxLength(50).IsUnicode(false).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.HasIndex(x => x.Code).IsUnique();
        b.ToTable(table, t =>
        {
            t.HasCheckConstraint($"CK_{table}_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
            t.HasCheckConstraint($"CK_{table}_Code", "LEN(LTRIM(RTRIM([Code]))) > 0");
        });
    }
}

public sealed class PartnerTypeConfiguration : IEntityTypeConfiguration<PartnerType>
{
    public void Configure(EntityTypeBuilder<PartnerType> b) => LookupConfiguration.Configure(b, "PartnerTypes");
}

public sealed class ObligationTypeConfiguration : IEntityTypeConfiguration<ObligationType>
{
    public void Configure(EntityTypeBuilder<ObligationType> b) => LookupConfiguration.Configure(b, "ObligationTypes");
}

public sealed class InvoiceTypeConfiguration : IEntityTypeConfiguration<InvoiceType>
{
    public void Configure(EntityTypeBuilder<InvoiceType> b) => LookupConfiguration.Configure(b, "InvoiceTypes");
}

public sealed class PaymentMethodConfiguration : IEntityTypeConfiguration<PaymentMethod>
{
    public void Configure(EntityTypeBuilder<PaymentMethod> b) => LookupConfiguration.Configure(b, "PaymentMethods");
}

public sealed class SettlementTypeConfiguration : IEntityTypeConfiguration<SettlementType>
{
    public void Configure(EntityTypeBuilder<SettlementType> b) => LookupConfiguration.Configure(b, "SettlementTypes");
}

internal static class LookupSeedData
{
    // Anonymous seed records omit database-generated rowversions. Names are initial values only;
    // editing them in Settings does not cause them to be reset at application startup.
    public static void Configure(ModelBuilder model)
    {
        var timestamp = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);
        object Basic(int id, string code, string name) => new
        {
            Id = id, Code = code, Name = name, IsActive = true,
            CreatedAtUtc = timestamp, UpdatedAtUtc = timestamp
        };
        object Obligation(int id, string code, string name, bool period, bool invoice) => new
        {
            Id = id, Code = code, Name = name, IsActive = true,
            RequiresPeriod = period, HasInvoiceDetails = invoice,
            CreatedAtUtc = timestamp, UpdatedAtUtc = timestamp
        };
        object Invoice(int id, string code, string name, bool original) => new
        {
            Id = id, Code = code, Name = name, IsActive = true, RequiresOriginalInvoice = original,
            CreatedAtUtc = timestamp, UpdatedAtUtc = timestamp
        };
        object Settlement(int id, string code, string name, bool offset) => new
        {
            Id = id, Code = code, Name = name, IsActive = true, IsCreditOffset = offset,
            CreatedAtUtc = timestamp, UpdatedAtUtc = timestamp
        };
        model.Entity<PartnerType>().HasData(
            Basic(1, "BUSINESS", "Vállalkozás"), Basic(2, "INDIVIDUAL", "Magánszemély"), Basic(3, "AUTHORITY", "Hatóság"));
        model.Entity<ObligationType>().HasData(
            Obligation(1, "INCOMING_INVOICE", "Bejövő számla", false, true),
            Obligation(2, "OTHER_EXPENSE", "Egyéb költség", false, false),
            Obligation(3, "NET_SALARY", "Nettó bér", true, false),
            Obligation(4, "TAX_OR_CONTRIBUTION", "Járulék / adó", true, false));
        model.Entity<InvoiceType>().HasData(
            Invoice(1, "STANDARD", "Normál", false), Invoice(2, "AMENDMENT", "Módosító", true),
            Invoice(3, "CANCELLATION", "Érvénytelenítő", true));
        model.Entity<PaymentMethod>().HasData(
            Basic(1, "BANK_TRANSFER", "Átutalás"), Basic(2, "CASH", "Készpénz"),
            Basic(3, "BANK_CARD", "Bankkártya"), Basic(4, "DIRECT_DEBIT", "Csoportos beszedés"), Basic(5, "OTHER", "Egyéb"));
        model.Entity<SettlementType>().HasData(
            Settlement(1, "MONEY_MOVEMENT", "Pénzmozgás", false), Settlement(2, "CREDIT_OFFSET", "Jóváírás beszámítása", true));
    }
}
