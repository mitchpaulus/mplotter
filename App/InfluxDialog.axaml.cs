using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
        KeyDown += InfluxDialog_OnKeyDown;
        BucketSearchTextBox.KeyDown += BucketSearchTextBox_OnKeyDown;
    }

    public void SetBuckets(IEnumerable<string> buckets)
    {
        _allBuckets = buckets.ToList();
        UpdateBucketList();
    }

    private List<string> GetFilteredBuckets()
    {
        var filter = BucketSearchTextBox.Text?.Trim();
        IEnumerable<string> filteredBuckets = _allBuckets;

        if (!string.IsNullOrWhiteSpace(filter))
        {
            filteredBuckets = filteredBuckets.Where(bucket =>
                bucket.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        return filteredBuckets.ToList();
    }

    private void UpdateBucketList()
    {
        InfluxBucketListBox.ItemsSource = GetFilteredBuckets();
    }

    private void AcceptBucket(string bucket)
    {
        SelectedItem = bucket;
        Close(SelectedItem);
    }

    private void InfluxBucketListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var listBox = sender as ListBox;
        if (listBox?.SelectedItem != null)
        {
            AcceptBucket(listBox.SelectedItem.ToString()!);
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

    private void BucketSearchTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        var filteredBuckets = GetFilteredBuckets();
        if (filteredBuckets.Count != 1) return;

        e.Handled = true;
        AcceptBucket(filteredBuckets[0]);
    }

    private void InfluxDialog_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        e.Handled = true;
        Close();
    }
}
