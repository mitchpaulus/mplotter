using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Domain;
using OfficeOpenXml;
using ScottPlot;
using ScottPlot.Avalonia;
using Brushes = Avalonia.Media.Brushes;
using File = System.IO.File;

namespace csvplot;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public readonly List<XySerie> XySeries = new();

    private readonly List<IDataSource> _loadedDataSources = new();

    private readonly List<IDataSource> _selectedDataSources = new();

    public readonly List<PlotTrendConfig> SelectedTimeSeriesTrends = new();
    private readonly List<PlotTrendConfig> _availableTimeSeriesTrends = new();

    private readonly List<TextBlock> _timeSeriesTextBlocks1 = new();
    private readonly List<TextBlock> _timeSeriesTextBlocks2 = new();
    private int _currentTimeSeriesTextBlocks = 1;
    private bool _currentlyFiltering = false;

    private readonly ListBox _timeSeriesTrendsListBox = new()
    {
        SelectionMode = SelectionMode.Multiple | SelectionMode.Toggle,
    };

    private readonly Grid _xyTrendSelectionGrid = new();

    public PlotMode Mode;

    public MainWindow()
    {
        InitializeComponent();

        AvaPlot? plot = this.Find<AvaPlot>("AvaPlot");

        _vm = new MainViewModel(plot!, StorageProvider, this);
        DataContext = _vm;

        UpdateMrus();
        _timeSeriesTrendsListBox.SelectionChanged += TimeSeriesTrendList_OnSelectionChanged;
        Grid.SetRow(_timeSeriesTrendsListBox, 5);

        Mode = PlotMode.Ts;
        TsRadio.IsChecked = true;

        _xyTrendSelectionGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        _xyTrendSelectionGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        _xyTrendSelectionGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        _xyTrendSelectionGrid.RowDefinitions.Add(new(GridLength.Auto));
        _xyTrendSelectionGrid.RowDefinitions.Add(new(GridLength.Auto));
        Grid.SetRow(_xyTrendSelectionGrid, 5);

        Button addXySerieButton = new();
        addXySerieButton.Content = "Add Series";
        addXySerieButton.Click += AddXySerieButtonOnClick;

        Grid.SetColumn(addXySerieButton, 0);
        Grid.SetRow(addXySerieButton, 0);
        _xyTrendSelectionGrid.Children.Add(addXySerieButton);
    }

    private void AddXySerieButtonOnClick(object? sender, RoutedEventArgs e)
    {
        XySeries.Add(new XySerie(null, null));
        UpdateXyGrid();
    }

    private void UpdateXyGrid()
    {
        int numHeaderRows = 2;

        for (int i = _xyTrendSelectionGrid.Children.Count - 1 ; i > 0; i--)
        {
            int row = Grid.GetRow(_xyTrendSelectionGrid.Children[i]);
            if (row >= numHeaderRows) _xyTrendSelectionGrid.Children.RemoveAt(i);
        }

        int rowDiff = XySeries.Count - (_xyTrendSelectionGrid.RowDefinitions.Count - numHeaderRows);
        if (rowDiff > 0)
        {
            while (XySeries.Count > _xyTrendSelectionGrid.RowDefinitions.Count - numHeaderRows)
            {
                _xyTrendSelectionGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }
        }
        else if (rowDiff < 0)
        {
            while (_xyTrendSelectionGrid.RowDefinitions.Count - numHeaderRows > XySeries.Count)
            {
                _xyTrendSelectionGrid.RowDefinitions.RemoveAt(_xyTrendSelectionGrid.RowDefinitions.Count - 1);
            }
        }

        foreach (var (serie, row) in XySeries.WithIndex(numHeaderRows))
        {
            if (serie.XTrend is { } xTrend)
            {
                TextBlock b = new();
                b.Text = xTrend.TrendName;
                b.TextWrapping = TextWrapping.Wrap;
                b.Tag = serie.XTrend;
                Grid.SetRow(b, row);
                Grid.SetColumn(b, 0);
                _xyTrendSelectionGrid.Children.Add(b);
            }
            else
            {
                Button b = new();
                b.Content = "Select X";
                b.Tag = serie.XTrend;
                b.Click += BOnClick;
                Grid.SetRow(b, row);
                Grid.SetColumn(b, 0);
                _xyTrendSelectionGrid.Children.Add(b);
            }

            if (serie.YTrend is { } yTrend)
            {
                TextBlock b = new();
                b.Text = yTrend.TrendName;
                b.TextWrapping = TextWrapping.Wrap;
                b.Tag = serie.YTrend;
                Grid.SetRow(b, row);
                Grid.SetColumn(b, 1);
                _xyTrendSelectionGrid.Children.Add(b);
            }
            else
            {
                Button b = new();
                b.Content = "Select Y";
                b.Tag = serie.YTrend;
                b.Click += BOnClick;
                Grid.SetRow(b, row);
                Grid.SetColumn(b, 1);
                _xyTrendSelectionGrid.Children.Add(b);
            }

            Button removeButton = new();
            removeButton.Content = "Remove";
            removeButton.Click += RemoveXySerie;
            removeButton.Tag = serie;
            Grid.SetRow(removeButton, row);
            Grid.SetColumn(removeButton, 2);
            _xyTrendSelectionGrid.Children.Add(removeButton);
        }
    }

    private async void BOnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button b) return;

        TrendDialog dialog = new TrendDialog(_selectedDataSources, SelectionMode.Single | SelectionMode.Toggle);

        await dialog.ShowDialog(this);

        if (dialog.SelectedConfigs.Any())
        {
            var first = dialog.SelectedConfigs.First();
            foreach (var series in XySeries)
            {
                if (ReferenceEquals(series.XTrend, b.Tag))
                {
                    series.XTrend = first;
                    break;
                }

                if (ReferenceEquals(series.YTrend, b.Tag))
                {
                    series.YTrend = first;
                    break;
                }
            }
        }
        else
        {
            foreach (var series in XySeries)
            {
                if (ReferenceEquals(series.XTrend, b.Tag))
                {
                    series.XTrend = null;
                    break;
                }

                if (ReferenceEquals(series.YTrend, b.Tag))
                {
                    series.YTrend = null;
                    break;
                }
            }
        }

        UpdateXyGrid();
        _vm.UpdatePlots();
    }

    private void RemoveXySerie(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button b) return;
        if (b.Tag is not XySerie s) return;
        XySeries.RemoveAll(serie => ReferenceEquals(serie, s));
        UpdateXyGrid();
        _vm.UpdatePlots();
    }

    private void HandlePlotTypeChange()
    {
        if (XyRadio.IsChecked ?? false)
        {
            MainSourceGrid.Children.Remove(_timeSeriesTrendsListBox);
            if (!MainSourceGrid.Children.Contains(_xyTrendSelectionGrid))
            {
                MainSourceGrid.Children.Add(_xyTrendSelectionGrid);
            }
            Mode = PlotMode.Xy;

        }
        else if (TsRadio.IsChecked ?? false)
        {
            Mode = PlotMode.Ts;
            MainSourceGrid.Children.Remove(_xyTrendSelectionGrid);
            if (!MainSourceGrid.Children.Contains(_timeSeriesTrendsListBox))
            {
                MainSourceGrid.Children.Add(_timeSeriesTrendsListBox);
            }
        }
        else if (HistogramRadio.IsChecked ?? false)
        {
            Mode = PlotMode.Histogram;
            if (!MainSourceGrid.Children.Contains(_timeSeriesTrendsListBox))
            {
                MainSourceGrid.Children.Add(_timeSeriesTrendsListBox);
            }
        }

        _vm.UpdatePlots();
    }

    private void RenderDataSources()
    {
        DataSourcesList.Children.Clear();

        foreach (var dataSource in _loadedDataSources)
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Button b = new Button()
            {
                Content = dataSource.ShortName,
                Tag = dataSource,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background =  _selectedDataSources.Contains(dataSource) ? Brushes.LightBlue : Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };

            b.Click += ClickSource;

            Grid.SetColumn(b, 0);

            Button removeButton = new Button()
            {
                Content = "Remove",
                Tag = dataSource,
            };
            Grid.SetColumn(removeButton, 1);

            g.Children.Add(b);
            g.Children.Add(removeButton);

            removeButton.Click += RemoveDataSource;
            DataSourcesList.Children.Add(g);
        }
    }

    private async void ClickSource(object? sender, EventArgs args)
    {
        if (sender is not Button b) return;
        var source = (IDataSource)b.Tag!;

        var removed = _selectedDataSources.Remove(source);
        if (!removed) _selectedDataSources.Add(source);
        else
        {
            // Remove all selected trends that had that source
            SelectedTimeSeriesTrends.RemoveAll(config => config.DataSource.Equals(source));
        }

        RenderDataSources();
        await UpdateAvailableTimeSeriesTrendList();
        _vm.UpdatePlots();
    }

    public async void AddDataSourceMruClick(object? sender, EventArgs args)
    {
        if (sender is not Button b) return;
        if (b.Tag is not IDataSource source) return;
        await AddDataSource(source);
    }

    public async Task AddDataSource(IDataSource source)
    {
        // Don't add dup
        foreach (var s in _loadedDataSources)
        {
            if (s.GetType() != source.GetType()) continue;
            if (s.Header == source.Header) return;
        }

        _loadedDataSources.Add(source);
        _selectedDataSources.Add(source);
        RenderDataSources();
        await UpdateAvailableTimeSeriesTrendList();
    }

    private async Task UpdateAvailableTimeSeriesTrendList()
    {
        _availableTimeSeriesTrends.Clear();
        foreach (var s in _selectedDataSources)
        {
            var trends = await s.Trends();
            _availableTimeSeriesTrends.AddRange(trends.Select(trend => new PlotTrendConfig(s, trend)));
        }

        Dictionary<string, List<PlotTrendConfig>> grouped = _availableTimeSeriesTrends.GroupBy(config => config.TrendName).ToDictionary(configs => configs.Key, configs => configs.ToList());
        var sortedKeys = grouped.Keys.OrderBy(s => s.ToLowerInvariant());

        List<TextBlock> timeSeriesTextBlocks = _currentTimeSeriesTextBlocks == 1 ? _timeSeriesTextBlocks2 : _timeSeriesTextBlocks1;

        timeSeriesTextBlocks.Clear();

        string loweredSearchText = SearchBox.Text ?? "".ToLowerInvariant();

        foreach (var key in sortedKeys)
        {
            if (!key.ToLowerInvariant().Contains(loweredSearchText)) continue;

            List<PlotTrendConfig> trends = grouped[key];
            if (trends.Count > 1)
            {
                foreach (var t in trends)
                {
                    TextBlock b = new TextBlock();
                    b.Text = $"{t.DataSource.ShortName}: {t.TrendName}";
                    b.Tag = t;
                    timeSeriesTextBlocks.Add(b);
                }
            }
            else
            {
                var t = trends[0];
                TextBlock b = new TextBlock();
                b.Text = $"{t.TrendName}";
                b.Tag = t;
                timeSeriesTextBlocks.Add(b);
            }
        }
        _currentlyFiltering = true;

        _timeSeriesTrendsListBox.ItemsSource = timeSeriesTextBlocks;
        _currentTimeSeriesTextBlocks = 1 - _currentTimeSeriesTextBlocks;

        // Add back selected if required.
        foreach (var item in _timeSeriesTrendsListBox.ItemsSource)
        {
             TextBlock b = (TextBlock)item;
             PlotTrendConfig c = (PlotTrendConfig)b.Tag!;
             if (SelectedTimeSeriesTrends.Any(config => config.Equals(c )))
             {
                 _timeSeriesTrendsListBox.SelectedItems!.Add(item);
             }
        }

        _currentlyFiltering = false;
    }

    private async void RemoveDataSource(object? sender, EventArgs args)
    {
        if (sender is not Button b) return;

        var source = (IDataSource)b.Tag!;
        _loadedDataSources.Remove(source);
        _selectedDataSources.Remove(source);

        foreach (var c in DataSourcesList.Children)
        {
            if (c is not Grid g) continue;
            if (!g.Children.Any(control => ReferenceEquals(control, b))) continue;

            DataSourcesList.Children.Remove(c);
            break;
        }

        await UpdateAvailableTimeSeriesTrendList();
    }



    public void UpdateMrus()
    {
        List<string> mrus = MostRecentlyUsedFiles();

        var buttons = mrus.Select(mru =>
            {
                // Add a button to load that file
                Button button = new Button();
                IDataSource source = DataSourceFactory.SourceFromLocalPath(mru);
                button.Click += AddDataSourceMruClick;
                button.Content = Path.GetFileName(mru).EscapeUiText();
                button.Tag = source;

                var tooltip = new TextBlock { Text = mru };
                ToolTip.SetTip(button, tooltip);
                return button;
            }
        );

        MruPanel.Children.Clear();
        MruPanel.Children.AddRange(buttons);
    }

    public static List<string> MostRecentlyUsedFiles()
    {
        string? localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (localAppData is null) return new List<string>();

        string[] lines;
        try
        {
            lines = File.ReadAllLines($"{localAppData}/mplotter/mru.txt");
        }
        catch
        {
            return new List<string>();
        }

        return lines.Where(File.Exists).Order().ToList();
    }

    public static readonly FilePickerFileType DateFileTypes = new("Data Files")
    {
        Patterns = new[] { "*.csv", "*.tsv", "*.txt", "*.sql", "*.db" }
    };


    private void InputElement_OnKeyDown(object? sender, KeyEventArgs e)
    {
        // On '/', move focus to 'SearchBox'
        if (e.Key == Key.Oem2) // Oem2 is often used for '/'
        {
            // Check if the currently focused control is the search textbox
            if (FocusManager != null && (FocusManager.GetFocusedElement() is not TextBox currentTextBox ||
                                         currentTextBox.Name != "SearchBox"))
            {
                SearchBox.Focus();
                e.Handled = true; // Mark the event as handled to prevent further processing
            }
        }
        else if (e.Key == Key.Escape)
        {
            SearchBox.Clear();
            SearchBox.Focus();
        }
        else if (e.Key == Key.Left)
        {
            // Check that a text box is currently not focused
            if (FocusManager == null || FocusManager.GetFocusedElement() is TextBox) return;

            foreach (var child in PlotStackPanel.Children)
            {
                if (child is not AvaPlot avaPlot) continue;

                // Not a date
                if (avaPlot.Plot.Axes.Bottom.Min < new DateTime(1960, 1, 1).ToOADate()) return;

                var minOaDate = DateTime.FromOADate(avaPlot.Plot.Axes.Bottom.Min);
                var maxOaDate = DateTime.FromOADate(avaPlot.Plot.Axes.Bottom.Max);

                if (minOaDate.Day == 1 && maxOaDate.Day == 1)
                {
                    var monthDiff = (maxOaDate.Year * 12 + maxOaDate.Month) - (minOaDate.Year * 12 + minOaDate.Month);

                    if (monthDiff > 0)
                    {
                        var newMinDate = minOaDate.AddMonths(-monthDiff);
                        var newMaxDate = maxOaDate.AddMonths(-monthDiff);

                        avaPlot.Plot.Axes.SetLimitsX(newMinDate.ToOADate(), newMaxDate.ToOADate());

                        if (monthDiff == 1)
                        {
                            avaPlot.Plot.Axes.Bottom.Label.Text = MonthNames.Names[newMinDate.Month - 1];
                        }

                        avaPlot.Refresh();
                    }
                }
            }
        }
        else if (e.Key == Key.Right)
        {
            // Check that a text box is currently not focused
            if (FocusManager == null || FocusManager.GetFocusedElement() is TextBox) return;

            foreach (var child in PlotStackPanel.Children)
            {
                if (child is not AvaPlot avaPlot) continue;

                // Not a date
                if (avaPlot.Plot.Axes.Bottom.Min < new DateTime(1960, 1, 1).ToOADate()) return;

                var minOaDate = DateTime.FromOADate(avaPlot.Plot.Axes.Bottom.Min);
                var maxOaDate = DateTime.FromOADate(avaPlot.Plot.Axes.Bottom.Max);

                if (minOaDate.Day == 1 && maxOaDate.Day == 1)
                {
                    var monthDiff = (maxOaDate.Year * 12 + maxOaDate.Month) - (minOaDate.Year * 12 + minOaDate.Month);

                    if (monthDiff > 0)
                    {
                        var newMinDate = minOaDate.AddMonths(monthDiff);
                        var newMaxDate = maxOaDate.AddMonths(monthDiff);

                        avaPlot.Plot.Axes.SetLimitsX(newMinDate.ToOADate(), newMaxDate.ToOADate());

                        if (monthDiff == 1)
                        {
                            avaPlot.Plot.Axes.Bottom.Label.Text = MonthNames.Names[newMinDate.Month - 1];
                        }

                        avaPlot.Refresh();
                    }
                }
            }
        }
    }

    private void ClearSelections(object? sender, RoutedEventArgs e)
    {
        SelectedTimeSeriesTrends.Clear();
        _timeSeriesTrendsListBox.SelectedItems!.Clear();
        _vm.UpdatePlots();
    }

    private async void InfluxButtonClick(object? sender, RoutedEventArgs e)
    {
        // await _vm.AddDataSource(new InfluxDataSource("511 John Carpenter"));

        var influxDialog = new InfluxDialog();
        influxDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        influxDialog.Width = 500;
        influxDialog.Height = 1000;

        InfluxEnv env = InfluxDataSource.GetEnv();
        if (!env.IsValid()) return;

        // Get available buckets

        try
        {
            var client = new InfluxDBClient(env.InfluxHost, env.InfluxToken);
            var api = client.GetBucketsApi();

            var buckets = new List<string>();
            var offset = 0;

            while (true)
            {
                List<Bucket>? bucketResponse = await api.FindBucketsAsync(limit: 100, offset: offset);

                if (bucketResponse is null) break;

                buckets.AddRange(bucketResponse.Select(bucket => bucket.Name));
                if (bucketResponse.Count < 100) break;
                offset += 100;
            }

            influxDialog.InfluxBucketListBox.ItemsSource = buckets.Order().ToList();

            var selected = await influxDialog.ShowDialog<string>(this);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                await AddDataSource(new InfluxDataSource(selected));
            }
        }
        catch
        {
            // Empty
        }
    }

    private async void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        await UpdateAvailableTimeSeriesTrendList();
    }

    private async void ExportButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var exportType = ExportType.Tsv;
        if (CsvComboBox.IsSelected) exportType = ExportType.Csv;
        else if (XlsxComboBox.IsSelected) exportType = ExportType.Xlsx;

        string extension = exportType switch
        {
            ExportType.Tsv => ".tsv",
            ExportType.Csv => ".csv",
            ExportType.Xlsx => ".xlsx",
            _ => throw new ArgumentOutOfRangeException()
        };

        var pickerResult = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
        {
            Title = "Save data export",
            DefaultExtension = extension,
            SuggestedFileName = $"{DateTime.Now:yyyy-MM-dd HHmm} Export{extension}"
        });

        int minuteInterval = int.Parse((string)((ComboBoxItem)MinuteIntervalComboBox.SelectedItem!).Content!);

        if (pickerResult is null) return;

        if (ExportStartYearComboBox.SelectedItem is not ComboBoxItem { Content: string startYearString }) return;
        if (ExportStartMonthComboBox.SelectedItem is not ComboBoxItem { Content: string startMonthString }) return;
        if (ExportStartDayComboBox.SelectedItem is not ComboBoxItem { Content: string startDayString }) return;
        if (ExportEndYearComboBox.SelectedItem is not ComboBoxItem { Content: string endYearString }) return;
        if (ExportEndMonthComboBox.SelectedItem is not ComboBoxItem { Content: string endMonthString }) return;
        if (ExportEndDayComboBox.SelectedItem is not ComboBoxItem { Content: string endDayString }) return;

        int startYear = int.Parse(startYearString);
        int startMonth = MonthNames.ShortNames.IndexOf(startMonthString) + 1;
        int startDay = int.Parse(startDayString);

        int endYear = int.Parse(endYearString);
        int endMonth = MonthNames.ShortNames.IndexOf(endMonthString) + 1;
        int endDay = int.Parse(endDayString);

        // Truncate day to max in the month
        startDay = Math.Min(startDay, MonthNames.DaysInMonth[startMonth - 1]);
        endDay = Math.Min(endDay, MonthNames.DaysInMonth[endMonth - 1]);

        DateTime start = new(startYear, startMonth, startDay);
        DateTime end = new(endYear, endMonth, endDay);

        List<TimestampData> allData = new();
        List<string> headers = new();

        foreach (var sourcePair in SelectedTimeSeriesTrends.GroupBy(pair => pair.DataSource))
        {
            var trends = sourcePair.Select(pair => pair.TrendName).ToList();
            List<TimestampData> output = sourcePair.Key.GetTimestampData(trends, start.AddDays(-1), end.AddDays(1));

            headers.AddRange(trends);

            foreach (var tsData in output)
            {
                tsData.AlignToMinuteInterval(start, end, minuteInterval);
                allData.Add(tsData);
            }
        }

        if (!allData.Any()) return;

        try
        {
            if (exportType == ExportType.Tsv)
            {
                await using Stream stream = await pickerResult.OpenWriteAsync();
                await using StreamWriter writer = new StreamWriter(stream);

                headers.Insert(0, "Timestamp");
                var headerString = string.Join("\t", headers);
                await writer.WriteAsync(headerString);
                await writer.WriteAsync('\n');

                List<DateTime> dateTimes = allData[0].DateTimes;

                List<string> fields = new List<string>(allData.Count);
                for (int i = 0; i < dateTimes.Count; i++)
                {
                    fields.Clear();
                    fields.Add(dateTimes[i].ToString("yyyy-MM-dd HH:mm"));
                    foreach (var t in allData)
                    {
                        double value = t.Values[i];
                        string valueStr = double.IsNaN(value) ? "" : value.ToString(CultureInfo.InvariantCulture);
                        fields.Add(valueStr);
                    }

                    string tabSepLine = string.Join("\t", fields);
                    await writer.WriteAsync(tabSepLine);
                    await writer.WriteAsync('\n');
                }
            }
            else if (exportType == ExportType.Csv)
            {
                await using Stream stream = await pickerResult.OpenWriteAsync();
                await using StreamWriter writer = new StreamWriter(stream);

                headers.Insert(0, "Timestamp");
                var headerString = string.Join(",", headers.Select(s => s.ToCsvCell()));
                await writer.WriteAsync(headerString);
                await writer.WriteAsync('\n');

                List<DateTime> dateTimes = allData[0].DateTimes;

                List<string> fields = new List<string>(allData.Count);
                for (int i = 0; i < dateTimes.Count; i++)
                {
                    fields.Clear();
                    fields.Add(dateTimes[i].ToString("yyyy-MM-dd HH:mm"));
                    foreach (var t in allData)
                    {
                        double value = t.Values[i];
                        string valueStr = double.IsNaN(value) ? "" : value.ToString(CultureInfo.InvariantCulture);
                        fields.Add(valueStr);
                    }

                    string tabSepLine = string.Join(",", fields.Select(s => s.ToCsvCell()));
                    await writer.WriteAsync(tabSepLine);
                    await writer.WriteAsync('\n');
                }
            }
            else if (exportType == ExportType.Xlsx)
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                await using Stream stream = await pickerResult.OpenWriteAsync();
                using ExcelPackage package = new ExcelPackage(stream);

                var sheet = package.Workbook.Worksheets.Add($"{DateTime.Now:yyyy-MM-dd HHmm} Export");

                sheet.Cells[1, 1].Value = "Timestamp";
                foreach ((string header, int col) in headers.WithIndex(2))
                {
                    sheet.Cells[1, col].Value = header;
                }

                List<DateTime> dateTimes = allData[0].DateTimes;
                for (int i = 0; i < dateTimes.Count; i++)
                {
                    var row = i + 2;
                    sheet.Cells[row, 1].Value = dateTimes[i];

                    for (int index = 0; index < allData.Count; index++)
                    {
                        var t = allData[index];
                        double value = t.Values[i];
                        if (double.IsNaN(value)) continue;
                        sheet.Cells[row, index + 2].Value = value;
                    }
                }

                sheet.Column(1).Style.Numberformat.Format = "yyyy-MM-dd HH:mm";

                sheet.Column(1).AutoFit();
                for (int i = 0; i < allData.Count; i++)
                {
                    sheet.Column(i + 2).AutoFit();
                }

                await package.SaveAsync();
            }
        }
        catch
        {
            // Ignored
        }
    }

    private void TimeSeriesTrendList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_currentlyFiltering) return;
        foreach (var added in e.AddedItems.Cast<TextBlock>())
        {
            SelectedTimeSeriesTrends.Add((PlotTrendConfig)added.Tag!);
        }

        foreach (var remove in e.RemovedItems.Cast<TextBlock>())
        {
            SelectedTimeSeriesTrends.Remove((PlotTrendConfig)remove.Tag!);
        }
        _vm.UpdatePlots();
    }

    private async void BrowseButtonOnClick(object? sender, RoutedEventArgs e)
    {
        // Use StorageProvider to show the dialog
        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            FileTypeFilter = new[] { MainWindow.DateFileTypes }
        });

        if (result.Any())
        {
            // Get the selected file path
            IStorageFile? filePath = result[0];

            IDataSource source = DataSourceFactory.SourceFromLocalPath(filePath.Path.LocalPath);

            await AddDataSource(source);

            // Handle the file path (e.g., updating the ViewModel)
            await MainViewModel.SaveToMru(filePath.Path.LocalPath);

            UpdateMrus();
        }
    }

    private void XyRadio_OnIsCheckedChanged(object? sender, RoutedEventArgs e) => HandlePlotTypeChange();

    private void Histogram_OnIsCheckedChanged(object? sender, RoutedEventArgs e) => HandlePlotTypeChange();

    private void Ts_OnIsCheckedChanged(object? sender, RoutedEventArgs e) => HandlePlotTypeChange();

    private void NoaaButtonClick(object? sender, RoutedEventArgs e)
    {
        throw new NotImplementedException();
    }
}

public class PlotTrendConfig : IEquatable<PlotTrendConfig>
{
    public readonly IDataSource DataSource;
    public readonly string TrendName;

    public PlotTrendConfig(IDataSource dataSource, string trendName)
    {
        DataSource = dataSource;
        TrendName = trendName;
    }

    public bool Equals(PlotTrendConfig? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return DataSource.Equals(other.DataSource) && TrendName == other.TrendName;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((PlotTrendConfig)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(DataSource, TrendName);
    }

    public static bool operator ==(PlotTrendConfig? left, PlotTrendConfig? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(PlotTrendConfig? left, PlotTrendConfig? right)
    {
        return !Equals(left, right);
    }
}

public class XySerie
{
    public PlotTrendConfig? XTrend;
    public PlotTrendConfig? YTrend;

    public XySerie(PlotTrendConfig? xTrend, PlotTrendConfig? yTrend)
    {
        XTrend = xTrend;
        YTrend = yTrend;
    }
}

public static class MonthNames
{
    public static readonly List<string> Names = new() { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
    public static readonly List<string> ShortNames = new() { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
    public static readonly List<int> DaysInMonth = new() { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
}

public enum PlotMode
{
    Histogram,
    Ts,
    Xy,
}

public enum ExportType
{
    Tsv,
    Csv,
    Xlsx,
}
