using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using TFM_ACCOUNTING.Business.Common;
using TFM_ACCOUNTING.Business.Partners;
using TFM_ACCOUNTING.Data;
using TFM_ACCOUNTING.Data.Entities;
using TFM_ACCOUNTING.Desktop.ViewModels;
using TFM_ACCOUNTING.Desktop.Views;

static class PartnerChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static SavePartnerRequest Request(string name, string? tax, int type = 1) =>
        new(null, name, null, tax, null, null, null, null, type);

    public static async Task ValidateWithoutDatabaseAsync(IDbContextFactory<AccountingDbContext> factory)
    {
        var service = new PartnerService(factory);
        foreach (var request in new[]
        {
            Request(" ", "VALID"), Request("Valid", " "), Request(new string('x', 201), "VALID"),
            Request("Valid", "VALID", 0), Request("Valid", "VALID") with { Notes = new string('x', 2001) },
            Request("Valid", "VALID") with { Id = 1 }
        }) Check((await service.SaveAsync(request)).ErrorCode == ErrorCode.Validation, "Partner validation accessed the database or accepted invalid values.");
        Check((await service.ListAsync(new(Page: int.MaxValue))).ErrorCode == ErrorCode.Validation, "Invalid partner page accepted.");
        Check((await service.ListAsync(new(PartnerTypeId: 0))).ErrorCode == ErrorCode.Validation, "Invalid partner filter accepted.");
        var cancelled = false;
        try { await service.ListTypesAsync(cancellationToken: new CancellationToken(true)); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "Partner cancellation did not propagate.");
        var matches = new List<RelatedRecord> { new(1, "Minta", "ABC") };
        var warning = Response<int>.ConfirmationRequired([new("Match", "Check", matches)], "Megerősítés szükséges.");
        matches.Clear();
        Check(warning.RequiresConfirmation && !warning.IsSuccess && warning.Warnings[0].RelatedRecords.Count == 1 && warning.Data == 0,
            "Warning response was mutable or marked successful.");
    }

    public static async Task IntegrationAsync(IDbContextFactory<AccountingDbContext> factory)
    {
        var service = new PartnerService(factory);
        var created = await service.SaveAsync(Request(" Beszállító Teszt Kft. ", " PARTNER-001 ") with { ShortName = " ", Notes = new string('x', 2000), City = " Budapest " });
        Check(created.IsSuccess, "Partner create failed: " + created.Message);
        var original = created.Data!;
        Check(original is { Name: "Beszállító Teszt Kft.", ShortName: null, TaxNumber: "PARTNER-001", City: "Budapest", RowVersion.Length: 8 }, "Partner normalization failed.");
        var duplicate = await service.SaveAsync(Request("Different", "partner-001") with { ConfirmDuplicateName = true });
        Check(duplicate.ErrorCode == ErrorCode.Duplicate, "Partner tax number duplicate bypassed.");
        var nameRequest = Request(" beszállító teszt kft. ", "PARTNER-002", 2);
        var sameName = await service.SaveAsync(nameRequest);
        Check(sameName.RequiresConfirmation && sameName.Warnings.SelectMany(x => x.RelatedRecords).Any(x => x.Id == original.Id), "Partner name match not returned.");
        Check((await service.ListAsync(new(Search: "PARTNER-002"))).Data!.TotalCount == 0, "Unconfirmed partner was persisted.");
        var confirmed = await service.SaveAsync(nameRequest with { ConfirmDuplicateName = true });
        Check(confirmed.IsSuccess, "Confirmed duplicate name rejected.");
        var sameNameDetails = confirmed.Data!;
        var unchanged = await service.SaveAsync(Request(sameNameDetails.Name, sameNameDetails.TaxNumber, 2) with
        { Id = sameNameDetails.Id, RowVersion = sameNameDetails.RowVersion, Notes = "Javítás" });
        Check(unchanged.IsSuccess, "Unchanged partner name demanded repeated confirmation.");

        var renamed = await service.SaveAsync(Request("Másik név", original.TaxNumber) with
        { Id = original.Id, RowVersion = original.RowVersion, ShortName = "BTS" });
        Check(renamed.IsSuccess, "Partner edit failed.");
        Check((await service.SaveAsync(Request("Elavult", original.TaxNumber) with { Id = original.Id, RowVersion = original.RowVersion })).ErrorCode == ErrorCode.ConcurrencyConflict, "Stale partner edit accepted.");
        var renameToExisting = await service.SaveAsync(Request(sameNameDetails.Name, original.TaxNumber) with { Id = original.Id, RowVersion = renamed.Data!.RowVersion });
        Check(renameToExisting.RequiresConfirmation, "Partner rename to duplicate name had no warning.");
        foreach (var search in new[] { "Másik név", "BTS", "PARTNER-001" })
            Check((await service.ListAsync(new(Search: search))).Data!.TotalCount == 1, "Partner search failed: " + search);
        Check((await service.ListAsync(new(Search: "PARTNER-", PartnerTypeId: 2))).Data!.Items.All(x => x.PartnerTypeId == 2), "Partner type filter failed.");
        Check((await service.ListAsync(new(Search: "PARTNER-", PageSize: 1))).Data!.Items.Count == 1, "Partner paging failed.");
        Check((await service.ListAsync(new(Search: "PARTNER-", Page: 2, PageSize: 1))).Data!.Items.Count == 1, "Partner second page failed.");
        Check((await service.ListAsync(new(Search: "PARTNER-", SortBy: "TaxNumber", Descending: true))).Data!.Items[0].TaxNumber == "PARTNER-002", "Partner sorting failed.");
        Check((await service.ListAsync(new(SortBy: "PartnerType"))).IsSuccess, "Partner type sorting failed.");
        var inactive = await service.SetActiveAsync(original.Id, false, renamed.Data!.RowVersion);
        Check(inactive.IsSuccess && !inactive.Data!.IsActive, "Partner inactivation failed.");
        Check((await service.ListAsync(new(Search: "PARTNER-001", IsActive: true))).Data!.TotalCount == 0, "Inactive partner appeared active.");
        Check((await service.SetActiveAsync(original.Id, true, renamed.Data.RowVersion)).ErrorCode == ErrorCode.ConcurrencyConflict, "Stale partner activation accepted.");
        var active = await service.SetActiveAsync(original.Id, true, inactive.Data!.RowVersion);
        Check(active.IsSuccess, "Partner reactivation failed.");
        var generated = await Task.WhenAll(
            service.SaveAsync(Request("Belső A", null, 2) with { GenerateInternalIdentifier = true }),
            service.SaveAsync(Request("Belső B", null, 2) with { GenerateInternalIdentifier = true }));
        Check(generated.All(x => x.IsSuccess) && generated[0].Data!.TaxNumber != generated[1].Data!.TaxNumber, "Partner internal identifiers failed.");
        var races = await Task.WhenAll(service.SaveAsync(Request("Partner verseny A", "PARTNER-RACE")), service.SaveAsync(Request("Partner verseny B", "PARTNER-RACE")));
        Check(races.Count(x => x.IsSuccess) == 1 && races.Count(x => x.ErrorCode == ErrorCode.Duplicate) == 1, "Partner tax duplicate race failed.");

        await using var db = await factory.CreateDbContextAsync();
        var type = await db.PartnerTypes.SingleAsync(x => x.Id == 2);
        type.IsActive = false;
        await db.SaveChangesAsync();
        Check(!(await service.ListTypesAsync()).Data!.Any(x => x.Id == 2), "Inactive type available for new partner.");
        Check((await service.ListTypesAsync(true)).Data!.Any(x => x.Id == 2), "Inactive type unavailable for existing partner.");
        Check((await service.SaveAsync(Request("Invalid type", "PARTNER-BAD", 2))).ErrorCode == ErrorCode.Validation, "Inactive type accepted for new partner.");
        Check((await service.SaveAsync(Request("Missing type", "PARTNER-BAD", int.MaxValue))).ErrorCode == ErrorCode.Validation, "Missing type accepted.");
        var retained = await service.SaveAsync(Request(sameNameDetails.Name, sameNameDetails.TaxNumber, 2) with
        { Id = sameNameDetails.Id, RowVersion = unchanged.Data!.RowVersion, Notes = "Inaktív típus megőrizve" });
        Check(retained.IsSuccess, "Existing inactive type could not be retained.");
        Check((await service.SaveAsync(Request("Másik név", "PARTNER-001", 2) with { Id = original.Id, RowVersion = active.Data!.RowVersion })).ErrorCode == ErrorCode.Validation, "Switch to inactive type accepted.");
        Check((await service.GetAsync(int.MaxValue)).ErrorCode == ErrorCode.NotFound, "Missing partner not reported.");
        // Own-company and partner identifiers are independent domains.
        var companyTax = await db.OwnCompanies.Where(x => x.TaxNumber != "TEST-001").Select(x => x.TaxNumber).FirstAsync();
        Check((await service.SaveAsync(Request("Saját cég partnerként", companyTax))).IsSuccess, "Own-company tax number was incorrectly reserved in the partner domain.");
        db.Obligations.Add(new Obligation
        {
            OwnCompanyId = await db.OwnCompanies.Select(x => x.Id).FirstAsync(), PartnerId = original.Id,
            ObligationTypeId = 2, Description = "Partnerhez tartozó kötelezettség", DueDate = new DateOnly(2026, 11, 20), Amount = 1000m
        });
        await db.SaveChangesAsync();
        Check((await service.SetActiveAsync(original.Id, false, active.Data!.RowVersion)).IsSuccess, "Referenced partner inactivation failed.");
        Check(await db.Obligations.AnyAsync(x => x.PartnerId == original.Id), "Partner inactivation lost an obligation.");
        Console.WriteLine("PASS: partners: warnings without writes, confirmation, tax duplicates and race, normalization, types, inactive-type retention, searches, sort, pages, concurrency, activation, internal identifiers.");
    }

    public static void RenderPreview()
    {
        foreach (var decision in new[] { new PartnerNameDecision(false), new(true), new(false, 1) })
        {
            var fake = new PreviewPartnerService();
            var vm = new PartnersViewModel(fake, resolveNameMatch: _ => decision);
            vm.InitializeAsync().GetAwaiter().GetResult();
            vm.Name = "Kovács Anna"; vm.TaxNumber = "BELSO-000002";
            vm.SelectedPartnerType = vm.PartnerTypes[0];
            vm.SaveCommand.Execute(null);
            if (decision.SaveAnyway) Check(fake.Confirmed && !vm.IsDirty, "UI did not confirm partner name match.");
            else if (decision.OpenPartnerId.HasValue) Check(vm.TaxNumber == "BELSO-000001" && !vm.IsDirty && !fake.Confirmed, "UI did not open existing partner.");
            else Check(vm.IsDirty && fake.SaveCalls == 1 && vm.TaxNumber == "BELSO-000002", "Cancel lost data or persisted a partner.");
        }
        var previewService = new PreviewPartnerService();
        var preview = new PartnersViewModel(previewService);
        preview.InitializeAsync().GetAwaiter().GetResult();
        preview.SelectedItem = preview.Items[0];
        var view = new PartnersView { DataContext = preview };
        view.Measure(new Size(1250, 820)); view.Arrange(new Rect(0, 0, 1250, 820)); view.UpdateLayout();
        var bitmap = new RenderTargetBitmap(1250, 820, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create("obj/ModelVerification/partners-preview.png"); encoder.Save(file);
        var dialog = new PartnerNameMatchDialog(PreviewPartnerService.Warning().Warnings);
        var content = (FrameworkElement)dialog.Content;
        content.Measure(new Size(720, 380)); content.Arrange(new Rect(0, 0, 720, 380)); content.UpdateLayout();
        var dialogBitmap = new RenderTargetBitmap(720, 380, 96, 96, PixelFormats.Pbgra32); dialogBitmap.Render(content);
        var dialogEncoder = new PngBitmapEncoder(); dialogEncoder.Frames.Add(BitmapFrame.Create(dialogBitmap));
        using var dialogFile = File.Create("obj/ModelVerification/partner-warning-preview.png"); dialogEncoder.Save(dialogFile);
        dialog.Close();
    }

    private sealed class PreviewPartnerService : IPartnerService
    {
        public bool Confirmed { get; private set; }
        public int SaveCalls { get; private set; }
        public static Response<PartnerDetails> Warning() => Response<PartnerDetails>.ConfirmationRequired(
            [new("PartnerNameMatch", "Azonos név", new[] { new RelatedRecord(1, "Kovács Anna", "BELSO-000001") })], "Azonos nevű partner.");
        public Task<Response<IReadOnlyList<PartnerTypeOption>>> ListTypesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
            => Task.FromResult(Response<IReadOnlyList<PartnerTypeOption>>.Success(new[] { new PartnerTypeOption(2, "Magánszemély", true) }));
        public Task<Response<PageResult<PartnerListItem>>> ListAsync(PartnerQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(Response<PageResult<PartnerListItem>>.Success(new(new[] { new PartnerListItem(1, "Kovács Anna", null, "BELSO-000001", "Budapest", true, 2, "Magánszemély") }, 1, 1, 25)));
        public Task<Response<PartnerDetails>> GetAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(Response<PartnerDetails>.Success(new(id, "Kovács Anna", null, "BELSO-000001", "1111", "Budapest", "Minta utca 12.", null, true, new byte[8], 2, "Magánszemély")));
        public Task<Response<PartnerDetails>> SaveAsync(SavePartnerRequest request, CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            if (!request.ConfirmDuplicateName) return Task.FromResult(Warning());
            Confirmed = true;
            return Task.FromResult(Response<PartnerDetails>.Success(new(2, request.Name, request.ShortName, request.TaxNumber!, request.PostalCode,
                request.City, request.AddressLine, request.Notes, request.IsActive, new byte[8], 2, "Magánszemély")));
        }
        public Task<Response<PartnerDetails>> SetActiveAsync(int id, bool isActive, byte[] rowVersion, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
