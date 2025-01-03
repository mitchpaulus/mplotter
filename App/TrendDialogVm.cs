using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.Selection;

namespace csvplot;

public class TrendDialogVm : INotifyPropertyChanged
{
    public TrendDialogVm(List<IDataSource> sources)
    {
        Sources = sources;
        SourceList = new ObservableCollection<IDataSource>(sources);
        SelectionModel = new SelectionModel<IDataSource>();
        SelectionModel.SelectionChanged += SelectionModelOnSelectionChanged;
        SelectionModel.SingleSelect = false;
        _selectedSources = new ObservableCollection<IDataSource>();

        SelectedTrendsSelectionModel = new SelectionModel<SourceTrendPairVm>
        {
            SingleSelect = false
        };
    }

    public int MyCount;

    private async void SelectionModelOnSelectionChanged(object? sender, SelectionModelSelectionChangedEventArgs<IDataSource> e)
    {
        if (sender is not SelectionModel<IDataSource> selectionModel) return;

        // _selectedSources.Clear();
        _availableTrends = new List<SourceTrendPairVm>();
        foreach (var i in selectionModel.SelectedItems)
        {
            if (i is null) continue;

            var trends = await i.Trends();
            foreach (var t in trends)
            {
                _availableTrends.Add(new(i, t.Name));
            }
        }

        MyCount++;

        OnPropertyChanged(nameof(AvailableTrends));
    }

    public List<IDataSource> Sources { get; set; }

    public int Page { get; set; }

    public ObservableCollection<IDataSource> SourceList { get; set; }

    public SelectionModel<IDataSource> SelectionModel { get; set; }

    public SelectionModel<SourceTrendPairVm> SelectedTrendsSelectionModel { get; set; }

    private ObservableCollection<IDataSource> _selectedSources;

    private List<SourceTrendPairVm> _availableTrends = new();

    public ObservableCollection<SourceTrendPairVm> AvailableTrends
    {
        get
        {
            return new ObservableCollection<SourceTrendPairVm>(_availableTrends);
        }

        set
        {
            _availableTrends = value.ToList();
            OnPropertyChanged();
        }
    }

    public ObservableCollection<IDataSource> SelectedSources
    {
        get => _selectedSources;
        set
        {
            _selectedSources = value;
            OnPropertyChanged();
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

public class SourceTrendPairVm
{
    public IDataSource Source { get; }
    public string Trend { get; }

    public SourceTrendPairVm(IDataSource source, string trend)
    {
        Source = source;
        Trend = trend;
    }
}
