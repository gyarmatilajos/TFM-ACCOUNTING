using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TFM_ACCOUNTING.Business.Obligations;

namespace TFM_ACCOUNTING.Desktop.Views;

internal sealed class ObligationEditor : Window
{
    private readonly ObligationService service;
    private readonly ObligationDetails? details;
    private readonly ComboBox company = new(), partner = new(), type = new(), category = new(),
        payment = new(), invoiceType = new(), original = new();
    private readonly TextBox description = new() { MaxLength = 200 }, notes = new() { MaxLength = 2000, AcceptsReturn = true, Height = 65 },
        amount = new(), year = new() { MaxLength = 4 }, month = new() { MaxLength = 2 },
        number = new() { MaxLength = 100 }, net = new(), vat = new();
    private readonly DatePicker due = new(), issue = new(), performance = new();
    private readonly StackPanel invoicePanel = new();
    private readonly Button save = new() { Content = "Mentés", MinWidth = 110, Padding = new Thickness(12, 7, 12, 7) };
    private readonly TextBlock feedback = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
    private bool loadingOriginals;
    private bool saving;
    private int originalRequest;

    public ObligationEditor(ObligationService service, ObligationOptions options, ObligationDetails? details)
    {
        this.service = service;
        this.details = details;
        Title = details is null ? "Új tétel" : "Tétel szerkesztése";
        Width = 680;
        Height = 780;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var input = details?.Input;
        var root = new DockPanel { Margin = new Thickness(20) };
        var footer = new StackPanel();
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        footer.Children.Add(feedback);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(save);
        buttons.Children.Add(new Button { Content = "Mégse", IsCancel = true, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(8, 0, 0, 0) });
        footer.Children.Add(buttons);
        var fields = new StackPanel();
        root.Children.Add(new ScrollViewer { Content = fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;

        SetOptions(company, Available(options.Companies, input?.CompanyId), input?.CompanyId);
        SetOptions(partner, Available(options.Partners, input?.PartnerId), input?.PartnerId);
        SetOptions(type, Available(options.Types, input?.TypeId), input?.TypeId);
        SetOptions(category, Available(options.Categories, input?.CategoryId), input?.CategoryId, true);
        SetOptions(payment, Available(options.PaymentMethods, input?.PaymentMethodId), input?.PaymentMethodId, true);
        SetOptions(invoiceType, Available(options.InvoiceTypes, input?.Invoice?.TypeId), input?.Invoice?.TypeId);
        Field(fields, "Saját cég *", company);
        Field(fields, "Partner / jogosult *", partner);
        Field(fields, "Kötelezettségtípus *", type);
        Field(fields, "Költségkategória", category);
        Field(fields, "Leírás *", description);
        Field(fields, "Határidő *", due);
        Field(fields, "Fizetendő összeg (Ft) *", amount);
        Field(fields, "Vonatkozási év", year);
        Field(fields, "Vonatkozási hónap (1–12)", month);
        Field(fields, "Tervezett fizetési mód", payment);
        Field(fields, "Megjegyzés", notes);
        fields.Children.Add(invoicePanel);
        Field(invoicePanel, "Számlaszám *", number);
        Field(invoicePanel, "Számlatípus *", invoiceType);
        Field(invoicePanel, "Kiállítás dátuma *", issue);
        Field(invoicePanel, "Teljesítés dátuma *", performance);
        Field(invoicePanel, "Nettó összeg (Ft) *", net);
        Field(invoicePanel, "Áfa összege (Ft) *", vat);
        Field(invoicePanel, "Eredeti számla", original);
        if (details is not null)
        {
            var related = new List<string>();
            related.AddRange(details.Settlements.Select(x => $"{x.Date:yyyy.MM.dd.}  {x.Amount:N2} Ft  {x.Type}  {x.Method}"));
            related.AddRange(details.Attachments.Select(x => $"{x.Name} ({x.Size:N0} bájt)  {x.Notes}"));
            if (related.Count > 0)
                Field(fields, "Rendezések és csatolmányok", new ListBox { ItemsSource = related, MaxHeight = 150 });
        }

        description.Text = input?.Description ?? "";
        notes.Text = input?.Notes ?? "";
        amount.Text = input?.Amount.ToString("0.##", CultureInfo.CurrentCulture) ?? "";
        year.Text = input?.Year?.ToString() ?? "";
        month.Text = input?.Month?.ToString() ?? "";
        due.SelectedDate = input?.DueDate.ToDateTime(TimeOnly.MinValue);
        number.Text = input?.Invoice?.Number ?? "";
        issue.SelectedDate = input?.Invoice?.IssueDate.ToDateTime(TimeOnly.MinValue);
        performance.SelectedDate = input?.Invoice?.PerformanceDate.ToDateTime(TimeOnly.MinValue);
        net.Text = input?.Invoice?.Net.ToString("0.##", CultureInfo.CurrentCulture) ?? "";
        vat.Text = input?.Invoice?.Vat.ToString("0.##", CultureInfo.CurrentCulture) ?? "";

        type.SelectionChanged += async (_, _) => await LoadOriginalsAsync();
        company.SelectionChanged += async (_, _) => await LoadOriginalsAsync();
        partner.SelectionChanged += async (_, _) => await LoadOriginalsAsync();
        Loaded += async (_, _) => await LoadOriginalsAsync(input?.OriginalId);
        save.Click += async (_, _) => await SaveAsync();
        Closing += (_, e) => { if (saving) e.Cancel = true; };
        UpdateInvoiceVisibility();
    }

    public int? SavedId { get; private set; }

    private static IEnumerable<ObligationOption> Available(IEnumerable<ObligationOption> items, int? existing) =>
        items.Where(x => x.IsActive || x.Id == existing);

    internal static void Field(Panel panel, string label, FrameworkElement control)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 4, 0, 3) });
        control.Margin = new Thickness(0, 0, 0, 8);
        panel.Children.Add(control);
    }

    internal static void SetOptions(ComboBox box, IEnumerable<ObligationOption> values, int? selected, bool optional = false, string emptyLabel = "—")
    {
        var items = values.ToList();
        if (optional) items.Insert(0, new(0, emptyLabel, true));
        box.DisplayMemberPath = nameof(ObligationOption.DisplayName);
        box.SelectedValuePath = nameof(ObligationOption.Id);
        box.ItemsSource = items;
        box.SelectedValue = selected ?? (optional ? 0 : null);
    }

    internal static int? Id(ComboBox box) => box.SelectedValue is int id && id != 0 ? id : null;
    internal static DateOnly? Date(DatePicker picker) => picker.SelectedDate is DateTime date ? DateOnly.FromDateTime(date) : null;
    internal static bool Money(string text, out decimal value)
    {
        const NumberStyles style = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        return decimal.TryParse(text.Trim(), style, CultureInfo.CurrentCulture, out value) ||
               decimal.TryParse(text.Trim(), style, CultureInfo.GetCultureInfo("hu-HU"), out value) ||
               decimal.TryParse(text.Trim(), style, CultureInfo.InvariantCulture, out value);
    }

    private void UpdateInvoiceVisibility() => invoicePanel.Visibility =
        type.SelectedItem is ObligationOption { HasInvoiceDetails: true } ? Visibility.Visible : Visibility.Collapsed;

    private async Task LoadOriginalsAsync(int? selected = null)
    {
        var request = ++originalRequest;
        UpdateInvoiceVisibility();
        SetOptions(original, [], null, true);
        loadingOriginals = invoicePanel.Visibility == Visibility.Visible && Id(company).HasValue && Id(partner).HasValue;
        save.IsEnabled = !loadingOriginals && !saving;
        if (!loadingOriginals) return;
        var response = await service.OriginalsAsync(Id(company)!.Value, Id(partner)!.Value, details?.Id);
        if (request != originalRequest) return;
        loadingOriginals = false;
        save.IsEnabled = !saving;
        if (response.IsSuccess) SetOptions(original, response.Data!, selected, true);
        else feedback.Text = response.Message;
    }

    private async Task SaveAsync()
    {
        if (loadingOriginals || saving) return;
        if (!Money(amount.Text, out var value) || Date(due) is not DateOnly dueDate ||
            !OptionalInt(year.Text, out var periodYear) || !OptionalInt(month.Text, out var periodMonth))
        {
            feedback.Text = "Ellenőrizd az összeget, a határidőt és a vonatkozási időszakot.";
            return;
        }
        InvoiceInput? invoice = null;
        if (invoicePanel.Visibility == Visibility.Visible)
        {
            if (!Money(net.Text, out var netAmount) || !Money(vat.Text, out var vatAmount) ||
                Date(issue) is not DateOnly issued || Date(performance) is not DateOnly performed)
            {
                feedback.Text = "Add meg a nettó és az áfa összegét, a kiállítás és a teljesítés dátumát.";
                return;
            }
            invoice = new(number.Text, Id(invoiceType) ?? 0, issued, performed, netAmount, vatAmount, details?.Input.Invoice?.Version);
        }

        var input = new ObligationInput(Id(company) ?? 0, Id(partner) ?? 0, Id(type) ?? 0,
            Id(category), description.Text, periodYear, periodMonth, dueDate, value, Id(payment),
            invoice is null ? null : Id(original), notes.Text, invoice, details?.Input.Version);
        saving = true;
        ((UIElement)Content).IsEnabled = false;
        var result = await service.SaveAsync(details?.Id, input);
        saving = false;
        ((UIElement)Content).IsEnabled = true;
        if (result.IsSuccess)
        {
            SavedId = result.Data;
            DialogResult = true;
        }
        else feedback.Text = result.ValidationErrors.Count > 0
            ? string.Join(Environment.NewLine, result.ValidationErrors.Select(x => x.Message)) : result.Message;
    }

    private static bool OptionalInt(string text, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!int.TryParse(text, out var parsed)) return false;
        value = parsed;
        return true;
    }
}
