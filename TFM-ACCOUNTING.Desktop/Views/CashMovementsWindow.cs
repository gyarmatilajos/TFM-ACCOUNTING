using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TFM_ACCOUNTING.Business.Obligations;

namespace TFM_ACCOUNTING.Desktop.Views;

internal sealed class CashMovementsWindow : AccountingDialog
{
    private readonly ObligationService service;
    private readonly DatePicker from = new(), to = new();
    private readonly TextBlock paid = new() { FontSize = 21, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock refunded = new() { FontSize = 21, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock feedback = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12), Visibility = Visibility.Collapsed };
    private readonly DataGrid rows = new() { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false };
    private bool busy;

    public CashMovementsWindow(ObligationService service)
    {
        this.service = service;
        Title = "Tényleges pénzmozgások";
        Width = 1020;
        Height = 570;
        MinWidth = 760;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(20) };
        var header = new StackPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        header.Children.Add(AccountingUi.Heading(Title));
        var filters = new StackPanel { Orientation = Orientation.Horizontal };
        var today = DateTime.Today;
        from.SelectedDate = new DateTime(today.Year, today.Month, 1);
        to.SelectedDate = today;
        AddDateFilter(filters, "Rendezés dátumától", from);
        AddDateFilter(filters, "Rendezés dátumáig", to);
        var refresh = AccountingUi.Action("Lekérés", "\uE721");
        refresh.VerticalAlignment = VerticalAlignment.Bottom;
        refresh.Click += async (_, _) => await LoadAsync();
        filters.Children.Add(refresh);
        header.Children.Add(filters);
        var totals = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new Thickness(0, 12, 0, 12) };
        var paidCard = TotalCard("Kifizetve", paid);
        paidCard.Margin = new Thickness(0, 0, 6, 0);
        totals.Children.Add(paidCard);
        var refundedCard = TotalCard("Visszakapva", refunded);
        refundedCard.Margin = new Thickness(6, 0, 0, 0);
        totals.Children.Add(refundedCard);
        header.Children.Add(totals);
        header.Children.Add(feedback);

        var close = AccountingUi.Close("Bezárás");
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Margin = new Thickness(0, 12, 0, 0);
        DockPanel.SetDock(close, Dock.Bottom);
        root.Children.Add(close);
        root.Children.Add(AccountingUi.Card(rows, 0));
        Content = root;
        AddColumn("Dátum", nameof(CashMovementRow.Date), 100, "yyyy.MM.dd.");
        AddColumn("Saját cég", nameof(CashMovementRow.Company), 120);
        AddColumn("Partner", nameof(CashMovementRow.Partner), 150);
        AddColumn("Tétel", nameof(CashMovementRow.Item), new DataGridLength(1, DataGridLengthUnitType.Star));
        AddColumn("Összeg (Ft)", nameof(CashMovementRow.Amount), 110, "N2");
        AddColumn("Fizetési mód", nameof(CashMovementRow.Method), 120);
        AddColumn("Hivatkozás", nameof(CashMovementRow.Reference), 120);
        Loaded += async (_, _) => await LoadAsync();
    }

    private static void AddDateFilter(Panel parent, string label, DatePicker picker)
    {
        var field = new StackPanel { Width = 170, Margin = new Thickness(0, 0, 12, 0) };
        ObligationEditor.Field(field, label, picker);
        parent.Children.Add(field);
    }

    private static Border TotalCard(string label, TextBlock value)
    {
        var content = new StackPanel();
        content.Children.Add(new TextBlock { Text = label });
        content.Children.Add(value);
        return AccountingUi.Card(content, 10);
    }

    private void AddColumn(string header, string property, DataGridLength width, string? format = null) =>
        rows.Columns.Add(new DataGridTextColumn
        {
            Header = header, Width = width,
            Binding = new Binding(property) { Mode = BindingMode.OneWay, StringFormat = format }
        });

    private async Task LoadAsync()
    {
        if (busy) return;
        busy = true;
        try
        {
            var result = await service.CashMovementsAsync(ObligationEditor.Date(from), ObligationEditor.Date(to));
            if (!result.IsSuccess)
            {
                rows.ItemsSource = null;
                paid.Text = refunded.Text = "—";
                feedback.Text = SettlementsWindow.Error(result);
                feedback.Visibility = Visibility.Visible;
                return;
            }
            var report = result.Data!;
            rows.ItemsSource = report.Rows;
            paid.Text = $"{report.Paid:N2} Ft";
            refunded.Text = $"{report.Refunded:N2} Ft";
            feedback.Visibility = Visibility.Collapsed;
        }
        finally { busy = false; }
    }
}
