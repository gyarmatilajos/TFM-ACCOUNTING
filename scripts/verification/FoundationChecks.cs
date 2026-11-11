using System.IO;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using TFM_ACCOUNTING.Data;
using TFM_ACCOUNTING.Data.Entities;
using TFM_ACCOUNTING.Business.Common;
using TFM_ACCOUNTING.Business.Attachments;
using TFM_ACCOUNTING.Business.Settings;
using TFM_ACCOUNTING.Desktop.ViewModels;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

using var modelDb = new AccountingDbContext(new DbContextOptionsBuilder<AccountingDbContext>()
    .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=TFM_ModelVerification;Trusted_Connection=True").Options);
var entities = modelDb.Model.GetEntityTypes().ToArray();
Check(entities.Length == 12, "Expected twelve entities (seven original and five lookup tables).");
Check(entities.All(x => x.FindProperty("RowVersion") is { IsConcurrencyToken: true }), "Missing concurrency protection.");
Check(entities.SelectMany(x => x.GetForeignKeys()).All(x => x.DeleteBehavior == DeleteBehavior.Restrict), "Unexpected cascade delete.");
Check(entities.SelectMany(x => x.GetProperties()).All(x => !x.IsShadowProperty() && !x.ClrType.IsEnum), "Unexpected shadow/enum property.");
foreach (var type in new[] { typeof(OwnCompany), typeof(Partner) })
{
    var entity = modelDb.Model.FindEntityType(type)!;
    Check(!entity.FindProperty("TaxNumber")!.IsNullable, "Tax number must be required.");
    Check(entity.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "TaxNumber" })), "Missing unique tax number.");
    Check(entity.FindProperty("ShortName")!.IsNullable, "Short name should be optional.");
}
var designModel = modelDb.GetService<IDesignTimeModel>().Model;
foreach (var type in new[] { typeof(PartnerType), typeof(ObligationType), typeof(InvoiceType), typeof(PaymentMethod), typeof(SettlementType) })
{
    var entity = designModel.FindEntityType(type)!;
    Check(entity.GetSeedData().Any(), "Lookup seed data missing.");
    foreach (var field in new[] { "Code", "Name" })
        Check(entity.GetIndexes().Any(x => x.IsUnique && x.Properties.Count == 1 && x.Properties[0].Name == field), "Lookup uniqueness missing.");
}
var invoice = modelDb.Model.FindEntityType(typeof(InvoiceDetails))!;
Check(invoice.GetIndexes().Any(x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "OwnCompanyId", "PartnerId", "InvoiceNumber" })), "Missing invoice uniqueness.");
var sql = modelDb.Database.GenerateCreateScript();
Check(Regex.Matches(sql, "CREATE TABLE").Count == 12, "Expected twelve SQL tables.");
Check(!sql.Contains("[ObligationType] IN") && !sql.Contains("[SettlementType] ="), "Hardcoded enum constraints remain.");
Directory.CreateDirectory("docs");
File.WriteAllText("docs/database-schema.sql", sql);

var company = new OwnCompany { Name = " Minta Kft. ", TaxNumber = " BELSO-000001 " };
modelDb.OwnCompanies.Add(company);
var before = DateTime.UtcNow;
try { await modelDb.SaveChangesAsync(new CancellationToken(true)); }
catch (OperationCanceledException) { }
Check(company.Name == "Minta Kft." && company.TaxNumber == "BELSO-000001", "Whitespace normalization failed.");
Check(company.CreatedAtUtc >= before && company.CreatedAtUtc == company.UpdatedAtUtc, "Audit timestamp failed.");
Check(Response<int>.Success(42) is { IsSuccess: true, Data: 42, ErrorCode: null }, "Invalid success response.");
Check(Response<int>.Failure(ErrorCode.ConcurrencyConflict, "Módosult.").ErrorCode == ErrorCode.ConcurrencyConflict, "Invalid failure response.");
var errors = new List<ValidationError> { new("Name", "Kötelező.") };
var invalid = Response<int>.Invalid(errors);
errors.Clear();
Check(!invalid.IsSuccess && invalid.ValidationErrors.Count == 1, "Validation errors were not copied.");
foreach (var extension in new[] { "pdf", "jpg", "jpeg", "png", "doc", "docx", "xls", "xlsx", "csv" })
    Check(AttachmentFileTypes.TryGetContentType("sample." + extension.ToUpperInvariant(), out _), "Missing attachment type: " + extension);
Check(!AttachmentFileTypes.TryGetContentType("sample.exe", out _), "Unexpected attachment type accepted.");
var noDbFactory = new SettingsServiceFactory(new RejectingFactory());
foreach (var group in SettingsCatalog.Groups)
{
    var service = noDbFactory.Create(group.Key);
    var empty = await service.SaveAsync(new(null, " ", null, true, group.Behaviors.FirstOrDefault()?.Key));
    Check(empty.ErrorCode == ErrorCode.Validation, "Empty name did not fail before accessing database.");
    var oversized = await service.SaveAsync(new(null, "Valid", new string('x', 2001), true, group.Behaviors.FirstOrDefault()?.Key));
    Check(oversized.ErrorCode == ErrorCode.Validation, "Notes length was not checked.");
    if (group.Behaviors.Count > 0)
        Check((await service.SaveAsync(new(null, "Valid", null, true, "invalid"))).ErrorCode == ErrorCode.Validation, "Invalid behavior accepted.");
}
var offlineVm = new SettingsViewModel(_ => null);
await offlineVm.InitializeAsync();
Check(!offlineVm.IsBusy && offlineVm.Message.Contains("Nincs beállított"), "Missing connection was not handled in UI.");
Check(offlineVm.Groups.Count == 6, "Expected six settings sections.");
await OwnCompanyChecks.ValidateWithoutDatabaseAsync(new RejectingFactory());
await PartnerChecks.ValidateWithoutDatabaseAsync(new RejectingFactory());
Exception? renderError = null;
var renderThread = new Thread(() =>
{
    try
    {
        var viewModel = new SettingsViewModel(_ => new PreviewService());
        viewModel.SelectedGroup = SettingsCatalog.Groups.Single(x => x.Key == "obligation-types");
        Check(viewModel.Items.Count == 4, "Settings group switching did not reload rows.");
        viewModel.SelectedItem = viewModel.Items[2];
        Check(!viewModel.CanChooseBehavior && viewModel.BehaviorKey == "period", "Existing behavior was editable in UI.");
        var content = new TFM_ACCOUNTING.Desktop.Views.SettingsView { DataContext = viewModel };
        content.Measure(new Size(1220, 720));
        content.Arrange(new Rect(0, 0, 1220, 720));
        content.UpdateLayout();
        var bitmap = new RenderTargetBitmap(1220, 720, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory("obj/ModelVerification");
        using var file = File.Create("obj/ModelVerification/settings-preview.png");
        png.Save(file);
        OwnCompanyChecks.RenderPreview();
        PartnerChecks.RenderPreview();
        _ = new TFM_ACCOUNTING.Desktop.Views.ObligationsView();
    }
    catch (Exception ex) { renderError = ex; }
});
renderThread.SetApartmentState(ApartmentState.STA);
renderThread.Start();
renderThread.Join();
if (renderError is not null) throw new Exception("WPF rendering failed.", renderError);
Console.WriteLine("PASS: twelve-table SQL model, seeds, foreign keys, uniqueness, rowversions, response, validation, settings navigation, attachment types.");

if (!args.Contains("--integration")) return;

// A fresh, randomly named LocalDB database is used; no configured application database is read or modified.
var testDatabase = "TFM_SettingsVerification_" + Guid.NewGuid().ToString("N");
var options = new DbContextOptionsBuilder<AccountingDbContext>()
    .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={testDatabase};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=20").Options;
var factory = new PooledDbContextFactory<AccountingDbContext>(options);
await using var setup = await factory.CreateDbContextAsync();
try
{
    await setup.Database.MigrateAsync();
    var services = new SettingsServiceFactory(factory);
    foreach (var group in SettingsCatalog.Groups)
    {
        var service = services.Create(group.Key);
        var initial = await service.ListAsync();
        Check(initial.IsSuccess, group.Key + ": list failed: " + initial.Message);
        var behavior = group.Behaviors.FirstOrDefault()?.Key;
        var created = await service.SaveAsync(new(null, " Teszt bejegyzés ", " Megjegyzés ", true, behavior));
        Check(created.IsSuccess && created.Data is { Name: "Teszt bejegyzés", Notes: "Megjegyzés", RowVersion.Length: 8 }, group.Key + ": create failed: " + created.Message);
        var item = created.Data!;
        Check((await service.SaveAsync(new(null, "teszt bejegyzés", null, true, behavior))).ErrorCode == ErrorCode.Duplicate, "Case-insensitive duplicate not rejected.");
        var renamed = await service.SaveAsync(new(item.Id, "Átnevezett bejegyzés", null, true, behavior, item.RowVersion));
        Check(renamed.IsSuccess && renamed.Data!.Code == item.Code && renamed.Data.BehaviorKey == behavior, "Rename changed stable identity/behavior.");
        Check((await service.SaveAsync(new(item.Id, "Elavult módosítás", null, true, behavior, item.RowVersion))).ErrorCode == ErrorCode.ConcurrencyConflict, "Stale update was not rejected.");
        var otherBehavior = group.Behaviors.FirstOrDefault(x => x.Key != behavior)?.Key;
        if (otherBehavior is not null)
            Check((await service.SaveAsync(new(item.Id, "Átnevezett bejegyzés", null, true, otherBehavior, renamed.Data!.RowVersion))).ErrorCode == ErrorCode.Validation, "Existing behavior changed.");
        var deactivated = await service.SetActiveAsync(item.Id, false, renamed.Data!.RowVersion);
        Check(deactivated.IsSuccess && !deactivated.Data!.IsActive, "Deactivate failed.");
        Check(!(await service.ListAsync(includeInactive: false)).Data!.Any(x => x.Id == item.Id), "Inactive value was selectable.");
        Check((await service.ListAsync()).Data!.Any(x => x.Id == item.Id), "Inactive value disappeared from settings.");
        Check((await service.SetActiveAsync(item.Id, true, renamed.Data.RowVersion)).ErrorCode == ErrorCode.ConcurrencyConflict, "Stale activation was accepted.");
        Check((await service.SetActiveAsync(item.Id, true, deactivated.Data!.RowVersion)).IsSuccess, "Reactivate failed.");
        Check((await service.ListAsync("Átnevezett")).Data!.Count == 1, "Search failed.");
        Check((await service.SaveAsync(new(int.MaxValue, "Missing", null, true, behavior, new byte[8]))).ErrorCode == ErrorCode.NotFound, "Missing record not reported.");
    }
    // A newly created custom type must be usable through the FK, without enum-range constraints.
    var newPartnerType = (await services.Create("partner-types").SaveAsync(new(null, "Egyedi partner", null, true))).Data!;
    setup.Partners.Add(new Partner { Name = "Teszt partner", TaxNumber = "TEST-001", PartnerTypeId = newPartnerType.Id });
    await setup.SaveChangesAsync();
    var partner = await setup.Partners.Include(x => x.PartnerType).SingleAsync();
    Check(partner.PartnerType.Name == "Egyedi partner", "Custom lookup FK failed.");
    var linkedRename = await services.Create("partner-types").SaveAsync(new(newPartnerType.Id, "Átnevezett egyedi partner", null, true, null, newPartnerType.RowVersion));
    Check(linkedRename.IsSuccess, "Rename of referenced type failed.");
    await setup.Entry(partner.PartnerType).ReloadAsync();
    Check(partner.PartnerType.Name == "Átnevezett egyedi partner", "Referenced type rename was not visible.");
    await OwnCompanyChecks.IntegrationAsync(factory);
    await PartnerChecks.IntegrationAsync(factory);
    await ObligationChecks.RunAsync(factory);
    Console.WriteLine("PASS: LocalDB integration for all six groups: create, rename, duplicates, stale versions, behavior protection, deactivate/reactivate, search and custom-type FK.");
}
finally
{
    // Only the generated database can be removed, even if this script is edited or fails partway through.
    Check(Regex.IsMatch(testDatabase, "^TFM_SettingsVerification_[a-f0-9]{32}$"), "Unexpected test database name.");
    await setup.Database.EnsureDeletedAsync();
}

sealed class RejectingFactory : IDbContextFactory<AccountingDbContext>
{
    public AccountingDbContext CreateDbContext() => throw new Exception("Validation unexpectedly accessed the database.");
}

sealed class PreviewService : ISettingsService
{
    public Task<Response<IReadOnlyList<SettingItem>>> ListAsync(string? search = null, bool includeInactive = true, CancellationToken cancellationToken = default)
        => Task.FromResult(Response<IReadOnlyList<SettingItem>>.Success(new[]
        {
            new SettingItem(1, "Bejövő számla", null, true, "INCOMING_INVOICE", "invoice", new byte[8]),
            new SettingItem(2, "Egyéb költség", null, true, "OTHER_EXPENSE", "general", new byte[8]),
            new SettingItem(3, "Nettó bér", "Munkavállalónként és hónaponként rögzített nettó bér.", true, "NET_SALARY", "period", new byte[8]),
            new SettingItem(4, "Járulék / adó", null, true, "TAX_OR_CONTRIBUTION", "period", new byte[8])
        }));
    public Task<Response<SettingItem>> SaveAsync(SaveSettingRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<Response<SettingItem>> SetActiveAsync(int id, bool isActive, byte[] rowVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
