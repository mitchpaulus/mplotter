using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;

namespace csvplot;

public class MainViewModel : INotifyPropertyChanged
{
    public ObservableCollection<DataSourceViewModel> Sources { get; } = new();

    public static AvaPlot Plot = new();

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

    public MainViewModel()
    {
        AvaPlot p = new AvaPlot();
        double[] dataX = new double[] { 1, 2, 3, 4, 5 };
        double[] dataY = new double[] { 1, 4, 9, 16, 25 };
        p.Plot.Add.Scatter(dataX, dataY);
        p.Refresh();

        Model = p;
    }

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
}

public class DataSourceViewModel : INotifyPropertyChanged
{
    public IDataSource DataSource { get; }

    public string Header { get; set; }
    public ObservableCollection<TrendItemViewModel> Trends { get; }

    public readonly ObservableCollection<TrendItemViewModel> FilteredTrendBuffer;

    public void UpdateFilteredTrends()
    {
        OnPropertyChanged(nameof(FilteredTrends));
    }

    public ObservableCollection<TrendItemViewModel> FilteredTrends
    {
        get => FilteredTrendBuffer;
    }

    public DataSourceViewModel(IDataSource dataSource)
    {
        DataSource = dataSource;
        Header = dataSource.Header;
        Trends = new ObservableCollection<TrendItemViewModel>(DataSource.Trends.Select(t => new TrendItemViewModel(t)));
        FilteredTrendBuffer = new ObservableCollection<TrendItemViewModel>(DataSource.Trends.Select(t => new TrendItemViewModel(t)));
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
    public string Name { get; set; }

    private bool _checked;
    public bool Checked
    {
        get => _checked;
        set
        {
            if (SetField(ref _checked, value))
            {
                // Run this.
            }
        }
    }

    public TrendItemViewModel(string name)
    {
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