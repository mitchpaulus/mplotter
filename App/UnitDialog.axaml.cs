using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;

namespace csvplot;

public partial class UnitDialog : Window
{
    private const string AutomaticLabel = "(Automatic)";

    private List<string> _allUnits = new();
    private string? _currentUnit;
    private bool _suppressSelectionChanged;

    public UnitDialog()
    {
        InitializeComponent();
        Opened += UnitDialog_OnOpened;
        KeyDown += UnitDialog_OnKeyDown;
        UnitSearchTextBox.KeyDown += UnitSearchTextBox_OnKeyDown;
        UpdateUnitList();
    }

    public UnitDialog(IReadOnlyList<string> units, string? currentUnit)
        : this()
    {
        _allUnits = units.ToList();
        _currentUnit = string.IsNullOrWhiteSpace(currentUnit) ? "" : currentUnit;
        UpdateUnitList();
    }

    public UnitDialog(IReadOnlyList<string> units, string? currentUnit, string? note)
        : this(units, currentUnit)
    {
        NoteTextBlock.Text = note ?? "";
        NoteTextBlock.IsVisible = !string.IsNullOrWhiteSpace(note);
    }

    private List<string> GetFilteredUnits()
    {
        string? filter = UnitSearchTextBox.Text?.Trim();

        IEnumerable<string> filteredUnits = _allUnits;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            filteredUnits = filteredUnits.Where(unit => GetDisplayText(unit).Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        return filteredUnits.ToList();
    }

    private static string GetDisplayText(string unit)
    {
        return string.IsNullOrEmpty(unit) ? AutomaticLabel : unit;
    }

    private void UpdateUnitList()
    {
        List<string> filteredUnits = GetFilteredUnits();
        UnitListBox.ItemsSource = filteredUnits.Select(GetDisplayText).ToList();

        string currentDisplay = GetDisplayText(_currentUnit ?? "");
        if (UnitListBox.ItemsSource is not IEnumerable<string> currentItems) return;

        _suppressSelectionChanged = true;
        foreach (string item in currentItems)
        {
            if (!string.Equals(item, currentDisplay, StringComparison.Ordinal)) continue;
            UnitListBox.SelectedItem = item;
            _suppressSelectionChanged = false;
            break;
        }

        _suppressSelectionChanged = false;
    }

    private void AcceptUnit(string displayText)
    {
        Close(displayText == AutomaticLabel ? "" : displayText);
    }

    private void UnitListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChanged) return;
        if (sender is not ListBox { SelectedItem: string selectedItem }) return;
        AcceptUnit(selectedItem);
    }

    private void UnitSearchTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateUnitList();
    }

    private void UnitDialog_OnOpened(object? sender, EventArgs e)
    {
        UnitSearchTextBox.Focus();
    }

    private void UnitSearchTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        List<string> filteredUnits = GetFilteredUnits();
        if (filteredUnits.Count != 1) return;

        e.Handled = true;
        AcceptUnit(GetDisplayText(filteredUnits[0]));
    }

    private void UnitDialog_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        e.Handled = true;
        Close();
    }
}
