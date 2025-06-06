using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
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
    }

    public TrendDialog(List<IDataSource> sources, SelectionMode selectionMode)
    {
        InitializeComponent();
        InitSources(sources);
        TrendsListBox.SelectionMode = selectionMode;
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
                TextBlock b = new TextBlock()
                {
                    Text = trend.Name,
                    Tag = new PlotTrendConfig(source, trend)
                };

                blockList.Add(b);
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
                 if (!trend.Name.ToLowerInvariant().Contains(TrendSearchBox.Text ?? "".ToLowerInvariant())) continue;

                 TextBlock b = new TextBlock()
                 {
                     Text = trend.Name,
                     Tag = new PlotTrendConfig(source, trend)
                 };

                 blockList.Add(b);
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
}
