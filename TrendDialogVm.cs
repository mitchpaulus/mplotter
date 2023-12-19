using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
    }

    private void SelectionModelOnSelectionChanged(object? sender, SelectionModelSelectionChangedEventArgs<IDataSource> e)
    {
        if (sender is not SelectionModel<IDataSource> selectionModel) return;

        _selectedSources.Clear();

        foreach (var i in selectionModel.SelectedItems)
        {
            _selectedSources.Add(i);
        }

        OnPropertyChanged(nameof(SelectedSources));
    }

    public List<IDataSource> Sources { get; set; }

    public int Page { get; set; }

    public ObservableCollection<IDataSource> SourceList { get; set; }

    public SelectionModel<IDataSource> SelectionModel { get; set; }


    private ObservableCollection<IDataSource> _selectedSources;

    public ObservableCollection<IDataSource> SelectedSources
    {
        get => _selectedSources;
        set
        {
            _selectedSources = value;
            OnPropertyChanged();
        }
    }

    public async void DebugViewModel()
    {
        var j = 0;
    }


    public void SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        //
        foreach (var i in args.AddedItems)
        {
            var j = 0;
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