using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using TFM_ACCOUNTING.Business.Common;
using TFM_ACCOUNTING.Business.Partners;

namespace TFM_ACCOUNTING.Desktop.ViewModels;

public sealed record PartnerFilter(string Name, bool? IsActive);
public sealed record PartnerSort(string Name, string Key);
public sealed record PartnerTypeFilter(string Name, int? Id);
public readonly record struct PartnerNameDecision(bool SaveAnyway, int? OpenPartnerId = null);

public sealed class PartnersViewModel : INotifyPropertyChanged
{
    private readonly IPartnerService service;
    private readonly Func<bool> confirmDiscard;
    private readonly Func<IReadOnlyList<ResponseWarning>, PartnerNameDecision> resolveNameMatch;
    private PartnerTypeOption? selectedPartnerType;
    private readonly List<PartnerCommand> commands = [];
    private PartnerDetails? editing;
    private PartnerListItem? selectedItem;
    private bool isBusy;
    private string message = "";
    private bool isDirty;
    private bool loadingEditor;
    private string name = "";
    private string? shortName, taxNumber, postalCode, city, addressLine, notes;
    private bool isActive = true, generateInternalIdentifier;
    private int page = 1, totalCount;

    public PartnersViewModel(IPartnerService service, Func<bool>? confirmDiscard = null,
        Func<IReadOnlyList<ResponseWarning>, PartnerNameDecision>? resolveNameMatch = null)
    {
        this.service = service;
        this.confirmDiscard = confirmDiscard ?? (() => true);
        this.resolveNameMatch = resolveNameMatch ?? (_ => default);
        SelectedFilter = Filters[0];
        SelectedSort = SortOptions[0];
        ReloadCommand = Command(RefreshAsync);
        SearchCommand = Command(async () =>
        {
            if (!MayDiscard()) return;
            page = 1;
            await RunAsync(async () => { ClearEditor(); await LoadListAsync(); });
        });
        NewCommand = Command(() => { if (MayDiscard()) ClearEditor(); return Task.CompletedTask; });
        SaveCommand = Command(SaveAsync);
        ToggleActiveCommand = Command(ToggleActiveAsync, () => editing is not null);
        PreviousPageCommand = Command(() => ChangePageAsync(-1), () => page > 1);
        NextPageCommand = Command(() => ChangePageAsync(1), () => page * 25 < totalCount);
    }

    public ObservableCollection<PartnerListItem> Items { get; } = [];
    public ObservableCollection<PartnerTypeOption> PartnerTypes { get; } = [];
    public IReadOnlyList<PartnerTypeOption> SelectableTypes => PartnerTypes.Where(x => x.IsActive || x.Id == editing?.PartnerTypeId).ToArray();
    public ObservableCollection<PartnerTypeFilter> TypeFilters { get; } = [new("Összes partnertípus", null)];
    public PartnerTypeFilter? SelectedTypeFilter { get; set; }
    public PartnerTypeOption? SelectedPartnerType
    {
        get => selectedPartnerType;
        set => Edit(ref selectedPartnerType, value);
    }
    public IReadOnlyList<PartnerFilter> Filters { get; } = new[] { new PartnerFilter("Összes állapot", null), new("Aktív", true), new("Inaktív", false) };
    public IReadOnlyList<PartnerSort> SortOptions { get; } = new[] { new PartnerSort("Partner neve", "Name"), new("Rövid név", "ShortName"), new("Adószám / azonosító", "TaxNumber"), new("Partnertípus", "PartnerType"), new("Aktív állapot", "IsActive") };
    public PartnerFilter? SelectedFilter { get; set; }
    public PartnerSort? SelectedSort { get; set; }
    public bool Descending { get; set; }
    public string? Search { get; set; }
    public ICommand ReloadCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand ToggleActiveCommand { get; }
    public ICommand PreviousPageCommand { get; }
    public ICommand NextPageCommand { get; }
    public PartnerListItem? SelectedItem
    {
        get => selectedItem;
        set
        {
            if (selectedItem?.Id == value?.Id || IsBusy) return;
            if (!MayDiscard()) { Changed(); return; }
            selectedItem = value; Changed();
            if (value is not null) _ = RunAsync(() => LoadDetailsAsync(value.Id));
            else ClearEditor();
        }
    }

    public string Name { get => name; set => Edit(ref name, value); }
    public string? ShortName { get => shortName; set => Edit(ref shortName, value); }
    public string? TaxNumber { get => taxNumber; set => Edit(ref taxNumber, value); }
    public string? PostalCode { get => postalCode; set => Edit(ref postalCode, value); }
    public string? City { get => city; set => Edit(ref city, value); }
    public string? AddressLine { get => addressLine; set => Edit(ref addressLine, value); }
    public string? Notes { get => notes; set => Edit(ref notes, value); }
    public bool IsActive { get => isActive; set => Edit(ref isActive, value); }
    public bool GenerateInternalIdentifier
    {
        get => generateInternalIdentifier;
        set { Edit(ref generateInternalIdentifier, value); Changed(nameof(CanEditTaxNumber)); }
    }
    public bool CanEditTaxNumber => !GenerateInternalIdentifier;
    public bool IsDirty { get => isDirty; private set { isDirty = value; Changed(); } }
    public bool IsBusy
    {
        get => isBusy;
        private set { isBusy = value; Changed(); Changed(nameof(IsIdle)); RefreshCommands(); }
    }
    public bool IsIdle => !IsBusy;
    public string Message { get => message; private set { message = value; Changed(); } }
    public string EditorTitle => editing is null ? "Új partner" : "Partner szerkesztése";
    public string ToggleActiveText => editing?.IsActive == true ? "Inaktiválás" : "Újraaktiválás";
    public string PageText => $"{page}. oldal / {Math.Max(1, (totalCount + 24) / 25)} • {totalCount} partner";

    public Task InitializeAsync() => RunAsync(async () => { if (await LoadTypesAsync()) await LoadListAsync(); });
    public Task ReloadTypesAsync() => RunAsync(async () => { await LoadTypesAsync(); });
    public bool MayLeave() => !IsBusy && MayDiscard();
    private bool MayDiscard() => !IsDirty || confirmDiscard();

    private async Task RefreshAsync()
    {
        if (!MayDiscard()) return;
        await RunAsync(async () =>
        {
            var id = editing?.Id;
            if (!await LoadTypesAsync()) return;
            if (await LoadListAsync() && id.HasValue) await LoadDetailsAsync(id.Value);
            else if (!id.HasValue) ClearEditor();
        });
    }

    private Task SaveAsync() => RunAsync(async () =>
    {
        var request = new SavePartnerRequest(editing?.Id, Name, ShortName, TaxNumber, PostalCode, City,
            AddressLine, Notes, SelectedPartnerType?.Id ?? 0, IsActive, GenerateInternalIdentifier, editing?.RowVersion);
        var response = await service.SaveAsync(request);
        if (response.RequiresConfirmation)
        {
            var decision = resolveNameMatch(response.Warnings);
            if (decision.OpenPartnerId.HasValue)
            {
                var match = response.Warnings.SelectMany(x => x.RelatedRecords).FirstOrDefault(x => x.Id == decision.OpenPartnerId);
                if (match is null) { Message = "A kiválasztott partner nem található."; return; }
                Search = match.Identifier; SelectedFilter = Filters[0]; SelectedTypeFilter = TypeFilters[0]; page = 1;
                Changed(nameof(Search)); Changed(nameof(SelectedFilter)); Changed(nameof(SelectedTypeFilter));
                if (!await LoadDetailsAsync(match.Id)) return;
                if (await LoadListAsync()) Message = "A meglévő partnert megnyitottuk.";
                return;
            }
            if (!decision.SaveAnyway) { Message = "A mentést megszakítottad. Az űrlap adatai megmaradtak."; return; }
            response = await service.SaveAsync(request with { ConfirmDuplicateName = true });
        }
        if (!response.IsSuccess) { Failure(response); return; }
        Populate(response.Data!);
        if (await LoadListAsync()) Message = response.Message ?? "Sikeres mentés.";
    });

    private async Task ToggleActiveAsync()
    {
        if (editing is null || !MayDiscard()) return;
        await RunAsync(async () =>
        {
            var response = await service.SetActiveAsync(editing.Id, !editing.IsActive, editing.RowVersion);
            if (!response.IsSuccess) { Failure(response); return; }
            Populate(response.Data!);
            if (await LoadListAsync()) Message = response.Message ?? "Az állapotot módosítottuk.";
        });
    }

    private async Task ChangePageAsync(int delta)
    {
        if (!MayDiscard()) return;
        await RunAsync(async () => { page += delta; ClearEditor(); await LoadListAsync(); });
    }
    private async Task<bool> LoadListAsync()
    {
        var response = await service.ListAsync(new(Search, SelectedFilter?.IsActive, page, 25, SelectedSort?.Key ?? "Name", Descending, SelectedTypeFilter?.Id));
        if (!response.IsSuccess) { Failure(response); return false; }
        var result = response.Data!;
        if (page > 1 && result.Items.Count == 0 && result.TotalCount <= (page - 1) * 25)
        {
            page = Math.Max(1, (result.TotalCount + 24) / 25);
            return await LoadListAsync();
        }
        totalCount = result.TotalCount;
        Items.Clear();
        foreach (var item in result.Items) Items.Add(item);
        selectedItem = Items.FirstOrDefault(x => x.Id == editing?.Id);
        Changed(nameof(SelectedItem)); Changed(nameof(PageText)); RefreshCommands();
        Message = $"{totalCount} partner a megadott feltételekkel.";
        return true;
    }
    private async Task<bool> LoadDetailsAsync(int id)
    {
        var result = await service.GetAsync(id);
        if (!result.IsSuccess)
        {
            selectedItem = Items.FirstOrDefault(x => x.Id == editing?.Id);
            Changed(nameof(SelectedItem));
            Failure(result); return false;
        }
        if (!PartnerTypes.Any(x => x.Id == result.Data!.PartnerTypeId) && !await LoadTypesAsync()) return false;
        Populate(result.Data!);
        return true;
    }
    private void Populate(PartnerDetails partner)
    {
        editing = partner;
        loadingEditor = true;
        Changed(nameof(SelectableTypes));
        SelectedPartnerType = PartnerTypes.FirstOrDefault(x => x.Id == partner.PartnerTypeId);
        Name = partner.Name; ShortName = partner.ShortName; TaxNumber = partner.TaxNumber;
        PostalCode = partner.PostalCode; City = partner.City; AddressLine = partner.AddressLine;
        Notes = partner.Notes; IsActive = partner.IsActive; GenerateInternalIdentifier = false;
        loadingEditor = false; IsDirty = false;
        Changed(nameof(EditorTitle)); Changed(nameof(ToggleActiveText)); RefreshCommands();
    }
    private void ClearEditor()
    {
        Populate(new(0, "", null, "", null, null, null, null, true, [], 0, ""));
        editing = null; selectedItem = null;
        Changed(nameof(SelectedItem)); Changed(nameof(EditorTitle)); Changed(nameof(SelectableTypes)); RefreshCommands();
    }
    private async Task<bool> LoadTypesAsync()
    {
        var response = await service.ListTypesAsync(includeInactive: true);
        if (!response.IsSuccess) { Failure(response); return false; }
        var selectedId = SelectedPartnerType?.Id;
        var filterId = SelectedTypeFilter?.Id;
        loadingEditor = true;
        PartnerTypes.Clear();
        TypeFilters.Clear(); TypeFilters.Add(new("Összes partnertípus", null));
        foreach (var type in response.Data!)
        {
            PartnerTypes.Add(type);
            TypeFilters.Add(new(type.DisplayName, type.Id));
        }
        Changed(nameof(SelectableTypes));
        SelectedPartnerType = PartnerTypes.FirstOrDefault(x => x.Id == selectedId && (x.IsActive || x.Id == editing?.PartnerTypeId));
        SelectedTypeFilter = TypeFilters.FirstOrDefault(x => x.Id == filterId) ?? TypeFilters[0];
        Changed(nameof(SelectedTypeFilter));
        loadingEditor = false;
        return true;
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        try { await action(); }
        catch (Exception) { Message = "A művelet nem sikerült. Próbáld újra."; }
        finally { IsBusy = false; }
    }
    private void Failure<T>(Response<T> response) => Message = response.ValidationErrors.Count > 0
        ? string.Join(Environment.NewLine, response.ValidationErrors.Select(x => x.Message)) : response.Message ?? "Sikertelen művelet.";
    private void Edit<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        if (!loadingEditor) IsDirty = true;
        Changed(propertyName);
    }
    private PartnerCommand Command(Func<Task> action, Func<bool>? canExecute = null)
    {
        var command = new PartnerCommand(action, () => !IsBusy && (canExecute?.Invoke() ?? true));
        commands.Add(command); return command;
    }
    private void RefreshCommands() { foreach (var command in commands) command.Refresh(); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

internal sealed class PartnerCommand(Func<Task> action, Func<bool> canExecute) : ICommand
{
    public bool CanExecute(object? parameter) => canExecute();
    public async void Execute(object? parameter) { if (CanExecute(parameter)) await action(); }
    public event EventHandler? CanExecuteChanged;
    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}



