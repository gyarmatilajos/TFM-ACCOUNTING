using System.Windows;
using System.Windows.Controls;
using TFM_ACCOUNTING.Business;
using TFM_ACCOUNTING.Desktop.ViewModels;
using TFM_ACCOUNTING.Desktop.Views;

namespace TFM_ACCOUNTING.Desktop;

public partial class MainWindow : Window
{
    private readonly OwnCompaniesViewModel? companies;
    private readonly SettingsViewModel? settings;
    private readonly PartnersViewModel? partners;
    private bool settingsLoaded;
    private bool partnersLoaded;
    private bool companiesLoaded;
    private string currentPage = "obligations";

    public MainWindow()
    {
        InitializeComponent();
        try
        {
            var services = new ApplicationServices();
            ObligationsView.Service = services.Obligations;
            ObligationsView.Attachments = services.Attachments;
            SettingsView.AttachmentFolder = services.AttachmentFolder;
            UpdateNavigation();
            companies = new OwnCompaniesViewModel(services.OwnCompanies, ConfirmDiscardChanges);
            settings = new SettingsViewModel(key => services.Settings.Create(key));
            partners = new PartnersViewModel(services.Partners, ConfirmDiscardChanges, warnings =>
            {
                var dialog = new PartnerNameMatchDialog(warnings) { Owner = this };
                dialog.ShowDialog();
                return dialog.Decision;
            });
            CompaniesView.DataContext = companies;
            PartnersView.DataContext = partners;
            SettingsView.DataContext = settings;
            DatabaseLabel.Text = services.DatabaseDescription;
            Loaded += async (_, _) => await ObligationsView.InitializeAsync();
            Closing += (_, e) => { if (!MayLeaveCurrentPage()) e.Cancel = true; };
        }
        catch (ArgumentException)
        {
            DatabaseLabel.Text = "Hibás adatbázis-kapcsolat. Ellenőrizd a TFM_ACCOUNTING_CONNECTION_STRING beállítást.";
            ObligationsView.IsEnabled = CompaniesView.IsEnabled = PartnersView.IsEnabled = SettingsView.IsEnabled = false;
        }
    }

    private bool ConfirmDiscardChanges() => AccountingUi.Confirm(this, "Nem mentett adatok",
        "A nem mentett módosítások elvesznek. Folytatod?", "Folytatás", "\uE72A", true);

    private async void ShowObligations(object sender, RoutedEventArgs e)
    {
        if (currentPage == "obligations" || !MayLeaveCurrentPage()) return;
        currentPage = "obligations";
        UpdateNavigation();
        CompaniesView.Visibility = PartnersView.Visibility = SettingsView.Visibility = Visibility.Collapsed;
        ObligationsView.Visibility = Visibility.Visible;
        await ObligationsView.InitializeAsync();
    }

    private async void ShowCompanies(object sender, RoutedEventArgs e)
    {
        if (currentPage == "companies" || !MayLeaveCurrentPage()) return;
        currentPage = "companies";
        UpdateNavigation();
        CompaniesView.Visibility = Visibility.Visible;
        ObligationsView.Visibility = Visibility.Collapsed;
        PartnersView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        if (!companiesLoaded && companies is not null)
        {
            await companies.InitializeAsync();
            companiesLoaded = true;
        }
    }
    private async void ShowSettings(object sender, RoutedEventArgs e)
    {
        if (currentPage == "settings" || !MayLeaveCurrentPage()) return;
        currentPage = "settings";
        UpdateNavigation();
        ObligationsView.Visibility = Visibility.Collapsed;
        CompaniesView.Visibility = Visibility.Collapsed;
        PartnersView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Visible;
        if (!settingsLoaded && settings is not null)
        {
            await settings.InitializeAsync();
            settingsLoaded = true;
        }
    }

    private async void ShowPartners(object sender, RoutedEventArgs e)
    {
        if (currentPage == "partners" || !MayLeaveCurrentPage()) return;
        currentPage = "partners";
        UpdateNavigation();
        ObligationsView.Visibility = Visibility.Collapsed;
        CompaniesView.Visibility = SettingsView.Visibility = Visibility.Collapsed;
        PartnersView.Visibility = Visibility.Visible;
        if (!partnersLoaded && partners is not null)
        {
            await partners.InitializeAsync();
            partnersLoaded = true;
        }
        else if (partners is not null) await partners.ReloadTypesAsync();
    }

    private bool MayLeaveCurrentPage() => currentPage switch
    {
        "obligations" => !ObligationsView.IsBusy,
        "companies" => companies?.MayLeave() ?? true,
        "partners" => partners?.MayLeave() ?? true,
        _ => settings?.IsBusy != true
    };

    private void UpdateNavigation()
    {
        static void Highlight(Button button, bool selected) =>
            button.SetValue(System.Windows.Controls.Primitives.Selector.IsSelectedProperty, selected);
        Highlight(CompaniesButton, currentPage == "companies");
        Highlight(ObligationsButton, currentPage == "obligations");
        Highlight(PartnersButton, currentPage == "partners");
        Highlight(SettingsButton, currentPage == "settings");
    }
}
