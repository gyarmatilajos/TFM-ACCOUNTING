using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TFM_ACCOUNTING.Business.Attachments;
using TFM_ACCOUNTING.Business.Obligations;

namespace TFM_ACCOUNTING.Desktop.Views;

internal sealed class ObligationEditor : AccountingDialog
{
    private readonly ObligationService service;
    private readonly AttachmentService? attachments;
    private readonly ObligationDetails? details;
    private readonly ObservableCollection<PendingAttachment> pendingFiles = [];
    private sealed record PendingAttachment(string Path)
    {
        public string Name => System.IO.Path.GetFileName(Path);
    }
    private readonly ComboBox company = new(), partner = new(), type = new(), category = new(),
        payment = new(), invoiceType = new(), original = new();
    private readonly TextBox description = new() { MaxLength = 200 }, notes = new() { MaxLength = 2000, AcceptsReturn = true, Height = 62, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
        amount = new(),
        number = new() { MaxLength = 100 }, net = new(), vat = new();
    private readonly ComboBox year = new(), month = new();
    private readonly DatePicker due = new(), issue = new(), performance = new();
    private readonly StackPanel periodPanel = new();
    private readonly StackPanel invoicePanel = new();
    private readonly TextBlock heading;
    private readonly ColumnDefinition detailColumn;
    private readonly Border invoiceCard;
    private readonly Border periodCard;
    private readonly TextBlock invoicePeriod = new() { FontWeight = FontWeights.SemiBold, Text = "—" };
    private readonly ListBox attachmentFiles = new() { MaxHeight = 100 };
    private readonly TextBlock attachmentHint = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button save = AccountingUi.Action("Mentés", "\uE74E", "PrimaryButton");
    private readonly TextBlock feedback = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
    private bool loadingOriginals;
    private bool saving;
    private int originalRequest;

    public ObligationEditor(ObligationService service, ObligationOptions options, ObligationDetails? details, AttachmentService? attachments)
    {
        this.service = service;
        this.details = details;
        this.attachments = attachments;
        Title = details is null ? "Új tétel" : "Tétel szerkesztése";
        Width = Math.Min(1045, SystemParameters.WorkArea.Width);
        MinWidth = Math.Min(750, Width);
        Height = Math.Min(810, SystemParameters.WorkArea.Height);
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var input = details?.Input;
        var root = new DockPanel { Margin = new Thickness(22, 18, 22, 16) };
        var footer = new StackPanel();
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        footer.Children.Add(feedback);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(AccountingUi.Cancel());
        save.Margin = new Thickness(8, 0, 0, 0);
        buttons.Children.Add(save);
        footer.Children.Add(buttons);
        var form = new StackPanel();
        heading = AccountingUi.Heading(Title);
        heading.Margin = new Thickness(0);
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 16), LastChildFill = true };
        var headerIcon = new TextBlock { Text = "\uE7C3", FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        headerIcon.SetResourceReference(TextBlock.FontFamilyProperty, "IconFont");
        headerIcon.SetResourceReference(TextBlock.ForegroundProperty, "Accent");
        var iconSurface = new Border
        {
            Width = 44, Height = 44, CornerRadius = new CornerRadius(9), Child = headerIcon,
            Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Top
        };
        iconSurface.SetResourceReference(Border.BackgroundProperty, "InformationSurface");
        DockPanel.SetDock(iconSurface, Dock.Left);
        header.Children.Add(iconSurface);
        var titleBlock = new StackPanel();
        titleBlock.Children.Add(heading);
        if (details is not null)
        {
            var subtitle = new TextBlock
            {
                Text = string.Join(" · ", new[] { input?.Invoice?.Number ?? $"Tétel #{details.Id}",
                    options.Partners.FirstOrDefault(item => item.Id == input?.PartnerId)?.Name }.OfType<string>()),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0)
            };
            subtitle.SetResourceReference(TextBlock.ForegroundProperty, "MutedInk");
            titleBlock.Children.Add(subtitle);
        }
        header.Children.Add(titleBlock);
        form.Children.Add(header);
        var columns = new Grid();
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        detailColumn = new ColumnDefinition();
        columns.ColumnDefinitions.Add(detailColumn);
        var fields = new StackPanel();
        fields.Children.Add(SectionHeading("Alapadatok"));
        columns.Children.Add(AccountingUi.Card(fields));
        invoicePanel.Children.Add(SectionHeading("Számlaadatok"));
        invoiceCard = AccountingUi.Card(invoicePanel);
        invoiceCard.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(invoiceCard, 1);
        columns.Children.Add(invoiceCard);
        periodPanel.Children.Add(SectionHeading("Vonatkozási időszak"));
        periodCard = AccountingUi.Card(periodPanel);
        periodCard.Margin = new Thickness(12, 0, 0, 0);
        periodCard.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(periodCard, 1);
        columns.Children.Add(periodCard);
        form.Children.Add(columns);
        form.Children.Add(CreateAttachmentsCard());
        root.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 4) });
        Content = root;

        SetOptions(company, Available(options.Companies, input?.CompanyId), input?.CompanyId);
        SetOptions(partner, Available(options.Partners, input?.PartnerId), input?.PartnerId);
        SetOptions(type, Available(options.Types, input?.TypeId), input?.TypeId);
        SetOptions(category, Available(options.Categories, input?.CategoryId), input?.CategoryId, true);
        SetOptions(payment, Available(options.PaymentMethods, input?.PaymentMethodId), input?.PaymentMethodId, true);
        SetOptions(invoiceType, Available(options.InvoiceTypes, input?.Invoice?.TypeId), input?.Invoice?.TypeId);
        foreach (var selector in new[] { company, partner, type, category, payment, invoiceType, original, year, month })
            selector.SetResourceReference(FrameworkElement.StyleProperty, "FilterComboBox");
        foreach (var datePicker in new[] { due, issue, performance }) datePicker.MinHeight = 32;
        Field(fields, "Saját cég *", company);
        Field(fields, "Partner / jogosult *", partner);
        AddFieldPair(fields, "Típus *", type, "Költségkategória", category);
        AddFieldPair(fields, "Határidő *", due, "Fizetendő összeg (Ft) *", amount);
        AddPeriodFields(input);
        AddFieldPair(fields, "Tervezett fizetési mód", payment, "Leírás", description);
        Field(fields, "Megjegyzés", notes);
        Field(invoicePanel, "Számlaszám *", number);
        Field(invoicePanel, "Számlatípus *", invoiceType);
        AddFieldPair(invoicePanel, "Kiállítás dátuma *", issue, "Teljesítés dátuma *", performance);
        AddFieldPair(invoicePanel, "Nettó összeg (Ft) *", net, "Áfa összege (Ft) *", vat);
        Field(invoicePanel, "Eredeti számla", original);
        var periodSummary = new DockPanel();
        DockPanel.SetDock(invoicePeriod, Dock.Right);
        invoicePeriod.Margin = new Thickness(8, 0, 0, 0);
        periodSummary.Children.Add(invoicePeriod);
        periodSummary.Children.Add(new TextBlock { Text = "Időszak a teljesítés alapján", TextWrapping = TextWrapping.Wrap });
        var summaryCard = new Border
        {
            Child = periodSummary, CornerRadius = new CornerRadius(6), Padding = new Thickness(10), Margin = new Thickness(0, 8, 0, 0)
        };
        summaryCard.SetResourceReference(Border.BackgroundProperty, "InformationSurface");
        invoicePanel.Children.Add(summaryCard);
        if (details is not null)
        {
            var related = new List<string>();
            related.AddRange(details.Settlements.Select(x => $"{x.Date:yyyy.MM.dd.}  {x.Amount:N2} Ft  {x.Type}  {x.Method}"));
            if (related.Count > 0)
            {
                var settlements = new StackPanel();
                settlements.Children.Add(SectionHeading("Rendezések"));
                var settlementList = new ListBox { ItemsSource = related, MaxHeight = 125 };
                settlementList.SetResourceReference(FrameworkElement.StyleProperty, "RoundedListBox");
                settlements.Children.Add(settlementList);
                var card = AccountingUi.Card(settlements);
                card.Margin = new Thickness(0, 12, 0, 0);
                form.Children.Add(card);
            }
        }

        description.Text = input?.Description ?? "";
        notes.Text = input?.Notes ?? "";
        amount.Text = input?.Amount.ToString("0.##", CultureInfo.CurrentCulture) ?? "";
        due.SelectedDate = input?.DueDate.ToDateTime(TimeOnly.MinValue);
        number.Text = input?.Invoice?.Number ?? "";
        issue.SelectedDate = input?.Invoice?.IssueDate.ToDateTime(TimeOnly.MinValue);
        performance.SelectedDate = input?.Invoice?.PerformanceDate.ToDateTime(TimeOnly.MinValue);
        net.Text = input?.Invoice?.Net.ToString("0.##", CultureInfo.CurrentCulture) ?? "";
        vat.Text = input?.Invoice?.Vat.ToString("0.##", CultureInfo.CurrentCulture) ?? "";

        type.SelectionChanged += async (_, _) => await LoadOriginalsAsync();
        company.SelectionChanged += async (_, _) => await LoadOriginalsAsync();
        partner.SelectionChanged += async (_, _) => await LoadOriginalsAsync();
        performance.SelectedDateChanged += (_, _) => UpdateInvoicePeriod();
        Loaded += async (_, _) => await LoadOriginalsAsync(input?.OriginalId);
        save.Click += async (_, _) => await SaveAsync();
        Closing += (_, e) => { if (saving) e.Cancel = true; };
        UpdateInvoiceVisibility();
        UpdateInvoicePeriod();
    }

    public int? SavedId { get; private set; }
    public bool HasAttachmentChanges { get; private set; }

    private FrameworkElement CreateAttachmentsCard()
    {
        var section = new StackPanel();
        section.Children.Add(SectionHeading("Csatolmányok"));
        var row = new DockPanel();
        var actions = new WrapPanel { VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 0, 0, 0) };
        DockPanel.SetDock(actions, Dock.Right);
        row.Children.Add(actions);
        if (details is null)
        {
            var remove = AccountingUi.Action("Levétel", "\uE74D");
            remove.IsEnabled = false;
            remove.Visibility = Visibility.Collapsed;
            attachmentFiles.SelectionChanged += (_, _) => remove.IsEnabled = attachmentFiles.SelectedItem is PendingAttachment;
            remove.Click += (_, _) =>
            {
                if (attachmentFiles.SelectedItem is PendingAttachment selected) pendingFiles.Remove(selected);
            };
            pendingFiles.CollectionChanged += (_, _) =>
            {
                remove.Visibility = pendingFiles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                UpdateAttachmentHint(pendingFiles.Count);
            };
            actions.Children.Add(remove);
            attachmentFiles.ItemsSource = pendingFiles;
            attachmentFiles.DisplayMemberPath = nameof(PendingAttachment.Name);
            UpdateAttachmentHint(pendingFiles.Count);
        }
        else
        {
            SetExistingAttachments(details.Attachments.Select(file => file.Name));
        }

        var manage = AccountingUi.Action(details is null ? "Fájlok kiválasztása" : "Csatolmányok", "\uE723");
        manage.Margin = new Thickness(0);
        manage.IsEnabled = attachments is not null;
        manage.Click += async (_, _) => await OpenAttachmentsAsync();
        actions.Children.Add(manage);
        var files = new StackPanel();
        attachmentFiles.SetResourceReference(FrameworkElement.StyleProperty, "RoundedListBox");
        attachmentHint.SetResourceReference(TextBlock.ForegroundProperty, "MutedInk");
        attachmentHint.Margin = new Thickness(0, 0, 0, 5);
        files.Children.Add(attachmentHint);
        files.Children.Add(attachmentFiles);
        row.Children.Add(files);
        section.Children.Add(row);
        var card = AccountingUi.Card(section);
        card.Margin = new Thickness(0, 12, 0, 0);
        return card;
    }

    private void SetExistingAttachments(IEnumerable<string> names)
    {
        var files = names.ToArray();
        attachmentFiles.ItemsSource = files;
        UpdateAttachmentHint(files.Length);
    }

    private void UpdateAttachmentHint(int count)
    {
        attachmentFiles.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        attachmentHint.Visibility = details is not null && count > 0 ? Visibility.Collapsed : Visibility.Visible;
        attachmentHint.Text = details is not null ? "Nincs csatolmány."
            : count > 0 ? "Mentésre vár" : "Válaszd ki a mentéskor csatolandó fájlokat.";
    }

    private async Task OpenAttachmentsAsync()
    {
        if (attachments is not { } service || saving) return;
        if (details is { } existing)
        {
            var title = existing.Input.Invoice?.Number ?? existing.Input.Description ?? $"Tétel #{existing.Id}";
            var window = new AttachmentsWindow(service, existing.Id, title) { Owner = this };
            window.ShowDialog();
            HasAttachmentChanges |= window.HasChanges;
            if (window.HasChanges)
            {
                var files = await service.ListAsync(existing.Id);
                if (files.IsSuccess) SetExistingAttachments(files.Data!.Select(file => file.OriginalFileName));
                else feedback.Text = SettlementsWindow.Error(files);
            }
            return;
        }
        foreach (var path in AttachmentsWindow.PickFiles(this))
            if (!pendingFiles.Any(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase)))
                pendingFiles.Add(new PendingAttachment(path));
    }

    private static IEnumerable<ObligationOption> Available(IEnumerable<ObligationOption> items, int? existing) =>
        items.Where(x => x.IsActive || x.Id == existing);

    internal static void Field(Panel panel, string label, FrameworkElement control)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 4, 0, 3) });
        control.Margin = new Thickness(0, 0, 0, 8);
        panel.Children.Add(control);
    }

    private static TextBlock SectionHeading(string text) => new()
    {
        Text = text, FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8)
    };

    private static void AddFieldPair(Panel panel, string leftLabel, FrameworkElement left, string rightLabel, FrameworkElement right)
    {
        var pair = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        var leftField = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
        var rightField = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
        Field(leftField, leftLabel, left);
        Field(rightField, rightLabel, right);
        pair.Children.Add(leftField);
        pair.Children.Add(rightField);
        panel.Children.Add(pair);
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

    private void AddPeriodFields(ObligationInput? input)
    {
        var today = DateTime.Today;
        var months = CultureInfo.GetCultureInfo("hu-HU").DateTimeFormat;
        var yearOptions = Enumerable.Range(1, 9999).Select(value => new ObligationOption(value, value.ToString(), true));
        var monthOptions = Enumerable.Range(1, 12).Select(value => new ObligationOption(value, months.GetMonthName(value), true));
        year.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
        VirtualizingStackPanel.SetIsVirtualizing(year, true);
        ScrollViewer.SetCanContentScroll(year, true);
        SetOptions(year, yearOptions, input is null ? today.Year : input.Year, true);
        SetOptions(month, monthOptions, input is null ? today.Month : input.Month, true);
        AddFieldPair(periodPanel, "Év", year, "Hónap", month);
    }

    private void UpdateInvoiceVisibility()
    {
        var selectedType = type.SelectedItem as ObligationOption;
        invoiceCard.Visibility = selectedType?.HasInvoiceDetails == true ? Visibility.Visible : Visibility.Collapsed;
        periodCard.Visibility = selectedType is { HasInvoiceDetails: false } ? Visibility.Visible : Visibility.Collapsed;
        detailColumn.Width = selectedType is null ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        if (details is null)
        {
            Title = selectedType is null ? "Új tétel" : $"Új tétel · {selectedType.Name}";
            heading.Text = Title;
        }
    }

    private void UpdateInvoicePeriod() => invoicePeriod.Text =
        performance.SelectedDate?.ToString("yyyy. MMMM", CultureInfo.GetCultureInfo("hu-HU")) ?? "—";

    private async Task LoadOriginalsAsync(int? selected = null)
    {
        var request = ++originalRequest;
        UpdateInvoiceVisibility();
        SetOptions(original, [], null, true);
        loadingOriginals = invoiceCard.Visibility == Visibility.Visible && Id(company).HasValue && Id(partner).HasValue;
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
        if (!Money(amount.Text, out var value) || Date(due) is not DateOnly dueDate)
        {
            feedback.Text = "Ellenőrizd az összeget és a határidőt.";
            return;
        }
        InvoiceInput? invoice = null;
        if (invoiceCard.Visibility == Visibility.Visible)
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
            Id(category), description.Text, invoice is null ? Id(year) : null, invoice is null ? Id(month) : null, dueDate, value, Id(payment),
            invoice is null ? null : Id(original), notes.Text, invoice, details?.Input.Version);
        saving = true;
        ((UIElement)Content).IsEnabled = false;
        try
        {
            var result = await service.SaveAsync(details?.Id, input);
            if (!result.IsSuccess)
            {
                feedback.Text = SettlementsWindow.Error(result);
                if (pendingFiles.Count > 0)
                    feedback.Text += "\nA kiválasztott fájlok a listán maradtak.";
                return;
            }
            SavedId = result.Data;
            if (details is null && attachments is { } attachmentService && pendingFiles.Count > 0)
                await UploadPendingFilesAsync(attachmentService, result.Data);
        }
        finally
        {
            saving = false;
            ((UIElement)Content).IsEnabled = true;
        }
        DialogResult = true;
    }

    private async Task UploadPendingFilesAsync(AttachmentService attachmentService, int obligationId)
    {
        feedback.Text = "A tételt mentettük. A csatolmányok mentése folyamatban…";
        var results = new StackPanel();
        var saved = 0;
        foreach (var file in pendingFiles)
        {
            var result = await Task.Run(() => attachmentService.UploadAsync(obligationId, file.Path));
            if (result.IsSuccess) saved++;
            var fileResult = new StackPanel();
            fileResult.Children.Add(new TextBlock { Text = file.Name, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.SemiBold });
            fileResult.Children.Add(new TextBlock
            {
                Text = result.IsSuccess ? "Mentve a beállított mappába" : SettlementsWindow.Error(result),
                TextWrapping = TextWrapping.Wrap
            });
            var card = AccountingUi.Card(fileResult, 10);
            card.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, result.IsSuccess ? "SuccessSurface" : "WarningSurface");
            card.Margin = new Thickness(0, 0, 0, 8);
            results.Children.Add(card);
        }
        HasAttachmentChanges = saved > 0;
        var summary = $"A tételt mentettük. {saved}/{pendingFiles.Count} fájlt csatoltunk.";
        if (saved < pendingFiles.Count)
            summary += "\nA sikertelen fájlokat a mentett tétel Csatolmányok ablakában próbálhatod újra.";
        var report = new AccountingDialog
        {
            Owner = this, Title = "Mentés eredménye", Width = 600, Height = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var layout = new DockPanel { Margin = new Thickness(18) };
        var close = AccountingUi.Action("Rendben", "\uE73E", "PrimaryButton");
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.IsDefault = close.IsCancel = true;
        close.Click += (_, _) => report.Close();
        DockPanel.SetDock(close, Dock.Bottom);
        layout.Children.Add(close);
        var heading = AccountingUi.Heading(report.Title);
        DockPanel.SetDock(heading, Dock.Top);
        layout.Children.Add(heading);
        var summaryText = new TextBlock { Text = summary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
        DockPanel.SetDock(summaryText, Dock.Top);
        layout.Children.Add(summaryText);
        layout.Children.Add(new ScrollViewer { Content = results, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 12) });
        report.Content = AccountingUi.Card(layout, 0);
        report.ShowDialog();
    }
}
