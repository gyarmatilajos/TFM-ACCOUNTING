using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TFM_ACCOUNTING.Data.Entities;

namespace TFM_ACCOUNTING.Data.Configurations;

internal static class ConfigurationDefaults
{
    public static void Common<T>(EntityTypeBuilder<T> b) where T : class
    {
        b.HasKey("Id");
        b.Property<string>("Notes").HasMaxLength(2000);
        b.Property<DateTime>("CreatedAtUtc").HasColumnType("datetime2");
        b.Property<DateTime>("UpdatedAtUtc").HasColumnType("datetime2");
        b.Property<byte[]>("RowVersion").IsRowVersion();
    }

    public static void Party<T>(EntityTypeBuilder<T> b) where T : class
    {
        Common(b);
        b.Property<string>("Name").HasMaxLength(200).IsRequired();
        b.Property<string>("ShortName").HasMaxLength(50);
        b.Property<string>("TaxNumber").HasMaxLength(20).IsRequired();
        b.Property<string>("PostalCode").HasMaxLength(10);
        b.Property<string>("City").HasMaxLength(100);
        b.Property<string>("AddressLine").HasMaxLength(200);
        b.HasIndex("TaxNumber").IsUnique();
        b.HasIndex("Name");
    }
}

public sealed class OwnCompanyConfiguration : IEntityTypeConfiguration<OwnCompany>
{
    public void Configure(EntityTypeBuilder<OwnCompany> b)
    {
        ConfigurationDefaults.Party(b);
        b.ToTable("OwnCompanies", t =>
        {
            t.HasCheckConstraint("CK_OwnCompanies_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
            t.HasCheckConstraint("CK_OwnCompanies_TaxNumber", "LEN(LTRIM(RTRIM([TaxNumber]))) > 0");
        });
    }
}

public sealed class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    public void Configure(EntityTypeBuilder<Partner> b)
    {
        ConfigurationDefaults.Party(b);
        b.HasOne(x => x.PartnerType).WithMany().HasForeignKey(x => x.PartnerTypeId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable("Partners", t =>
        {
            t.HasCheckConstraint("CK_Partners_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
            t.HasCheckConstraint("CK_Partners_TaxNumber", "LEN(LTRIM(RTRIM([TaxNumber]))) > 0");
        });
    }
}

public sealed class CostCategoryConfiguration : IEntityTypeConfiguration<CostCategory>
{
    public void Configure(EntityTypeBuilder<CostCategory> b)
    {
        ConfigurationDefaults.Common(b);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.ToTable("CostCategories", t => t.HasCheckConstraint("CK_CostCategories_Name", "LEN(LTRIM(RTRIM([Name]))) > 0"));
    }
}

public sealed class ObligationConfiguration : IEntityTypeConfiguration<Obligation>
{
    public void Configure(EntityTypeBuilder<Obligation> b)
    {
        ConfigurationDefaults.Common(b);
        b.Property(x => x.Description).HasMaxLength(200);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.HasOne(x => x.OwnCompany).WithMany().HasForeignKey(x => x.OwnCompanyId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.ObligationType).WithMany().HasForeignKey(x => x.ObligationTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.OriginalObligation).WithMany().HasForeignKey(x => x.OriginalObligationId).OnDelete(DeleteBehavior.Restrict);
        b.HasAlternateKey(x => new { x.Id, x.OwnCompanyId, x.PartnerId });
        b.HasIndex(x => new { x.OwnCompanyId, x.DueDate });
        b.HasIndex(x => new { x.PeriodYear, x.PeriodMonth, x.ObligationTypeId });
        b.ToTable("Obligations", t =>
        {
            t.HasCheckConstraint("CK_Obligations_Amount", "[Amount] <> 0");
            t.HasCheckConstraint("CK_Obligations_Description", "LEN(LTRIM(RTRIM([Description]))) > 0");
            t.HasCheckConstraint("CK_Obligations_Period", "([PeriodYear] IS NULL AND [PeriodMonth] IS NULL) OR ([PeriodYear] IS NOT NULL AND [PeriodMonth] IS NOT NULL AND [PeriodYear] BETWEEN 1 AND 9999 AND [PeriodMonth] BETWEEN 1 AND 12)");
            t.HasCheckConstraint("CK_Obligations_Original", "[OriginalObligationId] IS NULL OR [OriginalObligationId] <> [Id]");
        });
    }
}

public sealed class InvoiceDetailsConfiguration : IEntityTypeConfiguration<InvoiceDetails>
{
    public void Configure(EntityTypeBuilder<InvoiceDetails> b)
    {
        b.HasKey(x => x.ObligationId);
        b.Property(x => x.ObligationId).ValueGeneratedNever();
        b.Property(x => x.InvoiceNumber).HasMaxLength(100).IsRequired();
        b.Property(x => x.NetAmount).HasPrecision(18, 2);
        b.Property(x => x.VatAmount).HasPrecision(18, 2);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.InvoiceType).WithMany().HasForeignKey(x => x.InvoiceTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Obligation).WithOne(x => x.InvoiceDetails)
            .HasForeignKey<InvoiceDetails>(x => new { x.ObligationId, x.OwnCompanyId, x.PartnerId })
            .HasPrincipalKey<Obligation>(x => new { x.Id, x.OwnCompanyId, x.PartnerId })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.OwnCompanyId, x.PartnerId, x.InvoiceNumber }).IsUnique();
        b.HasIndex(x => x.IssueDate);
        b.HasIndex(x => x.PerformanceDate);
        b.ToTable("InvoiceDetails", t =>
        {
            t.HasCheckConstraint("CK_InvoiceDetails_Number", "LEN(LTRIM(RTRIM([InvoiceNumber]))) > 0");
        });
    }
}

public sealed class PaymentEntryConfiguration : IEntityTypeConfiguration<PaymentEntry>
{
    public void Configure(EntityTypeBuilder<PaymentEntry> b)
    {
        ConfigurationDefaults.Common(b);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.Reference).HasMaxLength(200);
        b.HasOne(x => x.Obligation).WithMany(x => x.PaymentEntries).HasForeignKey(x => x.ObligationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SettlementType).WithMany().HasForeignKey(x => x.SettlementTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.SettlementDate);
        b.HasIndex(x => new { x.SettlementGroupId, x.ObligationId }).IsUnique().HasFilter("[SettlementGroupId] IS NOT NULL");
        b.ToTable("PaymentEntries", t =>
        {
            t.HasCheckConstraint("CK_PaymentEntries_Amount", "[Amount] <> 0");
            t.HasCheckConstraint("CK_PaymentEntries_Settlement", "([SettlementGroupId] IS NULL AND [PaymentMethodId] IS NOT NULL) OR ([SettlementGroupId] IS NOT NULL AND [PaymentMethodId] IS NULL)");
        });
    }
}

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> b)
    {
        ConfigurationDefaults.Common(b);
        b.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        // Generated paths contain ASCII IDs only: varchar keeps the unique index below SQL Server's key size limit.
        b.Property(x => x.RelativePath).HasMaxLength(1000).IsUnicode(false).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.RelativePath).IsUnique();
        b.HasOne(x => x.Obligation).WithMany(x => x.Attachments).HasForeignKey(x => x.ObligationId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable("Attachments", t =>
        {
            t.HasCheckConstraint("CK_Attachments_Size", "[FileSizeBytes] > 0");
            t.HasCheckConstraint("CK_Attachments_Name", "LEN(LTRIM(RTRIM([OriginalFileName]))) > 0");
            t.HasCheckConstraint("CK_Attachments_Path", "LEN(LTRIM(RTRIM([RelativePath]))) > 0");
            t.HasCheckConstraint("CK_Attachments_ContentType", "LEN(LTRIM(RTRIM([ContentType]))) > 0");
        });
    }
}
