using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;
using ScottPlot.Statistics;

namespace csvplot;

public class MainViewModel : INotifyPropertyChanged
{
    // public readonly AvaPlot AvaPlot;
    private readonly IStorageProvider _storageProvider;
    private readonly MainWindow _window;

    private readonly UnitConverterReader _unitConverterReader = new();
    private readonly UnitReader _unitReader = new();

    public MainViewModel(AvaPlot avaPlot, IStorageProvider storageProvider, MainWindow window)
    {
        // AvaPlot = avaPlot;
        _storageProvider = storageProvider;
        _window = window;
        _unitConverterReader.Read();
        _unitReader.Read();
    }

    public void UpdatePlots()
    {
        _window.PlotStackPanel.Children.Clear();
        // AvaPlot.Plot.Clear();

        // double[] dataX = new double[] { 1, 2, 3, 4, 5 };
        // double[] dataY = new double[] { 1, 4, 9, 16, 25 };

        // double[] dataY = RandomDataGenerator.Generate.RandomSample(5, 25);

        // Middle click to reset axis limits
        // var scatter = _avaPlot.Plot.Add.Scatter(dataX, dataY);

        var series = new List<BarSeries>();

        List<(DataSourceViewModel s, TrendItemViewModel t)> selectedTrends = Sources.SelectMany(s =>
            s.Trends.Where(t => s.CheckedTrends.Contains(t.Name))
                    .Select(t => (s, t)))
                    .ToList();

        var unitGrouped = selectedTrends.GroupBy(tuple => tuple.t.Name.GetUnit());

        List<AvaPlot> plots = new();

        foreach (var unitGroup in unitGrouped)
        {
            var plot = new AvaPlot();

            string? unit = unitGroup.Key;

            foreach (var (source, t) in unitGroup)
            {
                 var data = source.DataSource.GetData(t.Name);

                 if (_isTs)
                 {
                     if (data.Length == 8760 || source.DataSource.DataSourceType != DataSourceType.NonTimeSeries) {
                         plot.Plot.AxisStyler.DateTimeTicks(Edge.Bottom);
                     }

                     var tsData = source.DataSource.GetTimestampData(t.Name);

                     // Bail if we messed up.
                     if (!tsData.LengthsEqual) continue;

                     double[] yData = tsData.Values.ToArray();

                     bool didConvert = false;
                     // if (_unitConverterReader.TryGetConversion(t.Name, _unitReader, out Unit? unitToConvertTo))
                     // {
                     //     foreach ((var unitTypeKey, Dictionary<string, Unit> associatedUnits) in _units)
                     //     {
                     //         if (associatedUnits.TryGetValue(unit, out Unit? fromUnit) && associatedUnits.TryGetValue(unitToConvertTo, out var toUnit))
                     //         {
                     //             yData = yData.Select(d =>
                     //                     d * fromUnit.Factor / toUnit.Factor)
                     //                     .ToArray();
                     //             didConvert = true;
                     //             break;
                     //         }
                     //     }
                     // }

                     string label = unitGroup.Count(tuple => tuple.t.Name == t.Name) < 2 ? t.Name : $"{source.DataSource.ShortName}: {t.Name}";
                     // if (didConvert) label = $"{label} in {unitToConvertTo}";

                     if (source.DataSource.DataSourceType == DataSourceType.EnergyModel || data.Length == 8760)
                     {
                         var signalPlot = plot.Plot.Add.Signal(yData, (double)1/24);
                         signalPlot.Label = label;
                         signalPlot.Data.XOffset = tsData.DateTimes.First().ToOADate();
                     }
                     else
                     {
                         double[] xData = tsData.DateTimes.Select(time => time.ToOADate()).ToArray();
                         // TODO: add safety here
                         // DateTime dateTimeStart = new DateTime(DateTime.Now.Year, 1, 1);
                         var scatter = plot.Plot.Add.Scatter(xData, yData);
                         scatter.Label = label;
                     }
                 }
                 else if (_isHistogram)
                 {

                     double min;
                     double max;
                     if (data.Length > 0)
                     {
                         min = data.Min();
                         max = data.Max();
                         if (Math.Abs(min - max) < 0.00000001)
                         {
                             max = min + 1;
                         }
                     }
                     else
                     {
                         min = 0;
                         max = 1;
                     }

                     var hist = new Histogram(min, max, 50);

                     hist.AddRange(data);

                     var values = hist.Counts;
                     var binCenters = hist.BinCenters;

                     List<Bar> s = new(binCenters.Length);
                     for (int i = 0; i < values.Length; i++)
                     {
                         Bar bar = new()
                         {

                         };

                         s.Add(new Bar()
                         {
                             Value = values[i],
                             Position = binCenters[i],
                         });

                     }

                     var barSeries = new BarSeries {Bars = s, Label = t.Name};
                     series.Add(barSeries);
                     BarPlot barPlot = plot.Plot.Add.Bar(series);
                 }
            }

            plot.Plot.Legend.IsVisible = unitGroup.Count() > 1;
            plot.Plot.YAxis.Label.Text = unitGroup.Count() == 1 ? unitGroup.First().t.Name : unitGroup.Key ?? "";

            plot.Plot.AutoScale();
            plots.Add(plot);
        }

        _window.PlotStackPanel.RowDefinitions.Clear();
        for (var index = 0; index < plots.Count; index++)
        {
            var p = plots[index];
            RowDefinition r = new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star),
                MinHeight = 100
            };
            _window.PlotStackPanel.RowDefinitions.Add(r);
            Grid.SetRow(p, index);
        }

        _window.PlotStackPanel.Children.AddRange(plots);

        foreach (var p in plots)
        {
            p.Refresh();
        }
    }

    private List<AvaPlot> AllPlots()
    {
        return _window.PlotStackPanel.Children.Where(control => control is AvaPlot).Cast<AvaPlot>().ToList();
    }

    private void FixMonth(int month)
    {
        if (month is < 1 or > 12) throw new ArgumentException("Month should be passed as 1-12");
         int currentYear = DateTime.Now.Year;
         foreach (var p in AllPlots())
         {
             if (month < 12)
             {
                 p.Plot.XAxis.Min = new DateTime(currentYear, month, 1).ToOADate();
                 p.Plot.XAxis.Max = new DateTime(currentYear, month + 1, 1).ToOADate();
             }
             else
             {
                 p.Plot.XAxis.Min = new DateTime(currentYear, month, 1).ToOADate();
                 p.Plot.XAxis.Max = new DateTime(currentYear + 1, 1, 1).ToOADate();
             }
             p.Plot.XAxis.Label.Text = MonthNames.Names[month - 1];
             p.Refresh();
         }
    }

    public void MakeJan() => FixMonth(1);
    public void MakeFeb() => FixMonth(2);

    public void MakeMar() => FixMonth(3);

    public void MakeApr() => FixMonth(4);

    public void MakeMay() => FixMonth(5);

    public void MakeJun() => FixMonth(6);

    public void MakeJul() => FixMonth(7);


    public void MakeAug() => FixMonth(8);

    public void MakeSep() => FixMonth(9);


    public void MakeOct() => FixMonth(10);


    public void MakeNov() => FixMonth(11);

    public void MakeDec() => FixMonth(12);


    public ObservableCollection<DataSourceViewModel> Sources { get; } = new();

    private string _trendFilter = "";
    public string TrendFilter
    {
        get => _trendFilter;
        set
        {
            if (SetField(ref _trendFilter, value))
            {
                foreach (var source in Sources)
                {
                    source.FilteredTrendBuffer.Clear();
                    foreach (var trend in source.Trends)
                    {
                        if (trend.Name.ToLowerInvariant().Contains(_trendFilter.ToLowerInvariant()))
                        {
                            source.FilteredTrendBuffer.Add(trend);
                        }
                    }

                    source.UpdateFilteredTrends();
                }
            };
        }
    }

    private bool _ignoreMonday = false;
    public bool IgnoreMonday
    {
        get => _ignoreMonday;
        set => SetField(ref _ignoreMonday, value);
    }

    private bool _ignoreTuesday = false;
    public bool IgnoreTuesday
    {
        get => _ignoreTuesday;
        set => SetField(ref _ignoreTuesday, value);
    }

    private bool _ignoreWednesday = false;
    public bool IgnoreWednesday
    {
        get => _ignoreWednesday;
        set => SetField(ref _ignoreWednesday, value);
    }

    private bool _ignoreThursday = false;
    public bool IgnoreThursday
    {
        get => _ignoreThursday;
        set => SetField(ref _ignoreThursday, value);
    }

    private bool _ignoreFriday = false;
    public bool IgnoreFriday
    {
        get => _ignoreFriday;
        set => SetField(ref _ignoreFriday, value);
    }

    private bool _ignoreSaturday = false;
    public bool IgnoreSaturday
    {
        get => _ignoreSaturday;
        set => SetField(ref _ignoreSaturday, value);
    }

    private bool _ignoreSunday = false;
    public bool IgnoreSunday
    {
        get => _ignoreSunday;
        set => SetField(ref _ignoreSunday, value);
    }

    private bool _isHistogram = false;

    public bool IsHistogram
    {
        get => _isHistogram;
        set
        {
            if (SetField(ref _isHistogram, value))
            {
                UpdatePlots();
            }
        }
    }

    private bool _isTs = true;

    public bool IsTs
    {
        get => _isTs;
        set
        {
            if (SetField(ref _isTs, value))
            {
                UpdatePlots();
            }
        }
    }

    private bool _isXy = false;

    public bool IsXy
    {
        get => _isXy;
        set
        {
            if (SetField(ref _isXy, value))
            {
                UpdatePlots();
            }
        }
    }


    // public MainViewModel()
    // {
    //     AvaPlot p = new AvaPlot();
    //     double[] dataX = new double[] { 1, 2, 3, 4, 5 };
    //     double[] dataY = new double[] { 1, 4, 9, 16, 25 };
    //     p.Plot.Add.Scatter(dataX, dataY);
    //     p.Refresh();
    //
    //     Model = p;
    // }

    private AvaPlot? _model;

    /// <summary>
    /// Gets the plot model.
    /// </summary>
    public AvaPlot Model
    {
        get => _model ?? new AvaPlot() {};
        set
        {
            if (_model != value)
            {
                _model = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Model)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public async void BrowseButton_Click()
    {
        // Use StorageProvider to show the dialog
        var result = await _storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            FileTypeFilter = new []{ MainWindow.DateFileTypes }
        } );

        if (result.Any())
        {
            // Get the selected file path
            IStorageFile? filePath = result[0];

            IDataSource source = DataSourceFactory.SourceFromLocalPath(filePath.Path.LocalPath);

            if (filePath != default(IStorageFile?))
            {
                Sources.Add(new(source, this));

               // Handle the file path (e.g., updating the ViewModel)
               await SaveToMru(filePath.Path.LocalPath);
            }
        }
    }

    private static async Task SaveToMru(string localPath)
    {
        // Save to MRU file list (up to 20)
        if (OperatingSystem.IsWindows())
        {
            string? localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (localAppData is not null)
            {
                int tries = 0;
                while (tries < 3)
                {
                    try
                    {
                        Directory.CreateDirectory($"{localAppData}\\mplotter");
                        var path = $"{localAppData}\\mplotter\\mru.txt";

                        List<string> lines;
                        try
                        {
                            lines = (await File.ReadAllLinesAsync(path)).ToList();
                        }
                        catch (FileNotFoundException)
                        {
                            lines = new List<string>();
                        }

                        var newLines = new List<string>();
                        newLines.Add(localPath);

                        foreach (var line in lines)
                        {
                            if (newLines.Contains(line)) continue;
                            newLines.Add(line);
                        }

                        await File.WriteAllLinesAsync(path, newLines, new UTF8Encoding(false));
                        break;
                    }
                    catch (Exception)
                    {
                        tries++;
                    }
                }
            }
        }
    }

    public async void SelectTrendClick()
    {
        var dialog = new TrendDialog();
        var vm = new TrendDialogVm(Sources.Select(model => model.DataSource).ToList());
        dialog.DataContext = vm;

        await dialog.ShowDialog(_window);
    }
}

public class DataSourceViewModel : INotifyPropertyChanged
{
    public readonly MainViewModel MainViewModel;
    public IDataSource DataSource { get; }
    public string Header { get; set; }
    public string EscapedHeader => Header.Replace("_", "__");

    public HashSet<string> CheckedTrends = new();

    public ObservableCollection<TrendItemViewModel> Trends { get; }

    public readonly ObservableCollection<TrendItemViewModel> FilteredTrendBuffer;

    public void UpdateFilteredTrends()
    {
        OnPropertyChanged(nameof(FilteredTrends));
    }

    public ObservableCollection<TrendItemViewModel> FilteredTrends => FilteredTrendBuffer;

    public DataSourceViewModel(IDataSource dataSource, MainViewModel mainViewModel)
    {
        MainViewModel = mainViewModel;
        DataSource = dataSource;
        Header = dataSource.Header;
        Trends = new ObservableCollection<TrendItemViewModel>(DataSource.Trends.Select(t => new TrendItemViewModel(t, this)));
        FilteredTrendBuffer = new ObservableCollection<TrendItemViewModel>(DataSource.Trends.Select(t => new TrendItemViewModel(t, this)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public class TrendItemViewModel : INotifyPropertyChanged
{
    private readonly DataSourceViewModel _dataSourceViewModel;
    public string Name { get; set; }
    public string EscapedName => Name.Replace("_", "__");

    public bool Checked
    {
        get => _dataSourceViewModel.CheckedTrends.Contains(Name);
        set
        {
            if (value)
            {
                _dataSourceViewModel.CheckedTrends.Add(Name);
            }
            else
            {
                _dataSourceViewModel.CheckedTrends.Remove(Name);
            }
            OnPropertyChanged();
            _dataSourceViewModel.MainViewModel.UpdatePlots();
        }
    }

    public TrendItemViewModel(string name, DataSourceViewModel dataSourceViewModel)
    {
        _dataSourceViewModel = dataSourceViewModel;
        Name = name;
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
