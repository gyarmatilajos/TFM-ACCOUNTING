using System.Windows;
using TFM_ACCOUNTING.Business.Common;
using TFM_ACCOUNTING.Desktop.ViewModels;

namespace TFM_ACCOUNTING.Desktop.Views;

public partial class PartnerNameMatchDialog : Window
{
    public PartnerNameMatchDialog(IReadOnlyList<ResponseWarning> warnings)
    {
        InitializeComponent();
        var matches = warnings.SelectMany(x => x.RelatedRecords).DistinctBy(x => x.Id).ToArray();
        MatchesGrid.ItemsSource = matches;
        MatchesGrid.SelectedItem = matches.FirstOrDefault();
    }
    public PartnerNameDecision Decision { get; private set; }
    private void OpenExisting(object sender, RoutedEventArgs e)
    {
        if (MatchesGrid.SelectedItem is not RelatedRecord selected) return;
        Decision = new(false, selected.Id);
        DialogResult = true;
    }
    private void SaveAnyway(object sender, RoutedEventArgs e)
    {
        Decision = new(true);
        DialogResult = true;
    }
}
