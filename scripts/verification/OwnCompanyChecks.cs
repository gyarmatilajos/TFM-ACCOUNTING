using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using TFM_ACCOUNTING.Business;
using TFM_ACCOUNTING.Business.Common;
using TFM_ACCOUNTING.Business.OwnCompanies;
using TFM_ACCOUNTING.Data;
using TFM_ACCOUNTING.Data.Entities;
using TFM_ACCOUNTING.Desktop.ViewModels;
using TFM_ACCOUNTING.Desktop.Views;

static class OwnCompanyChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static SaveOwnCompanyRequest Request(string name, string? tax = "TEST-001") => new(null, name, null, tax, null, null, null, null);

    public static async Task ValidateWithoutDatabaseAsync(IDbContextFactory<AccountingDbContext> factory)
    {
        var service = new OwnCompanyService(factory);
        Check((await service.SaveAsync(Request(" "))).ErrorCode == ErrorCode.Validation, "Empty company name accepted.");
        Check((await service.SaveAsync(Request("Valid", " "))).ErrorCode == ErrorCode.Validation, "Missing tax number accepted.");
        Check((await service.SaveAsync(Request(new string('x', 201)))).ErrorCode == ErrorCode.Validation, "Company name limit not checked.");
        Check((await service.SaveAsync(Request("Valid") with { Notes = new string('x', 2001) })).ErrorCode == ErrorCode.Validation, "Company notes limit not checked.");
        Check((await service.SaveAsync(Request("Valid") with { Id = 1 })).ErrorCode == ErrorCode.Validation, "Update without version accepted.");
        Check((await service.ListAsync(new(Page: 0))).ErrorCode == ErrorCode.Validation, "Invalid company page accepted.");
        Check((await service.ListAsync(new(SortBy: "Unknown"))).ErrorCode == ErrorCode.Validation, "Invalid company sorting accepted.");
        Check((await service.ListAsync(new(Page: int.MaxValue))).ErrorCode == ErrorCode.Validation, "Overflowing company page accepted.");
        var cancelled = false;
        try { await service.GetAsync(1, new CancellationToken(true)); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "Cancellation did not propagate.");
        Check(AccountingDatabase.LocalConnectionString.Contains("TFM_ACCOUNTING_Development"), "Missing persistent development connection.");
        Check(new ApplicationServices(AccountingDatabase.LocalConnectionString).DatabaseDescription.Contains("Helyi tesztadatbázis"), "Local environment not displayed.");
    }

    public static async Task IntegrationAsync(IDbContextFactory<AccountingDbContext> factory)
    {
        var service = new OwnCompanyService(factory);
        var created = await service.SaveAsync(Request(" Teszt Cég Kft. ") with
        {
            ShortName = " ", PostalCode = " 1111 ", City = " Budapest ", AddressLine = " Teszt utca 1. ", Notes = new string('x', 2000)
        });
        Check(created.IsSuccess, "Company create failed: " + created.Message);
        var original = created.Data!;
        Check(original is { Name: "Teszt Cég Kft.", ShortName: null, City: "Budapest", RowVersion.Length: 8 }, "Company normalization/version failed.");
        Check((await service.SaveAsync(Request("Másik cég", "test-001"))).ErrorCode == ErrorCode.Duplicate, "Case-insensitive company duplicate accepted.");
        var current = (await service.GetAsync(original.Id)).Data!;
        Check(current.Notes?.Length == 2000, "Company notes boundary lost.");
        var editRequest = Request("Átnevezett Cég Kft.") with { Id = original.Id, ShortName = "AC", RowVersion = current.RowVersion };
        var updated = await service.SaveAsync(editRequest);
        Check(updated.IsSuccess, "Company update failed.");
        Check((await service.SaveAsync(editRequest with { Name = "Elavult mentés" })).ErrorCode == ErrorCode.ConcurrencyConflict, "Stale company edit accepted.");
        Check((await service.SetActiveAsync(original.Id, false, original.RowVersion)).ErrorCode == ErrorCode.ConcurrencyConflict, "Stale company activation accepted.");
        var inactive = await service.SetActiveAsync(original.Id, false, updated.Data!.RowVersion);
        Check(inactive.IsSuccess && !inactive.Data!.IsActive, "Company inactivation failed.");
        Check((await service.ListAsync(new(IsActive: true))).Data!.TotalCount == 0, "Inactive company was active.");
        Check((await service.ListAsync(new(IsActive: false))).Data!.TotalCount == 1, "Inactive company not listed.");
        var active = await service.SetActiveAsync(original.Id, true, inactive.Data!.RowVersion);
        Check(active.IsSuccess && active.Data!.IsActive, "Company reactivation failed.");
        foreach (var search in new[] { "Átnevezett", "AC", "TEST-001" })
            Check((await service.ListAsync(new(Search: search))).Data!.TotalCount == 1, "Company search failed: " + search);
        var internalA = await service.SaveAsync(Request("Belső azonosítós A", null) with { GenerateInternalIdentifier = true });
        var internalB = await service.SaveAsync(Request("Belső azonosítós B", null) with { GenerateInternalIdentifier = true });
        Check(internalA.IsSuccess && internalB.IsSuccess && internalA.Data!.TaxNumber != internalB.Data!.TaxNumber && internalA.Data.TaxNumber.Length <= 20, "Internal identifiers failed.");
        var retained = await service.SaveAsync(Request("Belső azonosítós A javítva", null) with
        {
            Id = internalA.Data!.Id, RowVersion = internalA.Data.RowVersion, GenerateInternalIdentifier = true
        });
        Check(retained.IsSuccess && retained.Data!.TaxNumber == internalA.Data.TaxNumber, "Internal identifier changed on edit.");
        var race = await Task.WhenAll(service.SaveAsync(Request("Verseny A", "RACE-001")), service.SaveAsync(Request("Verseny B", "RACE-001")));
        Check(race.Count(x => x.IsSuccess) == 1 && race.Count(x => x.ErrorCode == ErrorCode.Duplicate) == 1, "Concurrent company duplicate protection failed.");
        var ascending = (await service.ListAsync(new(PageSize: 2))).Data!;
        var next = (await service.ListAsync(new(Page: 2, PageSize: 2))).Data!;
        Check(ascending.Items.Count == 2 && next.Items.Count == 2 && !ascending.Items.Select(x => x.Id).Intersect(next.Items.Select(x => x.Id)).Any(), "Company paging failed.");
        var sorted = (await service.ListAsync(new(SortBy: "TaxNumber", Descending: true))).Data!;
        Check(sorted.Items.First().TaxNumber == "TEST-001", "Company sorting failed.");
        Check((await service.GetAsync(int.MaxValue)).ErrorCode == ErrorCode.NotFound, "Missing company not reported.");
        Check((await service.SetActiveAsync(int.MaxValue, true, new byte[8])).ErrorCode == ErrorCode.NotFound, "Missing company activation not reported.");
        // Referenced companies can be inactivated without losing their obligations.
        await using var db = await factory.CreateDbContextAsync();
        db.Obligations.Add(new Obligation
        {
            OwnCompanyId = original.Id, PartnerId = await db.Partners.Select(x => x.Id).FirstAsync(),
            ObligationTypeId = 2, Description = "Megőrzendő kötelezettség", DueDate = new DateOnly(2026, 11, 20), Amount = 1000m
        });
        await db.SaveChangesAsync();
        Check((await service.SetActiveAsync(original.Id, false, active.Data!.RowVersion)).IsSuccess, "Referenced company could not be inactivated.");
        Check(await db.Obligations.AnyAsync(x => x.OwnCompanyId == original.Id), "Inactivation lost an obligation.");
        Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Initial migration is not up to date.");
        Console.WriteLine("PASS: own companies: validation, normalization, optional fields, identifier generation, searching, sorting, paging, concurrency, duplicate race, activation, preserved references and migrations.");
    }

    public static void RenderPreview()
    {
        var preview = new PreviewCompanyService();
        var vm = new OwnCompaniesViewModel(preview);
        vm.InitializeAsync().GetAwaiter().GetResult();
        vm.SelectedItem = vm.Items[0];
        Check(vm.Name == "Minta Kereskedelmi Kft." && !vm.IsDirty, "Company editor did not load details.");
        vm.Name = "Módosított cégnév";
        vm.SaveCommand.Execute(null);
        Check(vm.IsDirty && vm.Name == "Módosított cégnév" && vm.Message.Contains("másik kliens"), "Failed save lost edits.");
        vm.Name = "Minta Kereskedelmi Kft.";
        var view = new OwnCompaniesView { DataContext = vm };
        view.Measure(new Size(1250, 800));
        view.Arrange(new Rect(0, 0, 1250, 800));
        view.UpdateLayout();
        var bitmap = new RenderTargetBitmap(1250, 800, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create("obj/ModelVerification/own-companies-preview.png");
        encoder.Save(file);
    }

    private sealed class PreviewCompanyService : IOwnCompanyService
    {
        public Task<Response<PageResult<OwnCompanyListItem>>> ListAsync(OwnCompanyQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(Response<PageResult<OwnCompanyListItem>>.Success(new(new[]
            {
                new OwnCompanyListItem(1, "Minta Kereskedelmi Kft.", "Minta Kft.", "12345678-2-43", "Budapest", true),
                new OwnCompanyListItem(2, "Alfa Szolgáltató Kft.", "Alfa Kft.", "87654321-2-41", "Budapest", true)
            }, 2, query.Page, query.PageSize)));
        public Task<Response<OwnCompanyDetails>> GetAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(Response<OwnCompanyDetails>.Success(new(id, "Minta Kereskedelmi Kft.", "Minta Kft.", "12345678-2-43", "1111", "Budapest", "Minta utca 12.", "Teszteléshez használt mintacég.", true, new byte[8])));
        public Task<Response<OwnCompanyDetails>> SaveAsync(SaveOwnCompanyRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Response<OwnCompanyDetails>.Failure(ErrorCode.ConcurrencyConflict, "A cég adatait másik kliens módosította."));
        public Task<Response<OwnCompanyDetails>> SetActiveAsync(int id, bool isActive, byte[] rowVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
