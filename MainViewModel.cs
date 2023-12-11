using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.CodeAnalysis;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;
using ScottPlot.Statistics;

namespace csvplot;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly AvaPlot _avaPlot;
    private readonly IStorageProvider _storageProvider;

    public MainViewModel(AvaPlot avaPlot, IStorageProvider storageProvider)
    {
        _avaPlot = avaPlot;
        _storageProvider = storageProvider;
    }

    public void UpdatePlots()
    {
        _avaPlot.Plot.Clear();



        // double[] dataX = new double[] { 1, 2, 3, 4, 5 };
        // double[] dataY = new double[] { 1, 4, 9, 16, 25 };

        // double[] dataY = RandomDataGenerator.Generate.RandomSample(5, 25);

        // Middle click to reset axis limits
        // var scatter = _avaPlot.Plot.Add.Scatter(dataX, dataY);

        var series = new List<BarSeries>();

        foreach (var source in Sources)
        {
            List<TrendItemViewModel> selectedTrends = source.Trends.Where(model => source.CheckedTrends.Contains(model.Name)).ToList();

            foreach (var t in selectedTrends)
            {
                var data = source.DataSource.GetData(t.Name);

                if (_isTs || _isXy)
                {
                    _avaPlot.Plot.Add.Scatter(data.Select((d, i) => (double) i).ToArray(), data);
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
                    BarPlot barPlot = _avaPlot.Plot.Add.Bar(series);
                }
                // double min;
                // double max;
                // if (data.Length > 0)
                // {
                //     min = data.Min();
                //     max = data.Max();
                //     if (Math.Abs(min - max) < 0.00000001)
                //     {
                //         max = min + 1;
                //     }
                // }
                // else
                // {
                //     min = 0;
                //     max = 1;
                // }
                //
                // var hist = new Histogram(min, max, 50);
                //
                // var values = hist.Counts;
                // var binCenters = hist.BinCenters;
                //
                // List<Bar> s = new(binCenters.Length);
                // for (int i = 0; i < values.Length; i++)
                // {
                //     s.Add(new Bar()
                //     {
                //         Value = values[i],
                //         Position = binCenters[i]
                //     });
                // }
                //
                // series.Add(new BarSeries() { Bars = s });
            }

            // var barChart = _avaPlot.Plot.Add.Bar(series);
            // _avaPlot.Refresh();
        }

        _avaPlot.Plot.XLabel("TIME");
        _avaPlot.Plot.AutoScale();
        _avaPlot.Refresh();
    }

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
                        if (trend.Name.Contains(_trendFilter))
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

    private bool _isHistogram = true;

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

    private bool _isTs = false;

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

            SimpleDelimitedFile file = new SimpleDelimitedFile(filePath.Path.LocalPath);

            if (filePath != default(IStorageFile?))
            {
                Sources.Add(new(file, this));
            }
            // Handle the file path (e.g., updating the ViewModel)
        }
    }


}

public class DataSourceViewModel : INotifyPropertyChanged
{
    public readonly MainViewModel MainViewModel;
    public IDataSource DataSource { get; }
    public string Header { get; set; }

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
