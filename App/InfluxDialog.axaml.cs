using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace csvplot;

public partial class InfluxDialog : Window
{
    private List<string> _allBuckets = new();

    public string? SelectedItem { get; set; }
    public InfluxDialog()
    {
        InitializeComponent();
        Opened += InfluxDialog_OnOpened;
    }

    public void SetBuckets(IEnumerable<string> buckets)
    {
        _allBuckets = buckets.ToList();
        UpdateBucketList();
    }

    private void UpdateBucketList()
    {
        var filter = BucketSearchTextBox.Text?.Trim();
        IEnumerable<string> filteredBuckets = _allBuckets;

        if (!string.IsNullOrWhiteSpace(filter))
        {
            filteredBuckets = filteredBuckets.Where(bucket =>
                bucket.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        InfluxBucketListBox.ItemsSource = filteredBuckets.ToList();
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

    private void BucketSearchTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateBucketList();
    }

    private void InfluxDialog_OnOpened(object? sender, EventArgs e)
    {
        BucketSearchTextBox.Focus();
    }
}
