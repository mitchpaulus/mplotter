using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace csvplot;

public partial class MruDialog : Window
{
    private readonly List<MruItem> _items = new();

    public string? SelectedPath { get; private set; }

    public MruDialog(IEnumerable<string> paths)
    {
        InitializeComponent();
        LoadItems(paths);
        UpdateSelectionUi();
    }

    private void LoadItems(IEnumerable<string> paths)
    {
        _items.Clear();
        _items.AddRange(paths.Select(path => new MruItem(path)));
        MruListBox.ItemsSource = _items;
    }

    private void UpdateSelectionUi()
    {
        bool hasSelection = MruListBox.SelectedItem is MruItem;
        OpenButton.IsEnabled = hasSelection;
        RemoveButton.IsEnabled = hasSelection;
        SelectedPathTextBlock.Text = hasSelection
            ? ((MruItem)MruListBox.SelectedItem!).Path
            : "No MRU selected.";
    }

    private void OpenSelected()
    {
        if (MruListBox.SelectedItem is not MruItem item) return;
        SelectedPath = item.Path;
        Close(SelectedPath);
    }

    private void MruListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateSelectionUi();
    }

    private void MruListBox_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        OpenSelected();
    }

    private void OpenButton_OnClick(object? sender, RoutedEventArgs e)
    {
        OpenSelected();
    }

    private void RemoveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (MruListBox.SelectedItem is not MruItem item) return;

        MainWindow.RemoveMru(item.Path);
        _items.Remove(item);
        MruListBox.ItemsSource = null;
        MruListBox.ItemsSource = _items;
        UpdateSelectionUi();
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private sealed class MruItem
    {
        public string Path { get; }

        public MruItem(string path)
        {
            Path = path;
        }

        public override string ToString()
        {
            string fileName = System.IO.Path.GetFileName(Path);
            return string.IsNullOrWhiteSpace(fileName) ? Path : fileName;
        }
    }
}
