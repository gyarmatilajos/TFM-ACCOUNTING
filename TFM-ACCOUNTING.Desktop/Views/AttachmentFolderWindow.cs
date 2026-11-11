using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TFM_ACCOUNTING.Business.Attachments;

namespace TFM_ACCOUNTING.Desktop.Views;

internal sealed class AttachmentFolderWindow : AccountingDialog
{
    private readonly TextBox path = new() { MinWidth = 365 };
    private readonly TextBlock feedback = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) };
    private bool saving;

    public AttachmentFolderWindow(AttachmentFolderSettings settings)
    {
        Title = "Csatolmányok mappája";
        Width = 640;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var fields = new StackPanel { Margin = new Thickness(20) };
        Content = AccountingUi.Card(fields, 0);
        fields.Children.Add(AccountingUi.Heading(Title));

        fields.Children.Add(new TextBlock
        {
            Text = "A NAS megosztott mappája vagy a Synology Drive helyi szinkronmappája",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10)
        });
        var location = new DockPanel();
        var browse = AccountingUi.Action("Tallózás…", "\uE8B7");
        browse.Margin = new Thickness(8, 0, 0, 0);
        DockPanel.SetDock(browse, Dock.Right);
        location.Children.Add(browse);
        location.Children.Add(path);
        fields.Children.Add(location);
        fields.Children.Add(feedback);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var check = AccountingUi.Action("Ellenőrzés", "\uE73E");
        var save = AccountingUi.Action("Mentés", "\uE74E", "PrimaryButton");
        buttons.Children.Add(check);
        buttons.Children.Add(AccountingUi.Cancel());
        save.Margin = new Thickness(8, 0, 0, 0);
        buttons.Children.Add(save);
        fields.Children.Add(buttons);

        var stored = settings.Read();
        if (stored.IsSuccess) path.Text = stored.Data ?? "";
        else feedback.Text = stored.Message;
        browse.Click += (_, _) =>
        {
            var dialog = new OpenFolderDialog { Title = "Válaszd ki a csatolmányok mappáját" };
            if (Path.IsPathFullyQualified(path.Text)) dialog.InitialDirectory = path.Text;
            if (dialog.ShowDialog(this) == true) path.Text = dialog.FolderName;
        };
        check.Click += async (_, _) => await CheckFolderAsync(false);
        save.Click += async (_, _) => await CheckFolderAsync(true);
        Closing += (_, e) => { if (saving) e.Cancel = true; };

        async Task CheckFolderAsync(bool persist)
        {
            if (saving) return;
            var folder = path.Text;
            saving = true;
            fields.IsEnabled = false;
            feedback.Text = "A mappa ellenőrzése folyamatban…";
            try
            {
                var result = await Task.Run(() => persist ? settings.Save(folder) : AttachmentFolderSettings.Validate(folder));
                if (!result.IsSuccess) { feedback.Text = SettlementsWindow.Error(result); return; }
                if (!persist) { feedback.Text = "A mappa elérhető."; return; }
                saving = false;
                DialogResult = true;
            }
            finally { saving = false; fields.IsEnabled = true; }
        }
    }
}
