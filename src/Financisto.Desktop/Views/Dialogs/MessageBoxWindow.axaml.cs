using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Financisto.Desktop.Views.Dialogs;

public partial class MessageBoxWindow : Window
{
    public MessageBoxWindow()
    {
        InitializeComponent();
    }

    public static MessageBoxWindow Create(string text, string caption, bool yesNoButtons)
    {
        var window = new MessageBoxWindow { Title = caption };
        window.MessageText.Text = text;

        if (yesNoButtons)
        {
            window.OkButton.IsVisible = false;
            window.YesButton.IsVisible = true;
            window.NoButton.IsVisible = true;
        }

        return window;
    }

    private void OnOkClick(object sender, RoutedEventArgs e) => Close(true);

    private void OnYesClick(object sender, RoutedEventArgs e) => Close(true);

    private void OnNoClick(object sender, RoutedEventArgs e) => Close(false);
}
