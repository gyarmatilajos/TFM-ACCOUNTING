using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TFM_ACCOUNTING.Business.Common;
using TFM_ACCOUNTING.Business.Obligations;

namespace TFM_ACCOUNTING.Desktop.Views;

internal sealed class SettlementsWindow : AccountingDialog
{
    private readonly ObligationService service;
    private readonly int obligationId;
    private SettlementScreen screen;
    private readonly DataGrid history = new()
    {
        AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
        SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow
    };
    private readonly TextBlock balance = new() { FontSize = 19, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock feedback = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly Button edit = AccountingUi.Action("Módosítás", "\uE70F"), remove = AccountingUi.Action("Visszavonás", "\uE7A7");
    private readonly Button pay = AccountingUi.Action("Pénzmozgás rögzítése", "\uE710", "PrimaryButton"), offset = AccountingUi.Action("Jóváírás beszámítása", "\u267B");
    private bool busy;

    public SettlementsWindow(ObligationService service, int obligationId, SettlementScreen screen)
    {
        this.service = service;
        this.obligationId = obligationId;
        this.screen = screen;
        Title = "Rendezések · " + screen.ItemName;
        Width = 1020;
        Height = 530;
        MinWidth = 900;
        MinHeight = 350;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var layout = new DockPanel { Margin = new Thickness(20) };
        var top = new StackPanel();
        DockPanel.SetDock(top, Dock.Top);
        layout.Children.Add(top);
        top.Children.Add(AccountingUi.Heading("Rendezések"));
        top.Children.Add(new TextBlock { Text = screen.ItemName, Margin = new Thickness(0, 0, 0, 10) });
        var balanceCard = AccountingUi.Card(balance, 10);
        balanceCard.Margin = new Thickness(0, 0, 0, 12);
        top.Children.Add(balanceCard);
        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        top.Children.Add(actions);
        foreach (var button in new[] { pay, offset, edit, remove }) actions.Children.Add(button);

        var footer = new StackPanel();
        DockPanel.SetDock(footer, Dock.Bottom);
        layout.Children.Add(footer);
        footer.Children.Add(feedback);
        var close = AccountingUi.Close("Bezárás");
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Margin = new Thickness(0, 8, 0, 0);
        footer.Children.Add(close);
        layout.Children.Add(AccountingUi.Card(history, 0));
        Content = layout;

        AddColumn("Dátum", nameof(SettlementRow.Date), 100, "yyyy.MM.dd.");
        AddColumn("Összeg (Ft)", nameof(SettlementRow.Amount), 110, "N2");
        AddColumn("Rendezési típus", nameof(SettlementRow.Type), 175);
        AddColumn("Fizetési mód", nameof(SettlementRow.Method), 115);
        AddColumn("Kapcsolódó tétel", nameof(SettlementRow.RelatedItem), 150);
        AddColumn("Hivatkozás", nameof(SettlementRow.Reference), 110);
        AddColumn("Megjegyzés", nameof(SettlementRow.Notes), new DataGridLength(1, DataGridLengthUnitType.Star));

        history.SelectionChanged += (_, _) => UpdateButtons();
        pay.Click += async (_, _) => await OpenEditorAsync(false);
        offset.Click += async (_, _) => await OpenEditorAsync(true);
        edit.Click += async (_, _) =>
        {
            if (history.SelectedItem is SettlementRow row) await OpenEditorAsync(row.IsOffset, row);
        };
        remove.Click += async (_, _) => await RemoveAsync();
        Closing += (_, e) => { if (busy) e.Cancel = true; };
        Populate();
    }

    public bool HasChanges { get; private set; }

    private void AddColumn(string title, string property, DataGridLength width, string? format = null) =>
        history.Columns.Add(new DataGridTextColumn
        {
            Header = title, Width = width,
            Binding = new Binding(property) { Mode = BindingMode.OneWay, StringFormat = format }
        });

    private void Populate()
    {
        history.ItemsSource = screen.Rows;
        balance.Text = screen.Balance < 0 ? $"Fennmaradó jóváírás: {-screen.Balance:N2} Ft" : $"Hátralék: {screen.Balance:N2} Ft";
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        edit.IsEnabled = remove.IsEnabled = history.SelectedItem is SettlementRow;
        pay.IsEnabled = screen.Balance != 0 && screen.Types.Any(x => x.IsActive && !x.IsOffset) && screen.Methods.Any(x => x.IsActive);
        offset.IsEnabled = screen.Balance != 0 && screen.Types.Any(x => x.IsActive && x.IsOffset) && screen.Candidates.Count > 0;
    }

    private Task OpenEditorAsync(bool isOffset, SettlementRow? row = null) => BusyAsync(async () =>
    {
        var editor = new SettlementEditor(service, obligationId, screen, isOffset, row) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            HasChanges = true;
            await RefreshAsync();
        }
    });

    private async Task RemoveAsync()
    {
        if (history.SelectedItem is not SettlementRow row) return;
        var prompt = row.IsOffset
            ? "Visszavonod a beszámítást? Mindkét tétel hátraléka újraszámolódik."
            : "Visszavonod a rögzített pénzmozgást?";
        if (!AccountingUi.Confirm(this, "Rendezés visszavonása", prompt, "Visszavonás", "\uE7A7", true)) return;
        await BusyAsync(async () =>
        {
            var result = await service.DeleteSettlementAsync(obligationId, row.Version);
            if (result.IsSuccess) HasChanges = true;
            await RefreshAsync();
            if (!result.IsSuccess) feedback.Text = Error(result);
        });
    }

    private async Task RefreshAsync()
    {
        var refreshed = await service.GetSettlementsAsync(obligationId);
        if (!refreshed.IsSuccess) { feedback.Text = Error(refreshed); return; }
        screen = refreshed.Data!;
        feedback.Text = "";
        Populate();
    }

    private async Task BusyAsync(Func<Task> action)
    {
        if (busy) return;
        busy = true;
        ((UIElement)Content).IsEnabled = false;
        try { await action(); }
        finally { busy = false; ((UIElement)Content).IsEnabled = true; UpdateButtons(); }
    }

    internal static string? Error<T>(Response<T> response) => response.ValidationErrors.Count == 0
        ? response.Message : string.Join(Environment.NewLine, response.ValidationErrors.Select(x => x.Message));
}

internal sealed class SettlementEditor : AccountingDialog
{
    private readonly ObligationService service;
    private readonly int obligationId;
    private readonly bool isOffset;
    private readonly SettlementRow? existing;
    private readonly SettlementScreen screen;
    private readonly ComboBox type = new(), method = new(), target = new();
    private readonly DatePicker date = new();
    private readonly TextBox amount = new(), reference = new() { MaxLength = 200 }, notes = new() { MaxLength = 2000, AcceptsReturn = true, Height = 65 };
    private readonly TextBlock feedback = new() { TextWrapping = TextWrapping.Wrap };
    private readonly CheckBox full = new() { Content = "Teljes hátralék rendezése", Margin = new Thickness(0, 0, 0, 8) };
    private bool saving;

    public SettlementEditor(ObligationService service, int obligationId, SettlementScreen screen, bool isOffset, SettlementRow? existing)
    {
        this.service = service;
        this.obligationId = obligationId;
        this.isOffset = isOffset;
        this.existing = existing;
        this.screen = screen;
        foreach (var selector in new[] { type, method, target })
            selector.SetResourceReference(FrameworkElement.StyleProperty, "FilterComboBox");

        Title = isOffset ? "Jóváírás beszámítása" : screen.Balance < 0 || existing?.Amount < 0 ? "Visszafizetés rögzítése" : "Pénzmozgás rögzítése";
        Width = 650;
        SizeToContent = SizeToContent.Height;
        MaxHeight = SystemParameters.WorkArea.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var fields = new StackPanel { Margin = new Thickness(20) };
        Content = new ScrollViewer { Content = AccountingUi.Card(fields, 0), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        fields.Children.Add(AccountingUi.Heading(Title));
        fields.Children.Add(new TextBlock
        {
            Text = $"{screen.ItemName} · Hátralék: {screen.Balance:N2} Ft",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10)
        });
        var types = screen.Types.Where(x => x.IsOffset == isOffset && (x.IsActive || x.Id == existing?.TypeId)).ToArray();
        type.ItemsSource = types;
        type.DisplayMemberPath = nameof(SettlementTypeOption.DisplayName);
        type.SelectedValuePath = nameof(SettlementTypeOption.Id);
        type.SelectedValue = existing?.TypeId ?? types.FirstOrDefault()?.Id;
        date.SelectedDate = existing?.Date.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
        AddPair("Rendezési típus", type, "Rendezés dátuma", date);

        amount.Text = existing is null ? "" : FormatAmount(Math.Abs(existing.Amount));
        if (isOffset)
        {
            if (existing is null)
            {
                target.ItemsSource = screen.Candidates;
                target.DisplayMemberPath = nameof(OffsetCandidate.DisplayName);
                target.SelectedValuePath = nameof(OffsetCandidate.Id);
                target.SelectionChanged += (_, _) => FillOffsetAmount();
                ObligationEditor.Field(fields, "Kapcsolódó tétel", target);
            }
            else ObligationEditor.Field(fields, "Kapcsolódó tétel", new TextBlock { Text = existing.RelatedItem ?? "Kapcsolt rendezés" });
            ObligationEditor.Field(fields, "Beszámítandó összeg (Ft)", amount);
        }
        else
        {
            var methods = screen.Methods.Where(x => x.IsActive || x.Id == existing?.MethodId);
            ObligationEditor.SetOptions(method, methods, existing?.MethodId);
            AddPair("Fizetési mód", method, "Rendezendő összeg (Ft)", amount);
        }

        if (!isOffset && existing is null)
        {
            fields.Children.Add(full);
            full.Checked += (_, _) => { amount.Text = FormatAmount(Math.Abs(screen.Balance)); amount.IsEnabled = false; };
            full.Unchecked += (_, _) => amount.IsEnabled = true;
        }

        reference.Text = existing?.Reference ?? "";
        notes.Text = existing?.Notes ?? "";
        ObligationEditor.Field(fields, "Hivatkozás", reference);
        ObligationEditor.Field(fields, "Megjegyzés", notes);
        fields.Children.Add(feedback);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var save = AccountingUi.Action("Mentés", "\uE74E", "PrimaryButton");
        save.Click += async (_, _) => await SaveAsync();
        buttons.Children.Add(AccountingUi.Cancel());
        save.Margin = new Thickness(8, 0, 0, 0);
        buttons.Children.Add(save);
        fields.Children.Add(buttons);
        Closing += (_, e) => { if (saving) e.Cancel = true; };

        void AddPair(string leftLabel, FrameworkElement left, string rightLabel, FrameworkElement right)
        {
            var pair = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
            var leftField = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
            var rightField = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
            ObligationEditor.Field(leftField, leftLabel, left);
            ObligationEditor.Field(rightField, rightLabel, right);
            pair.Children.Add(leftField);
            pair.Children.Add(rightField);
            fields.Children.Add(pair);
        }
    }

    private void FillOffsetAmount()
    {
        if (target.SelectedItem is OffsetCandidate candidate)
            amount.Text = FormatAmount(Math.Min(Math.Abs(screen.Balance), Math.Abs(candidate.Balance)));
    }

    private static string FormatAmount(decimal value) => value.ToString("0.##", CultureInfo.GetCultureInfo("hu-HU"));

    private async Task SaveAsync()
    {
        if (saving) return;
        var other = ObligationEditor.Id(target);
        if (isOffset && existing is null && !other.HasValue)
        {
            feedback.Text = "Válaszd ki a kapcsolódó tételt.";
            return;
        }
        var value = 0m;
        if (ObligationEditor.Date(date) is not DateOnly day || ObligationEditor.Id(type) is not int typeId ||
            (full.IsChecked != true && !ObligationEditor.Money(amount.Text, out value)))
        {
            feedback.Text = "Ellenőrizd a rendezés típusát, dátumát és összegét.";
            return;
        }

        saving = true;
        ((UIElement)Content).IsEnabled = false;
        var result = await service.SaveSettlementAsync(obligationId, new(day, value, typeId, ObligationEditor.Id(method),
            other, reference.Text, notes.Text, full.IsChecked == true, existing?.Version));
        saving = false;
        ((UIElement)Content).IsEnabled = true;
        if (result.IsSuccess) DialogResult = true;
        else feedback.Text = SettlementsWindow.Error(result);
    }
}
