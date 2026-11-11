using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TFM_ACCOUNTING.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CostCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostCategories", x => x.Id);
                    table.CheckConstraint("CK_CostCategories_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "InvoiceTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RequiresOriginalInvoice = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceTypes", x => x.Id);
                    table.CheckConstraint("CK_InvoiceTypes_Code", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_InvoiceTypes_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "ObligationTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RequiresPeriod = table.Column<bool>(type: "bit", nullable: false),
                    HasInvoiceDetails = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObligationTypes", x => x.Id);
                    table.CheckConstraint("CK_ObligationTypes_Code", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_ObligationTypes_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "OwnCompanies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TaxNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AddressLine = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnCompanies", x => x.Id);
                    table.CheckConstraint("CK_OwnCompanies_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.CheckConstraint("CK_OwnCompanies_TaxNumber", "LEN(LTRIM(RTRIM([TaxNumber]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "PartnerTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerTypes", x => x.Id);
                    table.CheckConstraint("CK_PartnerTypes_Code", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_PartnerTypes_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "PaymentMethods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentMethods", x => x.Id);
                    table.CheckConstraint("CK_PaymentMethods_Code", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_PaymentMethods_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "SettlementTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsCreditOffset = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementTypes", x => x.Id);
                    table.CheckConstraint("CK_SettlementTypes_Code", "LEN(LTRIM(RTRIM([Code]))) > 0");
                    table.CheckConstraint("CK_SettlementTypes_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Partners",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ShortName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PartnerTypeId = table.Column<int>(type: "int", nullable: false),
                    TaxNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AddressLine = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Partners", x => x.Id);
                    table.CheckConstraint("CK_Partners_Name", "LEN(LTRIM(RTRIM([Name]))) > 0");
                    table.CheckConstraint("CK_Partners_TaxNumber", "LEN(LTRIM(RTRIM([TaxNumber]))) > 0");
                    table.ForeignKey(
                        name: "FK_Partners_PartnerTypes_PartnerTypeId",
                        column: x => x.PartnerTypeId,
                        principalTable: "PartnerTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Obligations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnCompanyId = table.Column<int>(type: "int", nullable: false),
                    PartnerId = table.Column<int>(type: "int", nullable: false),
                    ObligationTypeId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PeriodYear = table.Column<int>(type: "int", nullable: true),
                    PeriodMonth = table.Column<int>(type: "int", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethodId = table.Column<int>(type: "int", nullable: true),
                    OriginalObligationId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Obligations", x => x.Id);
                    table.UniqueConstraint("AK_Obligations_Id_OwnCompanyId_PartnerId", x => new { x.Id, x.OwnCompanyId, x.PartnerId });
                    table.CheckConstraint("CK_Obligations_Amount", "[Amount] <> 0");
                    table.CheckConstraint("CK_Obligations_Description", "LEN(LTRIM(RTRIM([Description]))) > 0");
                    table.CheckConstraint("CK_Obligations_Original", "[OriginalObligationId] IS NULL OR [OriginalObligationId] <> [Id]");
                    table.CheckConstraint("CK_Obligations_Period", "([PeriodYear] IS NULL AND [PeriodMonth] IS NULL) OR ([PeriodYear] IS NOT NULL AND [PeriodMonth] IS NOT NULL AND [PeriodYear] BETWEEN 1 AND 9999 AND [PeriodMonth] BETWEEN 1 AND 12)");
                    table.ForeignKey(
                        name: "FK_Obligations_CostCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "CostCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Obligations_ObligationTypes_ObligationTypeId",
                        column: x => x.ObligationTypeId,
                        principalTable: "ObligationTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Obligations_Obligations_OriginalObligationId",
                        column: x => x.OriginalObligationId,
                        principalTable: "Obligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Obligations_OwnCompanies_OwnCompanyId",
                        column: x => x.OwnCompanyId,
                        principalTable: "OwnCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Obligations_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Obligations_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ObligationId = table.Column<int>(type: "int", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    RelativePath = table.Column<string>(type: "varchar(1000)", unicode: false, maxLength: 1000, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.CheckConstraint("CK_Attachments_ContentType", "LEN(LTRIM(RTRIM([ContentType]))) > 0");
                    table.CheckConstraint("CK_Attachments_Name", "LEN(LTRIM(RTRIM([OriginalFileName]))) > 0");
                    table.CheckConstraint("CK_Attachments_Path", "LEN(LTRIM(RTRIM([RelativePath]))) > 0");
                    table.CheckConstraint("CK_Attachments_Size", "[FileSizeBytes] > 0");
                    table.ForeignKey(
                        name: "FK_Attachments_Obligations_ObligationId",
                        column: x => x.ObligationId,
                        principalTable: "Obligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceDetails",
                columns: table => new
                {
                    ObligationId = table.Column<int>(type: "int", nullable: false),
                    OwnCompanyId = table.Column<int>(type: "int", nullable: false),
                    PartnerId = table.Column<int>(type: "int", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InvoiceTypeId = table.Column<int>(type: "int", nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PerformanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    VatAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceDetails", x => x.ObligationId);
                    table.CheckConstraint("CK_InvoiceDetails_Number", "LEN(LTRIM(RTRIM([InvoiceNumber]))) > 0");
                    table.ForeignKey(
                        name: "FK_InvoiceDetails_InvoiceTypes_InvoiceTypeId",
                        column: x => x.InvoiceTypeId,
                        principalTable: "InvoiceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceDetails_Obligations_ObligationId_OwnCompanyId_PartnerId",
                        columns: x => new { x.ObligationId, x.OwnCompanyId, x.PartnerId },
                        principalTable: "Obligations",
                        principalColumns: new[] { "Id", "OwnCompanyId", "PartnerId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ObligationId = table.Column<int>(type: "int", nullable: false),
                    SettlementTypeId = table.Column<int>(type: "int", nullable: false),
                    SettlementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethodId = table.Column<int>(type: "int", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SettlementGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEntries", x => x.Id);
                    table.CheckConstraint("CK_PaymentEntries_Amount", "[Amount] <> 0");
                    table.CheckConstraint("CK_PaymentEntries_Settlement", "([SettlementGroupId] IS NULL AND [PaymentMethodId] IS NOT NULL) OR ([SettlementGroupId] IS NOT NULL AND [PaymentMethodId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_PaymentEntries_Obligations_ObligationId",
                        column: x => x.ObligationId,
                        principalTable: "Obligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentEntries_PaymentMethods_PaymentMethodId",
                        column: x => x.PaymentMethodId,
                        principalTable: "PaymentMethods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentEntries_SettlementTypes_SettlementTypeId",
                        column: x => x.SettlementTypeId,
                        principalTable: "SettlementTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "InvoiceTypes",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "IsActive", "Name", "Notes", "RequiresOriginalInvoice", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 1, "STANDARD", new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), true, "Normál", null, false, new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "AMENDMENT", new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), true, "Módosító", null, true, new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "CANCELLATION", new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), true, "Érvénytelenítő", null, true, new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "ObligationTypes",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "HasInvoiceDetails", "IsActive", "Name", "Notes", "RequiresPeriod", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 1, "INCOMING_INVOICE", new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), true, true, "Bejövő számla", null, false, new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "OTHER_EXPENSE", new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), false, true, "Egyéb költség", null, false, new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "NET_SALARY", new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), false, true, "Nettó bér", null, true, new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, "TAX_OR_CONTRIBUTION", new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), false, true, "Járulék / adó", null, true, new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "PartnerTypes",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "IsActive", "Name", "Notes", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 1, "BUSINESS", new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), true, "Vállalkozás", null, new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "INDIVIDUAL", new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), true, "Magánszemély", null, new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "AUTHORITY", new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), true, "Hatóság", null, new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "PaymentMethods",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "IsActive", "Name", "Notes", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 1, "BANK_TRANSFER", new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), true, "Átutalás", null, new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "CASH", new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), true, "Készpénz", null, new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, "BANK_CARD", new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), true, "Bankkártya", null, new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, "DIRECT_DEBIT", new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), true, "Csoportos beszedés", null, new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5, "OTHER", new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), true, "Egyéb", null, new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "SettlementTypes",
                columns: new[] { "Id", "Code", "CreatedAtUtc", "IsActive", "IsCreditOffset", "Name", "Notes", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { 1, "MONEY_MOVEMENT", new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), true, false, "Pénzmozgás", null, new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, "CREDIT_OFFSET", new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc), true, true, "Jóváírás beszámítása", null, new DateTime(2026, 11, 10, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_ObligationId",
                table: "Attachments",
                column: "ObligationId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_RelativePath",
                table: "Attachments",
                column: "RelativePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostCategories_Name",
                table: "CostCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_InvoiceTypeId",
                table: "InvoiceDetails",
                column: "InvoiceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_IssueDate",
                table: "InvoiceDetails",
                column: "IssueDate");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_ObligationId_OwnCompanyId_PartnerId",
                table: "InvoiceDetails",
                columns: new[] { "ObligationId", "OwnCompanyId", "PartnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_OwnCompanyId_PartnerId_InvoiceNumber",
                table: "InvoiceDetails",
                columns: new[] { "OwnCompanyId", "PartnerId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceDetails_PerformanceDate",
                table: "InvoiceDetails",
                column: "PerformanceDate");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTypes_Code",
                table: "InvoiceTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTypes_Name",
                table: "InvoiceTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Obligations_CategoryId",
                table: "Obligations",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Obligations_ObligationTypeId",
                table: "Obligations",
                column: "ObligationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Obligations_OriginalObligationId",
                table: "Obligations",
                column: "OriginalObligationId");

            migrationBuilder.CreateIndex(
                name: "IX_Obligations_OwnCompanyId_DueDate",
                table: "Obligations",
                columns: new[] { "OwnCompanyId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Obligations_PartnerId",
                table: "Obligations",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Obligations_PaymentMethodId",
                table: "Obligations",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_Obligations_PeriodYear_PeriodMonth_ObligationTypeId",
                table: "Obligations",
                columns: new[] { "PeriodYear", "PeriodMonth", "ObligationTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ObligationTypes_Code",
                table: "ObligationTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ObligationTypes_Name",
                table: "ObligationTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OwnCompanies_Name",
                table: "OwnCompanies",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_OwnCompanies_TaxNumber",
                table: "OwnCompanies",
                column: "TaxNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Partners_Name",
                table: "Partners",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Partners_PartnerTypeId",
                table: "Partners",
                column: "PartnerTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Partners_TaxNumber",
                table: "Partners",
                column: "TaxNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerTypes_Code",
                table: "PartnerTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerTypes_Name",
                table: "PartnerTypes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEntries_ObligationId",
                table: "PaymentEntries",
                column: "ObligationId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEntries_PaymentMethodId",
                table: "PaymentEntries",
                column: "PaymentMethodId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEntries_SettlementDate",
                table: "PaymentEntries",
                column: "SettlementDate");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEntries_SettlementGroupId_ObligationId",
                table: "PaymentEntries",
                columns: new[] { "SettlementGroupId", "ObligationId" },
                unique: true,
                filter: "[SettlementGroupId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEntries_SettlementTypeId",
                table: "PaymentEntries",
                column: "SettlementTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_Code",
                table: "PaymentMethods",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentMethods_Name",
                table: "PaymentMethods",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettlementTypes_Code",
                table: "SettlementTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SettlementTypes_Name",
                table: "SettlementTypes",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "InvoiceDetails");

            migrationBuilder.DropTable(
                name: "PaymentEntries");

            migrationBuilder.DropTable(
                name: "InvoiceTypes");

            migrationBuilder.DropTable(
                name: "Obligations");

            migrationBuilder.DropTable(
                name: "SettlementTypes");

            migrationBuilder.DropTable(
                name: "CostCategories");

            migrationBuilder.DropTable(
                name: "ObligationTypes");

            migrationBuilder.DropTable(
                name: "OwnCompanies");

            migrationBuilder.DropTable(
                name: "Partners");

            migrationBuilder.DropTable(
                name: "PaymentMethods");

            migrationBuilder.DropTable(
                name: "PartnerTypes");
        }
    }
}
