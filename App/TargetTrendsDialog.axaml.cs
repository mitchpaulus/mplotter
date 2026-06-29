using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace csvplot;

public partial class TargetTrendsDialog : Window
{
    private readonly List<CheckBox> _checkBoxes = new();

    public TargetTrendsDialog()
    {
        InitializeComponent();
    }

    public TargetTrendsDialog(IReadOnlyList<PlotTrendConfig> candidates, string? note = null)
        : this()
    {
        HeaderTextBlock.Text = string.IsNullOrWhiteSpace(note)
            ? "Apply to these trends:"
            : note;

        bool includeSourcePrefix = candidates
            .Select(config => config.DataSource)
            .Distinct()
            .Count() > 1;

        foreach (PlotTrendConfig config in candidates)
        {
            string? prefix = includeSourcePrefix ? $"{config.DataSource.ShortName}: " : null;
            CheckBox checkBox = new()
            {
                Content = TrendTextBlockFactory.Create(config.Trend, config, prefix),
                Tag = config,
                IsChecked = true
            };

            _checkBoxes.Add(checkBox);
            TrendPanel.Children.Add(checkBox);
        }
    }

    private void SetAll(bool isChecked)
    {
        foreach (CheckBox checkBox in _checkBoxes)
        {
            checkBox.IsChecked = isChecked;
        }
    }

    private void SelectAllButton_OnClick(object? sender, RoutedEventArgs e) => SetAll(true);

    private void SelectNoneButton_OnClick(object? sender, RoutedEventArgs e) => SetAll(false);

    private void OkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        List<PlotTrendConfig> selected = _checkBoxes
            .Where(checkBox => checkBox.IsChecked == true)
            .Select(checkBox => (PlotTrendConfig)checkBox.Tag!)
            .ToList();

        Close(selected);
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
