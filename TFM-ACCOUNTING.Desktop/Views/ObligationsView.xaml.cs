using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TFM_ACCOUNTING.Business.Attachments;
using TFM_ACCOUNTING.Business.Common;
using TFM_ACCOUNTING.Business.Obligations;

namespace TFM_ACCOUNTING.Desktop.Views;

public partial class ObligationsView : UserControl
{
    private readonly TextBox search = new() { MaxLength = 200 }, min = new(), max = new();
    private readonly ComboBox company = new(), partner = new(), type = new(), category = new(), state = new(),
        archived = new(), attachment = new(), dateField = new(), dueState = new();
    private readonly DatePicker from = new(), to = new();
    private ObligationQuery filter = new();
    private string quickTab = "all";
    private int total;
    private ObligationRow? Selected => Rows.SelectedItem as ObligationRow;

    public ObligationsView()
    {
        InitializeComponent();
        AddFilter("Saját cég", company, PrimaryFilters);
        AddFilter("Partner / jogosult", partner, PrimaryFilters);
        AddFilter("Állapot", state, PrimaryFilters);
        AddFilter("Bizonylatszám / leírás", search, PrimaryFilters);
        search.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Filter(search, e); };
        AddFilter("Típus", type);
        AddFilter("Kategória", category);
        AddFilter("Esedékesség", dueState);
        AddFilter("Archiválás", archived);
        AddFilter("Csatolmány", attachment);
        AddFilter("Dátum alapja", dateField);
        AddFilter("Dátumtól", from);
        AddFilter("Dátumig", to);
        AddFilter("Minimum (Ft)", min);
        AddFilter("Maximum (Ft)", max);
        Choices(state, ("Összes", ""), ("Nyitott", "open"), ("Részben rendezett", "partial"),
            ("Rendezett", "settled"));
        Choices(dueState, ("Összes", ""), ("Lejárt", "overdue"), ("Következő 7 nap", "week"));
        Choices(archived, ("Aktív", "false"), ("Archivált", "true"), ("Összes", ""));
        Choices(attachment, ("Mindegy", ""), ("Van", "true"), ("Nincs", "false"));
        Choices(dateField, ("Határidő", "due"), ("Kiállítás", "issue"), ("Teljesítés", "performance"), ("Vonatkozási hónap", "period"));
        UpdateButtons();
    }

    public ObligationService? Service { get; set; }
    public AttachmentService? Attachments { get; set; }
    public bool IsBusy { get; private set; }
    public Task InitializeAsync() => RunAsync(async service =>
    {
        if (await LoadOptionsAsync(service) is not null) await LoadAsync(service);
    });

    private void AddFilter(string label, FrameworkElement element, Panel? area = null)
    {
        var panel = new StackPanel { Margin = new Thickness(5, 0, 5, 0) };
        ObligationEditor.Field(panel, label, element);
        (area ?? FilterFields).Children.Add(panel);
    }

    private sealed record Choice(string Label, string Value);
    private static void Choices(ComboBox box, params (string Label, string Value)[] values)
    {
        box.ItemsSource = values.Select(x => new Choice(x.Label, x.Value)).ToArray();
        box.DisplayMemberPath = nameof(Choice.Label);
        box.SelectedValuePath = nameof(Choice.Value);
        box.SelectedIndex = 0;
    }
    private static string? Value(ComboBox box) => box.SelectedValue as string;
    private static bool? Flag(ComboBox box) => bool.TryParse(Value(box), out var value) ? value : null;

    private async Task<ObligationOptions?> LoadOptionsAsync(ObligationService service)
    {
        var result = await service.OptionsAsync();
        if (!result.IsSuccess) { ShowError(result); return null; }
        var options = result.Data!;
        ObligationEditor.SetOptions(company, options.Companies, ObligationEditor.Id(company), true, "Összes");
        ObligationEditor.SetOptions(partner, options.Partners, ObligationEditor.Id(partner), true, "Összes");
        ObligationEditor.SetOptions(type, options.Types, ObligationEditor.Id(type), true, "Összes");
        ObligationEditor.SetOptions(category, options.Categories.Prepend(new(-1, "Kategória nélkül", true)), ObligationEditor.Id(category), true, "Összes");
        return options;
    }

    private async Task LoadAsync(ObligationService service, int? select = null)
    {
        var result = await service.ListAsync(filter);
        if (!result.IsSuccess) { ShowError(result); return; }
        var page = result.Data!;
        var lastPage = Math.Max(1, (int)Math.Ceiling(page.Page.TotalCount / 25d));
        if (filter.Page > lastPage)
        {
            filter = filter with { Page = lastPage };
            await LoadAsync(service, select);
            return;
        }
        total = page.Page.TotalCount;
        Rows.ItemsSource = page.Page.Items;
        Rows.SelectedItem = page.Page.Items.FirstOrDefault(x => x.Id == select);
        PayableLabel.Text = FormatMoney(page.Payable);
        OverdueLabel.Text = FormatMoney(page.Overdue);
        DueSoonLabel.Text = FormatMoney(page.DueSoon);
        MonthlyPaidLabel.Text = FormatMoney(page.PaidThisMonth);
        CreditLabel.Text = $"Felhasználható jóváírás: {FormatMoney(page.Credit)}";
        StatusLabel.Text = $"{filter.Page}. oldal / {lastPage} · {total} tétel";
    }

    private static string FormatMoney(decimal value) =>
        value.ToString(value == decimal.Truncate(value) ? "N0" : "N2", CultureInfo.GetCultureInfo("hu-HU")) + " Ft";

    private void QuickFilter(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tab }) return;
        quickTab = tab;
        Filter(sender, e);
    }

    private async void Filter(object sender, RoutedEventArgs e)
    {
        if (!OptionalMoney(min.Text, out var minimum) || !OptionalMoney(max.Text, out var maximum))
        {
            StatusLabel.Text = "Ellenőrizd az összeghatárokat.";
            return;
        }
        var selectedCategory = ObligationEditor.Id(category);
        var candidate = new ObligationQuery(search.Text, ObligationEditor.Id(company), ObligationEditor.Id(partner),
            ObligationEditor.Id(type), selectedCategory == -1 ? null : selectedCategory, Flag(archived), Flag(attachment),
            Value(state), Value(dateField), ObligationEditor.Date(from), ObligationEditor.Date(to), minimum, maximum,
            NoCategory: selectedCategory == -1, DueState: quickTab == "overdue" ? "overdue" : Value(dueState),
            QuickType: quickTab is "all" or "overdue" ? null : quickTab);
        if (candidate.From > candidate.To || candidate.Min > candidate.Max)
        {
            StatusLabel.Text = "A tartomány kezdete nem lehet nagyobb a végénél.";
            return;
        }
        await RunAsync(async service => { filter = candidate; await LoadAsync(service); });
    }

    private static bool OptionalMoney(string text, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!ObligationEditor.Money(text, out var parsed)) return false;
        value = parsed;
        return true;
    }

    private async void Refresh(object sender, RoutedEventArgs e) => await InitializeAsync();
    private async void Add(object sender, RoutedEventArgs e) => await OpenEditorAsync(null);
    private async void Edit(object sender, RoutedEventArgs e)
    {
        if (Selected is { } row) await OpenEditorAsync(row.Id);
    }
    private Task OpenEditorAsync(int? id) => RunAsync(async service =>
    {
        var options = await LoadOptionsAsync(service);
        if (options is null) return;
        ObligationDetails? details = null;
        if (id is int key)
        {
            var result = await service.GetAsync(key);
            if (!result.IsSuccess) { ShowError(result); return; }
            details = result.Data;
        }
        var editor = new ObligationEditor(service, options, details, Attachments) { Owner = Window.GetWindow(this) };
        editor.ShowDialog();
        if (editor.SavedId.HasValue || editor.HasAttachmentChanges) await LoadAsync(service, editor.SavedId ?? id);
    });

    private async void OpenCashMovements(object sender, RoutedEventArgs e) => await RunAsync(service =>
    {
        var window = new CashMovementsWindow(service) { Owner = Window.GetWindow(this) };
        window.ShowDialog();
        return Task.CompletedTask;
    });

    private async void OpenSettlements(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) return;
        await RunAsync(async service =>
        {
            var result = await service.GetSettlementsAsync(row.Id);
            if (!result.IsSuccess) { ShowError(result); return; }
            var window = new SettlementsWindow(service, row.Id, result.Data!) { Owner = Window.GetWindow(this) };
            window.ShowDialog();
            if (window.HasChanges) await LoadAsync(service, row.Id);
        });
    }


    private async void Archive(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row) return;
        await RunAsync(async service =>
        {
            var detail = await service.GetAsync(row.Id);
            if (!detail.IsSuccess) { ShowError(detail); return; }
            var target = !detail.Data!.Archived;
            var prompt = target ? "Archiválod a kijelölt tételt?" : "Visszaállítod a kijelölt tételt?";
            if (!AccountingUi.Confirm(Window.GetWindow(this), "Tétel állapota", prompt,
                    row.Archived ? "Visszaállítás" : "Archiválás", row.Archived ? "\uE7A7" : "\uE7B8", !row.Archived)) return;
            var result = await service.SetArchivedAsync(row.Id, target, detail.Data.Input.Version!);
            if (!result.IsSuccess) { ShowError(result); return; }
            await LoadAsync(service);
            StatusLabel.Text = result.Message;
        });
    }

    private async void PreviousPage(object sender, RoutedEventArgs e)
    {
        if (filter.Page > 1) await RunAsync(async service => { filter = filter with { Page = filter.Page - 1 }; await LoadAsync(service); });
    }
    private async void NextPage(object sender, RoutedEventArgs e)
    {
        if ((long)filter.Page * 25 < total) await RunAsync(async service => { filter = filter with { Page = filter.Page + 1 }; await LoadAsync(service); });
    }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();
    private void UpdateButtons()
    {
        if (EditButton is null) return;
        EditButton.IsEnabled = SettlementsButton.IsEnabled = ArchiveButton.IsEnabled = Selected is not null;
        SelectionLabel.Text = Selected is null ? "Jelölj ki egy tételt" : "1 tétel kijelölve";
        ArchiveButton.Content = Selected?.Archived == true ? "Visszaállítás" : "Archiválás";
        AccountingUi.SetGlyph(ArchiveButton, Selected?.Archived == true ? "\uE7A7" : "\uE7B8");
        Previous.IsEnabled = filter.Page > 1;
        Next.IsEnabled = (long)filter.Page * 25 < total;
    }

    private async void Export(object sender, RoutedEventArgs e) => await RunAsync(async service =>
    {
        var csv = new StringBuilder("Saját cég;Partner;Típus;Leírás;Bizonylatszám;Határidő;Összeg (Ft);Rendezett (Ft);Hátralék (Ft);Állapot;Archivált\r\n");
        var culture = CultureInfo.GetCultureInfo("hu-HU");
        var response = await service.ExportRowsAsync(filter);
        if (!response.IsSuccess) { ShowError(response); return; }
        var items = response.Data!;
        foreach (var row in items)
        {
            csv.AppendLine(string.Join(";", Csv(row.Company), Csv(row.Partner), Csv(row.Type), Csv(row.Description),
                Csv(row.Number), row.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                row.Amount.ToString(culture), row.Settled.ToString(culture), row.Balance.ToString(culture),
                Csv(row.Status), row.Archived ? "Igen" : "Nem"));
        }
        var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "Tételek.csv", DefaultExt = ".csv" };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        await File.WriteAllTextAsync(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
        StatusLabel.Text = $"{items.Count} tételt exportáltunk.";
    });

    private static string Csv(string? value)
    {
        value ??= "";
        if (value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@') value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private async Task RunAsync(Func<ObligationService, Task> action)
    {
        if (IsBusy || Service is not { } service) return;
        IsBusy = true;
        IsEnabled = false;
        try { await action(service); }
        catch (IOException) { StatusLabel.Text = "A CSV-fájlt nem sikerült menteni."; }
        catch (UnauthorizedAccessException) { StatusLabel.Text = "A kiválasztott fájlhely nem írható."; }
        finally { IsBusy = false; IsEnabled = true; UpdateButtons(); }
    }

    private void ShowError<T>(Response<T> response) => StatusLabel.Text = response.ValidationErrors.Count > 0
        ? string.Join(" ", response.ValidationErrors.Select(x => x.Message)) : response.Message;
}

