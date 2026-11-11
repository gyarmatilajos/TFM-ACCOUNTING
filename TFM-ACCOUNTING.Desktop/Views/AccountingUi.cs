using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TFM_ACCOUNTING.Desktop.Views;

public static class AccountingUi
{
    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.RegisterAttached("Glyph", typeof(string), typeof(AccountingUi), new PropertyMetadata(""));

    public static string GetGlyph(DependencyObject element) => (string)element.GetValue(GlyphProperty);
    public static void SetGlyph(DependencyObject element, string value) => element.SetValue(GlyphProperty, value);

    internal static Button Action(string label, string glyph, string? style = null)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 0, 8, 0) };
        SetGlyph(button, glyph);
        if (style is not null) button.SetResourceReference(FrameworkElement.StyleProperty, style);
        return button;
    }

    internal static Button Cancel() => Close("Mégse");

    internal static Button Close(string label)
    {
        var button = Action(label, "\uE8BB");
        button.IsCancel = true;
        button.Margin = new Thickness(0);
        return button;
    }

    internal static TextBlock Heading(string text) => new()
    {
        Text = text, FontSize = 23, FontWeight = FontWeights.SemiBold,
        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12)
    };

    internal static Border Card(UIElement child, double padding = 16)
    {
        var card = new Border { Child = child, Padding = new Thickness(padding) };
        card.SetResourceReference(FrameworkElement.StyleProperty, "Card");
        return card;
    }

    internal static bool Confirm(Window? owner, string title, string message, string label, string glyph, bool destructive = false)
    {
        var dialog = new AccountingDialog { Title = title, Width = 540, SizeToContent = SizeToContent.Height };
        if (owner is not null) dialog.Owner = owner;
        var fields = new StackPanel();
        fields.Children.Add(Heading(title));
        fields.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 20) });
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var accept = Action(label, glyph, destructive ? "DangerButton" : "PrimaryButton");
        var cancel = Cancel();
        cancel.IsDefault = true;
        accept.Click += (_, _) => dialog.DialogResult = true;
        actions.Children.Add(cancel);
        accept.Margin = new Thickness(8, 0, 0, 0);
        actions.Children.Add(accept);
        fields.Children.Add(actions);
        dialog.Content = new Border { Child = Card(fields), Padding = new Thickness(16) };
        return dialog.ShowDialog() == true;
    }
}

internal class AccountingDialog : Window
{
    public AccountingDialog()
    {
        SetResourceReference(BackgroundProperty, "Workspace");
        SetResourceReference(ForegroundProperty, "Ink");
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 14;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
    }
}
