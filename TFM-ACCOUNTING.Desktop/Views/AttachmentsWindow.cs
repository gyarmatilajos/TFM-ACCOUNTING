using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using TFM_ACCOUNTING.Business.Attachments;

namespace TFM_ACCOUNTING.Desktop.Views;

internal sealed class AttachmentsWindow : AccountingDialog
{
    private readonly AttachmentService service;
    private readonly int obligationId;
    private readonly DataGrid rows = new()
    {
        AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
        SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow
    };
    private readonly TextBlock feedback = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button open = AccountingUi.Action("Megnyitás", "\uE8E5"),
        copy = AccountingUi.Action("Másolat mentése", "\uE74E"),
        notes = AccountingUi.Action("Megjegyzés", "\uE70F"),
        remove = AccountingUi.Action("Eltávolítás", "\uE74D");
    private bool busy;

    public AttachmentsWindow(AttachmentService service, int obligationId, string itemName)
    {
        this.service = service;
        this.obligationId = obligationId;
        Title = "Csatolmányok · " + itemName;
        Width = 1020;
        Height = 530;
        MinWidth = 800;
        MinHeight = 350;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var layout = new DockPanel { Margin = new Thickness(20) };
        var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        var header = new StackPanel();
        header.Children.Add(AccountingUi.Heading("Csatolmányok"));
        header.Children.Add(new TextBlock { Text = itemName, Margin = new Thickness(0, 0, 0, 12) });
        header.Children.Add(actions);
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);
        var upload = AccountingUi.Action("Fájlok csatolása", "\uE723", "PrimaryButton");
        var refresh = AccountingUi.Action("Frissítés", "\uE72C");
        foreach (var button in new[] { upload, open, copy, notes, remove, refresh }) actions.Children.Add(button);

        var footer = new StackPanel();
        DockPanel.SetDock(footer, Dock.Bottom);
        layout.Children.Add(footer);
        footer.Children.Add(new ScrollViewer
        {
            Content = feedback, MaxHeight = 115, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 10, 0, 0)
        });
        var close = AccountingUi.Close("Bezárás");
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Margin = new Thickness(0, 8, 0, 0);
        footer.Children.Add(close);
        layout.Children.Add(AccountingUi.Card(rows, 0));
        Content = layout;

        AddColumn("Fájlnév", nameof(AttachmentRow.OriginalFileName), new DataGridLength(2, DataGridLengthUnitType.Star));
        AddColumn("Méret (bájt)", nameof(AttachmentRow.Size), 115, "N0");
        AddColumn("Feltöltve (UTC)", nameof(AttachmentRow.CreatedAtUtc), 150, "yyyy.MM.dd. HH:mm");
        AddColumn("Típus", nameof(AttachmentRow.ContentType), 130);
        AddColumn("Megjegyzés", nameof(AttachmentRow.Notes), new DataGridLength(1, DataGridLengthUnitType.Star));

        upload.Click += async (_, _) => await UploadAsync();
        refresh.Click += async (_, _) => await RunAsync(LoadAsync);
        open.Click += async (_, _) => await OpenAsync();
        rows.MouseDoubleClick += async (_, _) => await OpenAsync();
        copy.Click += async (_, _) => await SaveCopyAsync();
        notes.Click += async (_, _) => await EditNotesAsync();
        remove.Click += async (_, _) => await RemoveAsync();
        rows.SelectionChanged += (_, _) => UpdateButtons();
        Loaded += async (_, _) => await RunAsync(LoadAsync);
        Closing += (_, e) => { if (busy) e.Cancel = true; };
        UpdateButtons();
    }

    public bool HasChanges { get; private set; }

    private void AddColumn(string title, string property, DataGridLength width, string? format = null) =>
        rows.Columns.Add(new DataGridTextColumn
        {
            Header = title, Width = width,
            Binding = new Binding(property) { Mode = BindingMode.OneWay, StringFormat = format }
        });

    private AttachmentRow? Selected => rows.SelectedItem as AttachmentRow;

    private void UpdateButtons() => open.IsEnabled = copy.IsEnabled = notes.IsEnabled = remove.IsEnabled = Selected is not null;

    private async Task LoadAsync()
    {
        var previous = Selected?.Id;
        var result = await service.ListAsync(obligationId);
        if (!result.IsSuccess) { feedback.Text = SettlementsWindow.Error(result); return; }
        var items = result.Data!;
        rows.ItemsSource = items;
        rows.SelectedItem = items.FirstOrDefault(x => x.Id == previous);
        feedback.Text = items.Count == 0 ? "A tételhez még nincs csatolmány." : "";
    }

    internal static string[] PickFiles(Window owner)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Válaszd ki a csatolandó fájlokat",
            Multiselect = true,
            Filter = "Támogatott fájlok|*.pdf;*.jpg;*.jpeg;*.png;*.doc;*.docx;*.xls;*.xlsx;*.csv"
        };
        return dialog.ShowDialog(owner) == true ? dialog.FileNames : [];
    }

    private async Task UploadAsync()
    {
        var files = PickFiles(this);
        if (files.Length == 0) return;
        await RunAsync(async () =>
        {
            var results = new List<string>();
            foreach (var file in files)
            {
                var result = await Task.Run(() => service.UploadAsync(obligationId, file));
                HasChanges |= result.IsSuccess;
                results.Add($"{Path.GetFileName(file)} – {SettlementsWindow.Error(result)}");
            }
            await LoadAsync();
            feedback.Text = string.Join(Environment.NewLine, results);
        });
    }

    private Task OpenAsync()
    {
        if (Selected is not { } row) return Task.CompletedTask;
        return RunAsync(async () =>
        {
            var result = await Task.Run(() => service.OpenPathAsync(obligationId, row.Id));
            if (!result.IsSuccess) { feedback.Text = SettlementsWindow.Error(result); return; }
            try
            {
                Process.Start(new ProcessStartInfo(result.Data!) { UseShellExecute = true })?.Dispose();
                feedback.Text = "";
            }
            catch (Exception e) when (e is Win32Exception or InvalidOperationException)
            {
                feedback.Text = "A fájl nem nyitható meg. Ellenőrizd a hozzáférést és a fájltípushoz beállított alkalmazást.";
            }
        });
    }

    private async Task SaveCopyAsync()
    {
        if (Selected is not { } row) return;
        var dialog = new SaveFileDialog { FileName = row.OriginalFileName, DefaultExt = Path.GetExtension(row.OriginalFileName),
            Filter = "Eredeti fájltípus|*" + Path.GetExtension(row.OriginalFileName) };
        if (dialog.ShowDialog(this) != true) return;
        await RunAsync(async () =>
        {
            var result = await Task.Run(() => service.SaveCopyAsync(obligationId, row.Id, dialog.FileName));
            feedback.Text = SettlementsWindow.Error(result);
        });
    }

    private async Task EditNotesAsync()
    {
        if (Selected is not { } row) return;
        var editor = new AccountingDialog { Owner = this, Title = "Csatolmány megjegyzése", Width = 490, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var layout = new StackPanel { Margin = new Thickness(18) };
        var text = new TextBox { Text = row.Notes ?? "", MaxLength = 2000, AcceptsReturn = true, Height = 110,
            TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        ObligationEditor.Field(layout, row.OriginalFileName, text);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var save = AccountingUi.Action("Mentés", "\uE74E", "PrimaryButton");
        save.Click += (_, _) => editor.DialogResult = true;
        buttons.Children.Add(AccountingUi.Cancel());
        save.Margin = new Thickness(8, 0, 0, 0);
        buttons.Children.Add(save);
        layout.Children.Add(buttons);
        layout.Children.Insert(0, AccountingUi.Heading(editor.Title));
        editor.Content = AccountingUi.Card(layout, 0);
        if (editor.ShowDialog() != true) return;
        await RunAsync(async () =>
        {
            var result = await service.UpdateNotesAsync(obligationId, row.Id, row.Version, text.Text);
            if (result.IsSuccess) await LoadAsync();
            feedback.Text = SettlementsWindow.Error(result);
        });
    }

    private async Task RemoveAsync()
    {
        if (Selected is not { } row) return;
        if (!AccountingUi.Confirm(this, "Csatolmány eltávolítása",
                $"Eltávolítod a csatolmányt? A fájl a {AttachmentService.DeletedFolder} mappába kerül.", "Eltávolítás", "\uE74D", true)) return;
        await RunAsync(async () =>
        {
            var result = await Task.Run(() => service.RemoveAsync(obligationId, row.Id, row.Version));
            if (result.IsSuccess)
            {
                HasChanges = true;
                await LoadAsync();
            }
            feedback.Text = SettlementsWindow.Error(result);
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (busy) return;
        busy = true;
        ((UIElement)Content).IsEnabled = false;
        try { await action(); }
        finally { busy = false; ((UIElement)Content).IsEnabled = true; UpdateButtons(); }
    }
}
