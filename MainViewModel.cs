using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;

namespace csvplot;

public class MainViewModel : INotifyPropertyChanged
{
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