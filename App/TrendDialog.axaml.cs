using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ScottPlot.Plottables;

namespace csvplot;

public partial class TrendDialog : Window
{
    private readonly List<TextBlock> _trendsList1 = new();
    private readonly List<TextBlock> _trendsList2 = new();
    private int _currentList = 1;
    private readonly List<PlotTrendConfig> _selectedConfigs = new();

    private bool _filterUpdate = false;

    public TrendDialog(List<IDataSource> sources)
    {
        InitializeComponent();
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
            _selectedConfigs.Add((PlotTrendConfig)added.Tag!);
        }

        foreach (var remove in e.RemovedItems.Cast<TextBlock>())
        {
            _selectedConfigs.Remove((PlotTrendConfig)remove.Tag!);
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
                    Text = trend,
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
         var selectedItems = SourceListBox.SelectedItems;

         List<TextBlock> blockList = _currentList == 1 ? _trendsList2 : _trendsList1;
         blockList.Clear();

         foreach (var selectedSource in selectedItems!.Cast<TextBlock>())
         {
             var source = (IDataSource)selectedSource.Tag!;
             var trends = await source.Trends();

             foreach (var trend in trends)
             {
                 if (!trend.ToLowerInvariant().Contains(TrendSearchBox.Text ?? "".ToLowerInvariant())) continue;

                 TextBlock b = new TextBlock()
                 {
                     Text = trend,
                     Tag = new PlotTrendConfig(source, trend)
                 };

                 blockList.Add(b);
             }
         }

         _filterUpdate = true;
         TrendsListBox.ItemsSource = blockList;

         // Add back the selected Items
         foreach (var item in TrendsListBox.ItemsSource)
         {
             TextBlock b = (TextBlock)item;
             PlotTrendConfig c = (PlotTrendConfig)b.Tag!;
             if (_selectedConfigs.Any(config => config.Equals(c )))
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
}
