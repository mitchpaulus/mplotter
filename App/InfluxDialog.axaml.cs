using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace csvplot;

public partial class InfluxDialog : Window
{
    public string? SelectedItem { get; set; }
    public InfluxDialog()
    {
        InitializeComponent();
    }

    private void InfluxBucketListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var listBox = sender as ListBox;
        if (listBox?.SelectedItem != null)
        {
            SelectedItem = listBox.SelectedItem.ToString();
            // You can close the dialog after selection if required
            // this.Close();
            Close(SelectedItem);
        }
    }

}
