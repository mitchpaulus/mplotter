using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.VisualTree;
using ScottPlot.Plottables;

namespace csvplot;

public partial class TrendDialog : Window
{
    private readonly List<TextBlock> _trendsList1 = new();
    private readonly List<TextBlock> _trendsList2 = new();
    private int _currentList = 1;
    public readonly List<PlotTrendConfig> SelectedConfigs = new();

    private bool _filterUpdate = false;

    public TrendDialog()
    {
        InitializeComponent();
        InitSources(new ());
        TrendsListBox.SelectionMode = SelectionMode.Toggle | SelectionMode.Multiple;
        TrendsListBox.AddHandler(InputElement.PointerPressedEvent, TrendsListBox_OnPointerPressed, RoutingStrategies.Tunnel);
        Opened += TrendDialog_OnOpened;
    }

    public TrendDialog(List<IDataSource> sources, SelectionMode selectionMode)
    {
        InitializeComponent();
        InitSources(sources);
        TrendsListBox.SelectionMode = selectionMode;
        TrendsListBox.AddHandler(InputElement.PointerPressedEvent, TrendsListBox_OnPointerPressed, RoutingStrategies.Tunnel);
        Opened += TrendDialog_OnOpened;
    }

    private void InitSources(List<IDataSource> sources)
    {
        var blocks = sources.Select(source =>
        {
            TextBlock b = new TextBlock
            {
                Text = source.Header,
                Tag = source
            };
            return b;
        }).ToList();
        SourceListBox.ItemsSource = blocks;

        if (blocks.Count == 1)
        {
            SourceListBox.SelectedItems?.Add(blocks[0]);
        }
    }

    private void TrendsListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_filterUpdate) return;

        foreach (var added in e.AddedItems.Cast<TextBlock>())
        {
            SelectedConfigs.Add((PlotTrendConfig)added.Tag!);
        }

        foreach (var remove in e.RemovedItems.Cast<TextBlock>())
        {
            SelectedConfigs.Remove((PlotTrendConfig)remove.Tag!);
        }
    }

    private async void SourceListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox lb) return;
        var selectedItems = lb.SelectedItems;

        List<TextBlock> blockList = _currentList == 1 ? _trendsList2 : _trendsList1;
        blockList.Clear();

        foreach (var selectedSource in selectedItems!.Cast<TextBlock>())
        {
            var source = (IDataSource)selectedSource.Tag!;
            var trends = await source.Trends();

            foreach (var trend in trends)
            {
                blockList.Add(CreateTrendTextBlock(source, trend));
            }
        }

        TrendsListBox.ItemsSource = blockList;
        _currentList = 1 - _currentList;
    }

    private async Task PopulateListBox()
    {
         _filterUpdate = true;
         var selectedItems = SourceListBox.SelectedItems;

         List<TextBlock> blockList = _currentList == 1 ? _trendsList2 : _trendsList1;
         blockList.Clear();

         foreach (var selectedSource in selectedItems!.Cast<TextBlock>())
         {
             var source = (IDataSource)selectedSource.Tag!;
             var trends = await source.Trends();

             foreach (var trend in trends)
             {
                 string searchText = TrendSearchBox.Text?.ToLowerInvariant() ?? "";
                 if (!trend.DisplayLabel.ToLowerInvariant().Contains(searchText)) continue;
                 blockList.Add(CreateTrendTextBlock(source, trend));
             }
         }

         TrendsListBox.ItemsSource = blockList;

         // Add back the selected Items
         foreach (var item in TrendsListBox.ItemsSource)
         {
             TextBlock b = (TextBlock)item;
             PlotTrendConfig c = (PlotTrendConfig)b.Tag!;
             if (SelectedConfigs.Any(config => config.Equals(c )))
             {
                 TrendsListBox.SelectedItems!.Add(item);
             }
         }

         _filterUpdate = false;
         _currentList = 1 - _currentList;
    }

    private async void TrendSearchBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        await PopulateListBox();
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(SelectedConfigs);
    }

    private void TrendDialog_OnOpened(object? sender, EventArgs e)
    {
        TrendSearchBox.Focus();
    }

    private TextBlock CreateTrendTextBlock(IDataSource source, Trend trend)
    {
        PlotTrendConfig config = new(source, trend);
        return TrendTextBlockFactory.Create(trend, config);
    }

    private void TrendsListBox_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ListBox listBox) return;
        if (!e.GetCurrentPoint(listBox).Properties.IsRightButtonPressed) return;

        e.Handled = true;

        PlotTrendConfig? config = GetPlotTrendConfigFromListBoxItem(e.Source);
        if (config is null) return;
        ShowTrendContextMenu(listBox, config);
    }

    private void ShowTrendContextMenu(Control placementTarget, PlotTrendConfig clickedConfig)
    {
        ContextMenu contextMenu = new()
        {
            Placement = PlacementMode.Pointer
        };

        if (clickedConfig.DataSource is IEditableTrendUnitSource)
        {
            MenuItem unitItem = new() { Header = "Edit Unit..." };
            unitItem.Click += async (_, _) =>
            {
                var (configs, skippedCount) = GetEditableTargetsForUnitEdit(clickedConfig);
                await EditTrendUnits(configs, skippedCount);
            };
            contextMenu.Items.Add(unitItem);
        }

        if (clickedConfig.DataSource is IEditableTrendTagSource)
        {
            MenuItem tagItem = new() { Header = "Edit Tags..." };
            tagItem.Click += async (_, _) =>
            {
                var (configs, skippedCount) = GetEditableTargetsForTagEdit(clickedConfig);
                await EditTrendTags(configs, skippedCount);
            };
            contextMenu.Items.Add(tagItem);
        }

        if (contextMenu.Items.Count == 0) return;
        contextMenu.Open(placementTarget);
    }

    private async Task EditTrendUnits(List<PlotTrendConfig> configs, int skippedCount)
    {
        if (!configs.Any()) return;
        if (configs[0].DataSource is not IEditableTrendUnitSource) return;

        string? currentUnit = GetInitialUnitForEdit(configs);
        string? note = skippedCount > 0
            ? $"{skippedCount} selected trend{(skippedCount == 1 ? "" : "s")} cannot have units edited and will be ignored."
            : null;
        UnitDialog dialog = new(UnitChoices.GetUnits(), currentUnit, note);
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        dialog.Width = 400;
        dialog.Height = 600;

        string? selectedUnit = await dialog.ShowDialog<string?>(this);
        if (selectedUnit is null) return;

        try
        {
            foreach (IGrouping<IEditableTrendUnitSource, PlotTrendConfig> sourceGroup in configs
                         .Where(config => config.DataSource is IEditableTrendUnitSource)
                         .GroupBy(config => (IEditableTrendUnitSource)config.DataSource))
            {
                await sourceGroup.Key.SetUnits(sourceGroup.Select(config => config.Trend), selectedUnit);
            }

            foreach (PlotTrendConfig config in configs)
            {
                config.Trend.Unit = selectedUnit ?? "";
            }
        }
        catch (Exception ex)
        {
            await ShowMessage("Failed To Save Unit", $"Could not write the configuration file.\n\n{ex.Message}");
            return;
        }

        if (Owner is MainWindow mainWindow)
        {
            await mainWindow.RefreshUiAfterDataSourceUpdate();
        }

        await RefreshSelectedConfigReferences();
        await PopulateListBox();
    }

    private async Task EditTrendTags(List<PlotTrendConfig> configs, int skippedCount)
    {
        if (!configs.Any()) return;
        if (configs[0].DataSource is not IEditableTrendTagSource) return;

        List<string> currentTags = GetInitialTagsForEdit(configs);
        string? note = skippedCount > 0
            ? $"{skippedCount} selected trend{(skippedCount == 1 ? "" : "s")} cannot have tags edited and will be ignored."
            : null;
        TagDialog dialog = new(TagChoices.GetTags(), currentTags, note);
        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        dialog.Width = 400;
        dialog.Height = 600;

        List<string>? selectedTags = await dialog.ShowDialog<List<string>?>(this);
        if (selectedTags is null) return;
        try
        {
            foreach (IGrouping<(IEditableTrendTagSource Source, string Signature), PlotTrendConfig> saveGroup in configs
                         .Where(config => config.DataSource is IEditableTrendTagSource)
                         .GroupBy(config =>
                         {
                             List<string> mergedTags = MergeTags(config.Trend.Tags, selectedTags);
                             string signature = string.Join("\u001f", mergedTags);
                             return ((IEditableTrendTagSource)config.DataSource, signature);
                         }))
            {
                List<string> mergedTags = MergeTags(saveGroup.First().Trend.Tags, selectedTags);
                IReadOnlyList<string>? tagsToSave = mergedTags.Count == 0 ? null : mergedTags;
                await saveGroup.Key.Source.SetTags(saveGroup.Select(config => config.Trend), tagsToSave);
            }

            foreach (PlotTrendConfig config in configs)
            {
                config.Trend.Tags = MergeTags(config.Trend.Tags, selectedTags);
            }
        }
        catch (Exception ex)
        {
            await ShowMessage("Failed To Save Tags", $"Could not write the configuration file.\n\n{ex.Message}");
            return;
        }

        if (Owner is MainWindow mainWindow)
        {
            await mainWindow.RefreshUiAfterDataSourceUpdate();
        }

        await RefreshSelectedConfigReferences();
        await PopulateListBox();
    }

    private (List<PlotTrendConfig> Configs, int SkippedCount) GetEditableTargetsForUnitEdit(PlotTrendConfig clickedConfig)
    {
        List<PlotTrendConfig> allSelectedConfigs = SelectedConfigs
            .Distinct()
            .ToList();

        List<PlotTrendConfig> editableSelectedConfigs = allSelectedConfigs
            .Where(config => config.DataSource is IEditableTrendUnitSource)
            .ToList()
            ;

        if (!allSelectedConfigs.Contains(clickedConfig))
        {
            return (new List<PlotTrendConfig> { clickedConfig }, 0);
        }

        int skippedCount = allSelectedConfigs.Count - editableSelectedConfigs.Count;
        return (editableSelectedConfigs, skippedCount);
    }

    private (List<PlotTrendConfig> Configs, int SkippedCount) GetEditableTargetsForTagEdit(PlotTrendConfig clickedConfig)
    {
        List<PlotTrendConfig> allSelectedConfigs = SelectedConfigs
            .Distinct()
            .ToList();

        List<PlotTrendConfig> editableSelectedConfigs = allSelectedConfigs
            .Where(config => config.DataSource is IEditableTrendTagSource)
            .ToList();

        if (!allSelectedConfigs.Contains(clickedConfig))
        {
            return (new List<PlotTrendConfig> { clickedConfig }, 0);
        }

        int skippedCount = allSelectedConfigs.Count - editableSelectedConfigs.Count;
        return (editableSelectedConfigs, skippedCount);
    }

    private static string? GetInitialUnitForEdit(IEnumerable<PlotTrendConfig> configs)
    {
        List<string> units = configs
            .Select(config => string.IsNullOrWhiteSpace(config.Trend.Unit) ? "" : config.Trend.Unit)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return units.Count == 1 ? units[0] : "";
    }

    private static List<string> GetInitialTagsForEdit(IEnumerable<PlotTrendConfig> configs)
    {
        return configs
            .SelectMany(config => config.Trend.Tags)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> MergeTags(IEnumerable<string> existingTags, IEnumerable<string> addedTags)
    {
        return existingTags
            .Concat(addedTags)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task ShowMessage(string title, string message)
    {
        MessageDialog dialog = new(title, message)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        await dialog.ShowDialog(this);
    }

    private async Task RefreshSelectedConfigReferences()
    {
        Dictionary<(IDataSource Source, string Name), PlotTrendConfig> updatedConfigs = new();
        foreach (IDataSource source in SelectedConfigs.Select(config => config.DataSource).Distinct())
        {
            List<Trend> trends = await source.Trends();
            foreach (Trend trend in trends)
            {
                updatedConfigs[(source, trend.Name)] = new PlotTrendConfig(source, trend);
            }
        }

        for (int i = 0; i < SelectedConfigs.Count; i++)
        {
            PlotTrendConfig selected = SelectedConfigs[i];
            if (updatedConfigs.TryGetValue((selected.DataSource, selected.Trend.Name), out PlotTrendConfig? replacement))
            {
                SelectedConfigs[i] = replacement;
            }
        }
    }

    private static PlotTrendConfig? GetPlotTrendConfigFromListBoxItem(object? source)
    {
        if (source is not Visual visual) return null;

        ListBoxItem? listBoxItem = visual.FindAncestorOfType<ListBoxItem>(true);
        if (listBoxItem is null) return null;

        if (listBoxItem.Content is PlotTrendConfig contentConfig)
        {
            return contentConfig;
        }

        if (listBoxItem.Content is TextBlock textBlock && textBlock.Tag is PlotTrendConfig taggedConfig)
        {
            return taggedConfig;
        }

        if (listBoxItem.DataContext is PlotTrendConfig dataContextConfig)
        {
            return dataContextConfig;
        }

        if (listBoxItem.DataContext is TextBlock { Tag: PlotTrendConfig textBlockDataContextConfig })
        {
            return textBlockDataContextConfig;
        }

        return null;
    }
}
