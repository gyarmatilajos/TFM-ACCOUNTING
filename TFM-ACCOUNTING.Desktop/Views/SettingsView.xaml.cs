using System.Windows;
using System.Windows.Controls;
using TFM_ACCOUNTING.Business.Attachments;

namespace TFM_ACCOUNTING.Desktop.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    public AttachmentFolderSettings? AttachmentFolder { get; set; }

    private void OpenAttachmentFolder(object sender, RoutedEventArgs e)
    {
        if (AttachmentFolder is not { } settings) return;
        var window = new AttachmentFolderWindow(settings) { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }
}
