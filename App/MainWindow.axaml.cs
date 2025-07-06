using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Domain;
using OfficeOpenXml;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.TickGenerators.TimeUnits;
using Brushes = Avalonia.Media.Brushes;
using File = System.IO.File;
using HorizontalAlignment = Avalonia.Layout.HorizontalAlignment;

namespace csvplot;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public readonly List<XySerie> XySeries = new();

    private readonly List<IDataSource> _loadedDataSources = new();
    private readonly List<FileSystemWatcher> _watchers = new();

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

    public DateMode DateMode = DateMode.Unspecified;

    public DateTime StartDate = DateTime.Today.AddDays(-28);
    public DateTime EndDate = DateTime.Today;

    public int StartMonthInt => StartDate.Year * 12 + (StartDate.Month - 1);
    public int EndMonthInt => EndDate.Year * 12 + (EndDate.Month - 1);

    public DateTime DateTimeMonthFromInt(int monthInt) => new(monthInt / 12, monthInt % 12 + 1, 1);


    private Timer _searchTimer;

    public readonly TrendConfigListener Listener = new();

    public AvaPlot XyPlot = new();
    private SingleDayState _singleDayState;

    private int _lastSingleDayYear;
    private int _lastSingleDayMonth = 1;
    private int _lastSingleDayDay = 1;

    private Button _singleDayNotStartedButton = new Button() { Content = "Single Day" };

    private List<Button> _singleDayYearButtons = new(5);
    private List<Button> _singleDayMonthButtons = new(12)
    {
        new Button() { Content = "Jan", Tag = 1 },
        new Button() { Content = "Feb", Tag = 2 },
        new Button() { Content = "Mar", Tag = 3 },
        new Button() { Content = "Apr", Tag = 4 },
        new Button() { Content = "May", Tag = 5 },
        new Button() { Content = "Jun", Tag = 6 },
        new Button() { Content = "Jul", Tag = 7 },
        new Button() { Content = "Aug", Tag = 8 },
        new Button() { Content = "Sep", Tag = 9 },
        new Button() { Content = "Oct", Tag = 10 },
        new Button() { Content = "Nov", Tag = 11 },
        new Button() { Content = "Dec", Tag = 12 },
    };
    private List<Button> _singleDayDayButtons = new(31);

    public MainWindow()
    {
        InitializeComponent();

        _searchTimer = new Timer(200);
        _searchTimer.AutoReset = false;
        _searchTimer.Elapsed += SearchTimerOnElapsed;
        _searchTimer.Stop();

        Listener.Load();
        AvaPlot? plot = this.Find<AvaPlot>("AvaPlot");

        _vm = new MainViewModel(plot!, StorageProvider, this);
        DataContext = _vm;

        UpdateMrus();
        _timeSeriesTrendsListBox.SelectionChanged += TimeSeriesTrendList_OnSelectionChanged;
        ScrollViewer.SetAllowAutoHide(_timeSeriesTrendsListBox, false);
        Grid.SetRow(_timeSeriesTrendsListBox, 5);

        Mode = PlotMode.Ts;
        TsRadio.IsChecked = true;

        SetDateMode(DateMode.Unspecified);

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

        int currentYear = DateTime.Today.Year;
        _lastSingleDayYear = currentYear;

        _singleDayNotStartedButton.Click += SingleDayClick;
        SingleDayStackPanel.Children.Add(_singleDayNotStartedButton);
        for (int i = currentYear; i >= currentYear - 4; i--)
        {
            _singleDayYearButtons.Add(new Button()
            {
                Content = i.ToString(),
                IsVisible = true,
                Tag = i,
            });
        }
        foreach (var b in _singleDayYearButtons) b.Click += SingleDayClick;
        foreach (var b in _singleDayMonthButtons) b.Click += SingleDayClick;

        for (int i = 0; i < 31; i++)
        {
            _singleDayDayButtons.Add(new Button()
            {
                Content = (i + 1).ToString(),
                IsVisible = true,
                Tag = (i + 1),
            });
        }
        foreach (var b in _singleDayDayButtons) b.Click += SingleDayClick;
    }

    private async void SearchTimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        await Dispatcher.UIThread.InvokeAsync(UpdateVisibleTimeSeriesTrendList);
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
                b.Text = xTrend.Trend.DisplayName;
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
                b.Text = yTrend.Trend.DisplayName;
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
        await _vm.UpdatePlots();
    }

    private async void RemoveXySerie(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button b) return;
        if (b.Tag is not XySerie s) return;
        XySeries.RemoveAll(serie => ReferenceEquals(serie, s));
        UpdateXyGrid();
        await _vm.UpdatePlots();
    }

    private async Task HandlePlotTypeChange(PlotMode mode)
    {
        if (mode == PlotMode.Xy)
        {
            MainSourceGrid.Children.Remove(_timeSeriesTrendsListBox);
            if (!MainSourceGrid.Children.Contains(_xyTrendSelectionGrid))
            {
                MainSourceGrid.Children.Add(_xyTrendSelectionGrid);
            }

            Mode = mode;
        }
        else if (mode == PlotMode.Ts)
        {
            Mode = mode;
            MainSourceGrid.Children.Remove(_xyTrendSelectionGrid);
            if (!MainSourceGrid.Children.Contains(_timeSeriesTrendsListBox))
            {
                MainSourceGrid.Children.Add(_timeSeriesTrendsListBox);
            }
        }
        else if (mode == PlotMode.Histogram)
        {
            Mode = PlotMode.Histogram;
            if (!MainSourceGrid.Children.Contains(_timeSeriesTrendsListBox))
            {
                MainSourceGrid.Children.Add(_timeSeriesTrendsListBox);
            }
        }
        await _vm.UpdatePlots();
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
        await UpdateBackingAvailableTimeSeriesTrendList();
        UpdateVisibleTimeSeriesTrendList();
        await _vm.UpdatePlots();
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

        foreach (var c in MruPanel.Children) if (c is Button b) b.IsEnabled = false;
        BrowseButton.IsEnabled = false;
        InfluxButton.IsEnabled = false;
        NoaaButton.IsEnabled = false;

        _loadedDataSources.Add(source);

        if (source is SimpleDelimitedFile file)
        {
            if (!_watchers.Any(watcher => watcher.Path == file.FileInfo.DirectoryName! && watcher.Filter == file.FileInfo.Name))
            {
                FileSystemWatcher watcher = new FileSystemWatcher(file.FileInfo.DirectoryName!, file.FileInfo.Name);
                watcher.NotifyFilter = NotifyFilters.Attributes
                                                              | NotifyFilters.CreationTime
                                                              | NotifyFilters.DirectoryName
                                                              | NotifyFilters.FileName
                                                              | NotifyFilters.LastWrite
                                                              | NotifyFilters.Security
                                                              | NotifyFilters.Size;
                watcher.Changed += WatcherOnChanged;
                watcher.Deleted += WatcherOnDeleted;
                watcher.Created += WatcherOnChanged;
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
        }
        else if (source is EnergyPlusEsoDataSource esoFile)
        {
             if (!_watchers.Any(watcher => watcher.Path == esoFile.FileInfo.DirectoryName! && watcher.Filter == esoFile.FileInfo.Name))
             {
                 FileSystemWatcher watcher = new FileSystemWatcher(esoFile.FileInfo.DirectoryName!, esoFile.FileInfo.Name);
                 watcher.NotifyFilter = NotifyFilters.Attributes
                                                               | NotifyFilters.CreationTime
                                                               | NotifyFilters.DirectoryName
                                                               | NotifyFilters.FileName
                                                               | NotifyFilters.LastWrite
                                                               | NotifyFilters.Security
                                                               | NotifyFilters.Size;
                 watcher.Changed += WatcherOnChanged;
                 watcher.Deleted += WatcherOnDeleted;
                 watcher.Created += WatcherOnChanged;
                 watcher.EnableRaisingEvents = true;
                 _watchers.Add(watcher);
             }
        }

        _selectedDataSources.Add(source);
        RenderDataSources();
        await UpdateBackingAvailableTimeSeriesTrendList();
        UpdateVisibleTimeSeriesTrendList();

        foreach (var c in MruPanel.Children) if (c is Button b) b.IsEnabled = true;
        BrowseButton.IsEnabled = true;
        InfluxButton.IsEnabled = true;
        NoaaButton.IsEnabled = true;
    }

    private void WatcherOnDeleted(object sender, FileSystemEventArgs e)
    {
        FileSystemWatcher w = (FileSystemWatcher)sender;
        Console.Error.WriteLine($"Dir: {w.Path}, Filter: {w.Filter}");
    }

    private async void WatcherOnChanged(object sender, FileSystemEventArgs e)
    {
        FileSystemWatcher w = (FileSystemWatcher)sender;

        // Find matching sources
        foreach (var s in _loadedDataSources)
        {
            if (s is SimpleDelimitedFile file)
            {
                if (file.FileInfo.DirectoryName != w.Path || file.FileInfo.Name != w.Filter) continue;

                // Reload the source
                await file.UpdateCache();
            }
            else if (s is EnergyPlusEsoDataSource esoFile)
            {
                await esoFile.UpdateCache();
            }
        }

        await Dispatcher.UIThread.InvokeAsync(() => _vm.UpdatePlots());
        await Console.Error.WriteLineAsync($"{DateTime.Now:HH:mm:ss.fff} Updated plots for Dir: {w.Path}, Filter: {w.Filter}");
    }

    private async Task UpdateBackingAvailableTimeSeriesTrendList()
    {
        _availableTimeSeriesTrends.Clear();
        foreach (var s in _selectedDataSources)
        {
            var trends = await s.Trends();
            _availableTimeSeriesTrends.AddRange(trends.Select(trend => new PlotTrendConfig(s, trend)));
        }
    }

    private void UpdateVisibleTimeSeriesTrendList()
    {
        Dictionary<string, List<PlotTrendConfig>> grouped = _availableTimeSeriesTrends.GroupBy(config => config.Trend.Name).ToDictionary(configs => configs.Key, configs => configs.ToList());
        var sortedKeys = grouped.Keys.OrderBy(s => s.ToLowerInvariant());

        List<TextBlock> timeSeriesTextBlocks = _currentTimeSeriesTextBlocks == 1 ? _timeSeriesTextBlocks2 : _timeSeriesTextBlocks1;
        timeSeriesTextBlocks.Clear();

        string loweredSearchText = SearchBox.Text?.ToLowerInvariant().Trim() ?? "";

        if (!loweredSearchText.Contains("*"))
        {
            foreach (var key in sortedKeys)
            {
                if (!key.ToLowerInvariant().Contains(loweredSearchText)) continue;

                List<PlotTrendConfig> trends = grouped[key];
                if (trends.Count > 1)
                {
                    foreach (var t in trends)
                    {
                        TextBlock b = new TextBlock();
                        b.Text = $"{t.DataSource.ShortName}: {t.Trend.DisplayName}";
                        b.Tag = t;
                        timeSeriesTextBlocks.Add(b);
                    }
                }
                else
                {
                    var t = trends[0];
                    TextBlock b = new TextBlock();
                    b.Text = $"{t.Trend.DisplayName}";
                    b.Tag = t;
                    timeSeriesTextBlocks.Add(b);
                }
            }
        }
        else
        {

            Regex r = new Regex('^' + loweredSearchText.Replace("*", ".*") + '$');

            foreach (var key in sortedKeys)
            {
                if (!r.Match(key.ToLowerInvariant()).Success) continue;

                List<PlotTrendConfig> trends = grouped[key];
                if (trends.Count > 1)
                {
                    foreach (var t in trends)
                    {
                        TextBlock b = new TextBlock();
                        b.Text = $"{t.DataSource.ShortName}: {t.Trend.DisplayName}";
                        b.Tag = t;
                        timeSeriesTextBlocks.Add(b);
                    }
                }
                else
                {
                    var t = trends[0];
                    TextBlock b = new TextBlock();
                    b.Text = $"{t.Trend.DisplayName}";
                    b.Tag = t;
                    timeSeriesTextBlocks.Add(b);
                }
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

        if (source is SimpleDelimitedFile simpleFile)
        {
            for (int i = _watchers.Count - 1; i >= 0; i--)
            {
                var w = _watchers[i];
                if (w.Path == simpleFile.FileInfo.DirectoryName &&
                    w.Filter == simpleFile.FileInfo.Name)
                {
                    _watchers.RemoveAt(i);
                }
            }
        }

        _loadedDataSources.Remove(source);
        _selectedDataSources.Remove(source);

        SelectedTimeSeriesTrends.RemoveAll(config => config.DataSource == source);

        foreach (var c in DataSourcesList.Children)
        {
            if (c is not Grid g) continue;
            if (!g.Children.Any(control => ReferenceEquals(control, b))) continue;

            DataSourcesList.Children.Remove(c);
            break;
        }

        await UpdateBackingAvailableTimeSeriesTrendList();
        UpdateVisibleTimeSeriesTrendList();
        await _vm.UpdatePlots();
    }



    public void UpdateMrus()
    {
        List<string> mrus = MostRecentlyUsedFiles();

        var buttons = mrus.Select(mru =>
            {
                // Add a button to load that file
                Button button = new Button();
                IDataSource source = DataSourceFactory.SourceFromLocalPath(mru, Listener);
                button.Click += AddDataSourceMruClick;
                button.Content = Path.GetFileName(mru).EscapeUiText();
                button.Tag = source;

                ContextMenu contextMenu = new ContextMenu();
                MenuItem removeItem = new MenuItem()
                {
                    Header = "Remove"
                };
                contextMenu.Items.Add(removeItem);
                button.ContextMenu = contextMenu;

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

        return lines.Where(File.Exists).ToList();
    }

    public static readonly FilePickerFileType DateFileTypes = new("Data Files")
    {
        Patterns = new[] { "*.csv", "*.tsv", "*.txt", "*.sql", "*.db" , "*.eso" }
    };

    private async Task<bool> AnyDbSourcesSelected()
    {
        foreach (var t in SelectedTimeSeriesTrends)
        {
            if (await t.DataSource.DataSourceType() == DataSourceType.Database) return true;
        }

        return false;
    }

    private bool OnMonth()
    {
        return DateMode == DateMode.Specified && StartDate.Day == 1 && EndDate.Day == 1;
    }

    private async Task TryShiftMonth(int monthShift)
    {
        if (DateMode != DateMode.Specified) return;
        if (OnMonth())
        {
            StartDate = DateTimeMonthFromInt(StartMonthInt + monthShift);
            EndDate = DateTimeMonthFromInt(EndMonthInt + monthShift);
            DateMode = DateMode.Specified;

            UpdateDateModeString();

            if (await AnyDbSourcesSelected() || Mode != PlotMode.Ts)
            {
                await _vm.UpdatePlots();
            }

            if (Mode == PlotMode.Ts)
            {
                foreach (var child in PlotStackPanel.Children)
                {
                    if (child is not AvaPlot avaPlot) continue;
                    avaPlot.Plot.Axes.SetLimitsX(StartDate.ToOADate(), EndDate.ToOADate());
                    if (EndMonthInt - StartMonthInt == 1)
                    {
                        avaPlot.Plot.Axes.Bottom.Label.Text = MonthNames.Names[StartDate.Month - 1];
                    }

                    avaPlot.Refresh();
                }
            }
        }
        else
        {
            int currentDayRange = (int)(EndDate - StartDate).TotalDays;
            StartDate = StartDate.AddDays(currentDayRange * monthShift);
            EndDate = EndDate.AddDays(currentDayRange * monthShift);
            DateMode = DateMode.Specified;

            UpdateDateModeString();

            if (await AnyDbSourcesSelected() || Mode != PlotMode.Ts)
            {
                await _vm.UpdatePlots();
            }

            if (Mode == PlotMode.Ts)
            {
                foreach (var child in PlotStackPanel.Children)
                {
                    if (child is not AvaPlot avaPlot) continue;
                    avaPlot.Plot.Axes.SetLimitsX(StartDate.ToOADate(), EndDate.ToOADate());

                    if (StartDate.Year == EndDate.Year && StartDate.Month == EndDate.Month)
                    {
                        avaPlot.Plot.Axes.Bottom.Label.Text = $"{MonthNames.ShortNames[StartDate.Month - 1]} {StartDate.Day}-{EndDate.Day}";
                    }
                    else if (StartDate.Year == EndDate.Year)
                    {
                        avaPlot.Plot.Axes.Bottom.Label.Text = $"{MonthNames.ShortNames[StartDate.Month - 1]} {StartDate.Day} - {MonthNames.ShortNames[StartDate.Month - 1]} {EndDate.Day}";
                    }
                    else
                    {
                        avaPlot.Plot.Axes.Bottom.Label.Text = $"{MonthNames.ShortNames[StartDate.Month - 1]} {StartDate.Day}, {StartDate.Year} - {MonthNames.ShortNames[StartDate.Month - 1]} {EndDate.Day}, {EndDate.Year}";
                    }

                    avaPlot.Refresh();
                }
            }
        }
    }

    private async void InputElement_OnKeyDown(object? sender, KeyEventArgs e)
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
            await TryShiftMonth(-1);
        }
        else if (e.Key == Key.Right)
        {
            // Check that a text box is currently not focused
            if (FocusManager == null || FocusManager.GetFocusedElement() is TextBox) return;
            await TryShiftMonth(1);
        }
    }

    private async void ClearSelections(object? sender, RoutedEventArgs e)
    {
        SelectedTimeSeriesTrends.Clear();
        _timeSeriesTrendsListBox.SelectedItems!.Clear();
        await _vm.UpdatePlots();
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

    private void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchTimer.Stop();
        _searchTimer.Start();
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
            var trends = sourcePair.Select(pair => pair.Trend).ToList();

            List<TimestampData> output;
            if (sourcePair.Key is InfluxDataSource influxDataSource)
            {
                // Need to specify a higher count than default, as we don't have to be as worried about plotting performance.
                output = await influxDataSource.GetTimestampData(trends.Select(trend => trend.Name).ToList(), start.AddDays(-1), end.AddDays(1), 500000);
            }
            else
            {
                output = await sourcePair.Key.GetTimestampData(trends.Select(trend => trend.Name).ToList(), start.AddDays(-1), end.AddDays(1));
            }

            headers.AddRange(trends.Select(trend => trend.Name));

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
                ExcelPackage.License.SetNonCommercialPersonal("Mitchell T Paulus");

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

    private async void TimeSeriesTrendList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
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
        await _vm.UpdatePlots();
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

            IDataSource source = DataSourceFactory.SourceFromLocalPath(filePath.Path.LocalPath, Listener);

            await AddDataSource(source);

            // Handle the file path (e.g., updating the ViewModel)
            await MainViewModel.SaveToMru(filePath.Path.LocalPath);

            UpdateMrus();
        }
    }

    private async void XyRadio_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (((RadioButton)sender!).IsChecked ?? false)
        {
            await HandlePlotTypeChange(PlotMode.Xy);
        }
    }

    private async void Histogram_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (((RadioButton)sender!).IsChecked ?? false)
        {
            await HandlePlotTypeChange(PlotMode.Histogram);
        }
    }

    private async void Ts_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (((RadioButton)sender!).IsChecked ?? false)
        {
            await HandlePlotTypeChange(PlotMode.Ts);
        }
    }

    private async void NoaaButtonClick(object? sender, RoutedEventArgs e)
    {
        NoaaDialog d = new();
        await d.AddStations();
        await d.ShowDialog(this);

        if (d.SelectedStation is not null)
        {
            await AddDataSource(new NoaaWeatherDataSource(d.SelectedStation));
        }
    }

    private async void IgnoreDow_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        await _vm.UpdatePlots();
    }

    private async void ClearDateMode(object? sender, RoutedEventArgs e)
    {
        SetDateMode(DateMode.Unspecified);
        await _vm.UpdatePlots();
    }

    public void SetDateMode(DateMode mode)
    {
        DateMode = mode;
        UpdateDateModeString();
    }

    public void UpdateDateModeString()
    {
        DateModeTextBlock.Text = DateModeString();
    }

    public string DateModeString()
    {
        return DateMode == DateMode.Specified
            ? $"Date mode: {DateMode}, Start: {StartDate:yyyy-MM-dd}, End: {EndDate:yyyy-MM-dd}"
            : $"Date mode: {DateMode}";
    }

    private async void MakeWeekOne(object? sender, RoutedEventArgs e)
    {
        int weekNum = int.Parse(((sender as Button)!.Tag as string)!);
        DateTime now = DateTime.Now;
        int currentYear = now.Year;

        SetDateMode(DateMode.Specified);
        StartDate = new DateTime(currentYear, 1, 1).AddDays((weekNum - 1)*7 );
        EndDate = new DateTime(currentYear, 1, 8).AddDays((weekNum - 1)*7 );
        UpdateDateModeString();

        foreach (var config in SelectedTimeSeriesTrends)
        {
            var dataType = await config.DataSource.DataSourceType();
            if (dataType != DataSourceType.Database) continue;

            await _vm.UpdatePlots();
            break;
        }

        foreach (var p in _vm.AllPlots())
        {
            if (Mode == PlotMode.Ts)
            {
                p.Plot.Axes.Bottom.Min = StartDate.ToOADate();
                p.Plot.Axes.Bottom.Max = EndDate.ToOADate();
                p.Plot.Axes.Bottom.Label.Text = $"{StartDate:MMM d} - {EndDate:MMM d}";
            }

            p.Refresh();
        }
    }

    private async void MakeSingleDay()
    {
        SetDateMode(DateMode.Specified);
        StartDate = new DateTime(_lastSingleDayYear, _lastSingleDayMonth, _lastSingleDayDay);
        EndDate = StartDate.AddDays(1);
        UpdateDateModeString();

        foreach (var config in SelectedTimeSeriesTrends)
        {
            var dataType = await config.DataSource.DataSourceType();
            if (dataType != DataSourceType.Database) continue;

            await _vm.UpdatePlots();
            break;
        }

        foreach (var p in _vm.AllPlots())
        {
            if (Mode == PlotMode.Ts)
            {
                p.Plot.Axes.Bottom.Min = StartDate.ToOADate();
                p.Plot.Axes.Bottom.Max = EndDate.ToOADate();
                p.Plot.Axes.Bottom.Label.Text = $"{StartDate:MMM d}";
            }

            p.Refresh();
        }
    }


    private void SingleDayClick(object? sender, RoutedEventArgs e)
    {
        Button button = (Button)sender!;
        if (_singleDayState == SingleDayState.NotStarted)
        {
            SingleDayStackPanel.Children.Clear();
            foreach (var b in _singleDayYearButtons) SingleDayStackPanel.Children.Add(b);
            _singleDayState = SingleDayState.Year;
        }
        else if (_singleDayState == SingleDayState.Year)
        {
            SingleDayStackPanel.Children.Clear();
            foreach (var b in _singleDayMonthButtons) SingleDayStackPanel.Children.Add(b);
            _singleDayState = SingleDayState.Month;
            _lastSingleDayYear = (int)button.Tag!;
        }
        else if (_singleDayState == SingleDayState.Month)
        {
            SingleDayStackPanel.Children.Clear();
            foreach (var b in _singleDayDayButtons) SingleDayStackPanel.Children.Add(b);
            _singleDayState = SingleDayState.Day;
            _lastSingleDayMonth = (int)button.Tag!;

        }
        else if (_singleDayState == SingleDayState.Day)
        {
            SingleDayStackPanel.Children.Clear();
            SingleDayStackPanel.Children.Add(_singleDayNotStartedButton);
            _singleDayState = SingleDayState.NotStarted;
            _lastSingleDayDay = (int)button.Tag!;
            MakeSingleDay();
        }
    }
}

public class PlotTrendConfig : IEquatable<PlotTrendConfig>
{
    public readonly IDataSource DataSource;
    public readonly Trend Trend;

    public PlotTrendConfig(IDataSource dataSource, Trend trend)
    {
        DataSource = dataSource;
        Trend = trend;
    }

    public bool Equals(PlotTrendConfig? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return DataSource.Equals(other.DataSource) && Trend == other.Trend;
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
        return HashCode.Combine(DataSource, Trend);
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

public enum DateMode
{
    Unspecified,
    Specified,
}

public enum ExportIncludeOption
{
    IncludeAllRows,
    IncludeOnlyFull,
}

public enum SingleDayState
{
    NotStarted,
    Year,
    Month,
    Day,
}

public static class GlobMatcher
{
    public static bool Match(string text, string pattern)
    {
        int ti = 0, pi = 0;
        int starIdx = -1, match = 0;

        while (ti < text.Length)
        {
            if (pi < pattern.Length && (pattern[pi] == text[ti]))
            {
                // Characters match
                ti++;
                pi++;
            }
            else if (pi < pattern.Length && pattern[pi] == '*')
            {
                // Star found, remember position
                starIdx = pi;
                match = ti;
                pi++;
            }
            else if (starIdx != -1)
            {
                // Backtrack to last *
                pi = starIdx + 1;
                match++;
                ti = match;
            }
            else
            {
                return false;
            }
        }

        // Consume trailing stars
        while (pi < pattern.Length && pattern[pi] == '*')
        {
            pi++;
        }

        return pi == pattern.Length;
    }
}
