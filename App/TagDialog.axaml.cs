using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;

namespace csvplot;

public partial class TagDialog : Window
{
    private readonly HashSet<string> _selectedTags = new(StringComparer.Ordinal);
    private List<string> _allTags = new();
    private bool _suppressSelectionChanged;

    public TagDialog()
    {
        InitializeComponent();
        Opened += TagDialog_OnOpened;
        KeyDown += TagDialog_OnKeyDown;
        TagSearchTextBox.KeyDown += TagSearchTextBox_OnKeyDown;
        TagListBox.SelectionMode = SelectionMode.Multiple | SelectionMode.Toggle;
        UpdateTagList();
    }

    public TagDialog(IReadOnlyList<string> tags, IReadOnlyList<string>? currentTags, string? note = null)
        : this()
    {
        _allTags = tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (string tag in currentTags ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                _selectedTags.Add(tag);
            }
        }

        NoteTextBlock.Text = note ?? "";
        NoteTextBlock.IsVisible = !string.IsNullOrWhiteSpace(note);
        UpdateTagList();
    }

    private List<string> GetVisibleTags()
    {
        string filter = TagSearchTextBox.Text?.Trim() ?? "";

        IEnumerable<string> visibleTags = _allTags.Concat(_selectedTags).Distinct(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(filter))
        {
            visibleTags = visibleTags.Where(tag => tag.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        return visibleTags
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsValidTag(string? tag)
    {
        return !string.IsNullOrWhiteSpace(tag) && !tag.Any(char.IsWhiteSpace);
    }

    private void UpdateSelectionSummary()
    {
        List<string> selectedTags = _selectedTags
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedTags.Count == 0)
        {
            SelectedTagsTextBlock.Text = "Selected: none";
            return;
        }

        SelectedTagsTextBlock.Text = $"Selected: {string.Join(", ", selectedTags)}";
    }

    private void UpdateCustomTagControls()
    {
        string filter = TagSearchTextBox.Text?.Trim() ?? "";
        bool exactMatch = _allTags.Any(tag => string.Equals(tag, filter, StringComparison.OrdinalIgnoreCase))
                          || _selectedTags.Any(tag => string.Equals(tag, filter, StringComparison.OrdinalIgnoreCase));
        bool showCustomButton = !string.IsNullOrWhiteSpace(filter) && !exactMatch && IsValidTag(filter);

        AddCustomTagButton.Content = $"Add custom tag: {filter}";
        AddCustomTagButton.IsVisible = showCustomButton;

        if (string.IsNullOrWhiteSpace(filter) || showCustomButton)
        {
            ValidationTextBlock.Text = "";
            ValidationTextBlock.IsVisible = false;
            return;
        }

        ValidationTextBlock.Text = "Custom tags cannot contain whitespace.";
        ValidationTextBlock.IsVisible = true;
    }

    private void UpdateTagList()
    {
        List<string> visibleTags = GetVisibleTags();
        TagListBox.ItemsSource = visibleTags;

        _suppressSelectionChanged = true;
        TagListBox.SelectedItems?.Clear();
        foreach (string tag in visibleTags)
        {
            if (_selectedTags.Contains(tag))
            {
                TagListBox.SelectedItems?.Add(tag);
            }
        }
        _suppressSelectionChanged = false;

        UpdateSelectionSummary();
        UpdateCustomTagControls();
    }

    private void AddCustomTag()
    {
        string customTag = TagSearchTextBox.Text?.Trim() ?? "";
        if (!IsValidTag(customTag))
        {
            UpdateCustomTagControls();
            return;
        }

        _selectedTags.Add(customTag);
        TagSearchTextBox.Text = "";
        UpdateTagList();
    }

    private void CloseWithSelection()
    {
        Close(_selectedTags
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    private void TagListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChanged) return;

        foreach (string addedTag in e.AddedItems.Cast<string>())
        {
            _selectedTags.Add(addedTag);
        }

        foreach (string removedTag in e.RemovedItems.Cast<string>())
        {
            _selectedTags.Remove(removedTag);
        }

        UpdateSelectionSummary();
    }

    private void TagSearchTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateTagList();
    }

    private void AddCustomTagButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AddCustomTag();
    }

    private void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CloseWithSelection();
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void TagDialog_OnOpened(object? sender, EventArgs e)
    {
        TagSearchTextBox.Focus();
    }

    private void TagSearchTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        string filter = TagSearchTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(filter))
        {
            e.Handled = true;
            CloseWithSelection();
            return;
        }

        string? exactVisibleTag = GetVisibleTags()
            .FirstOrDefault(tag => string.Equals(tag, filter, StringComparison.OrdinalIgnoreCase));
        if (exactVisibleTag is not null)
        {
            e.Handled = true;
            if (_selectedTags.Contains(exactVisibleTag))
            {
                _selectedTags.Remove(exactVisibleTag);
            }
            else
            {
                _selectedTags.Add(exactVisibleTag);
            }

            UpdateTagList();
            return;
        }

        if (IsValidTag(filter))
        {
            e.Handled = true;
            AddCustomTag();
        }
    }

    private void TagDialog_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
            return;
        }

        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control)
        {
            e.Handled = true;
            CloseWithSelection();
        }
    }
}
