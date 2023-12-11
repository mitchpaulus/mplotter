using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;

namespace csvplot;

public class MainViewModel : INotifyPropertyChanged
{
    public ObservableCollection<IDataSource> Sources { get; } = new();

    public static AvaPlot Plot = new();

    public void AddSource(string source)
    {
        SimpleDelimitedFile file = new(source);
        Sources.Add(file);
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

public class DataSourceViewModel
{
    public IDataSource DataSource { get; }

    public ObservableCollection<TrendItemViewModel> Trends { get; }

    public DataSourceViewModel(IDataSource dataSource)
    {
        DataSource = dataSource;
        Trends = new ObservableCollection<TrendItemViewModel>(DataSource.Trends.Select(t => new TrendItemViewModel {Name = t}));
    }

}

public class TrendItemViewModel : INotifyPropertyChanged
{
    public string Name { get; set; }
    public bool Checked { get; set; }
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