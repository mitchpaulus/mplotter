using Avalonia.Controls;
using Avalonia.Interactivity;

namespace csvplot;

public partial class MessageDialog : Window
{
    public MessageDialog()
    {
        InitializeComponent();
    }

    public MessageDialog(string title, string message)
        : this()
    {
        Title = title;
        MessageTextBlock.Text = message;
    }

    private void OkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
