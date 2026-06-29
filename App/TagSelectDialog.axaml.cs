using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace csvplot;

public partial class TagSelectDialog : Window
{
    private readonly List<string> _candidates = new();
    private readonly HashSet<string> _chosen = new(StringComparer.Ordinal);
    private bool _allowCustom;

    public TagSelectDialog()
    {
        InitializeComponent();
    }

    public TagSelectDialog(IReadOnlyList<string> candidateTags, bool allowCustom, string prompt, string? note = null)
        : this()
    {
        _candidates = candidateTags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _allowCustom = allowCustom;

        PromptTextBlock.Text = prompt;
        NoteTextBlock.Text = note ?? "";
        NoteTextBlock.IsVisible = !string.IsNullOrWhiteSpace(note);

        Opened += (_, _) => SearchTextBox.Focus();
        SearchTextBox.KeyDown += SearchTextBox_OnKeyDown;
        KeyDown += TagSelectDialog_OnKeyDown;

        UpdateResults();
        UpdateChosen();
    }

    private List<string> GetFilteredResults()
    {
        string filter = SearchTextBox.Text?.Trim() ?? "";

        IEnumerable<string> results = _candidates.Where(tag => !_chosen.Contains(tag));
        if (!string.IsNullOrWhiteSpace(filter))
        {
            results = results.Where(tag => tag.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        return results.ToList();
    }

    private void UpdateResults()
    {
        List<string> results = GetFilteredResults();
        ResultsListBox.ItemsSource = results;

        bool hasFilter = !string.IsNullOrWhiteSpace(SearchTextBox.Text);
        ResultsListBox.SelectedIndex = hasFilter && results.Count > 0 ? 0 : -1;
    }

    private void UpdateChosen()
    {
        ChosenPanel.Children.Clear();
        foreach (string tag in _chosen.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase))
        {
            Button button = new()
            {
                Content = $"{tag}  ✕",
                Tag = tag,
                Margin = new Thickness(0, 0, 4, 4)
            };
            button.Click += ChosenTag_OnClick;
            ChosenPanel.Children.Add(button);
        }

        ChosenCountTextBlock.Text = _chosen.Count == 0
            ? "Nothing selected yet."
            : $"Selected ({_chosen.Count}) — click a tag to remove it:";
    }

    private void ChosenTag_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        _chosen.Remove(tag);
        UpdateResults();
        UpdateChosen();
        SearchTextBox.Focus();
    }

    private void AddTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return;

        _chosen.Add(tag);
        SearchTextBox.Text = "";
        UpdateResults();
        UpdateChosen();
        SearchTextBox.Focus();
    }

    private static bool IsValidCustomTag(string? tag)
    {
        return !string.IsNullOrWhiteSpace(tag) && !tag.Any(char.IsWhiteSpace);
    }

    private void CommitCurrentSearch()
    {
        if (ResultsListBox.SelectedItem is string selected)
        {
            AddTag(selected);
            return;
        }

        string filter = SearchTextBox.Text?.Trim() ?? "";
        if (_allowCustom && IsValidCustomTag(filter) && !_chosen.Contains(filter))
        {
            AddTag(filter);
        }
    }

    private void MoveSelection(int delta)
    {
        int count = ResultsListBox.ItemCount;
        if (count == 0) return;

        int index = ResultsListBox.SelectedIndex;
        if (index < 0)
        {
            index = delta > 0 ? -1 : count;
        }

        ResultsListBox.SelectedIndex = Math.Clamp(index + delta, 0, count - 1);
    }

    private void SearchTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateResults();
    }

    private void SearchTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                return;
            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                return;
            case Key.Enter when e.KeyModifiers == KeyModifiers.None:
                e.Handled = true;
                CommitCurrentSearch();
                return;
        }
    }

    private void ResultsListBox_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ResultsListBox.SelectedItem is string selected)
        {
            AddTag(selected);
        }
    }

    private void CloseWithChosen()
    {
        Close(_chosen
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    private void OkButton_OnClick(object? sender, RoutedEventArgs e) => CloseWithChosen();

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e) => Close();

    private void TagSelectDialog_OnKeyDown(object? sender, KeyEventArgs e)
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
            CloseWithChosen();
        }
    }
}
