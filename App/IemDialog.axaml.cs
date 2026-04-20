using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;

namespace csvplot;

public partial class IemDialog : Window
{
    private readonly List<Grid> _allGrids = new();

    private readonly List<Grid> _grids1 = new();
    private readonly List<Grid> _grids2 = new();

    private int _currentGrid = 1;

    private string? _stidSearch = "";
    private string? _nameSearch = "";

    public IemStation? SelectedStation { get; private set; }

    public IemDialog()
    {
        InitializeComponent();
        BuildStationList();
    }

    private void BuildStationList()
    {
        _allGrids.Clear();
        foreach (var station in IemStations.All)
        {
            var row = new Grid();

            row.ColumnDefinitions.Add(new ColumnDefinition(100, GridUnitType.Pixel)); // STID
            row.ColumnDefinitions.Add(new ColumnDefinition(400, GridUnitType.Pixel)); // Name
            row.ColumnDefinitions.Add(new ColumnDefinition(100, GridUnitType.Pixel)); // Lat
            row.ColumnDefinitions.Add(new ColumnDefinition(100, GridUnitType.Pixel)); // Lon
            row.ColumnDefinitions.Add(new ColumnDefinition(100, GridUnitType.Pixel)); // Elev
            row.ColumnDefinitions.Add(new ColumnDefinition(125, GridUnitType.Pixel)); // Begin
            row.ColumnDefinitions.Add(new ColumnDefinition(125, GridUnitType.Pixel)); // End
            row.ColumnDefinitions.Add(new ColumnDefinition(125, GridUnitType.Pixel)); // Network

            var stid = new TextBlock { Text = station.Stid };
            var name = new TextBlock { Text = station.Name };
            var lat = new TextBlock { Text = station.Lat.ToString("f4") };
            var lon = new TextBlock { Text = station.Lon.ToString("f4") };
            var elev = new TextBlock { Text = Math.Round(station.Elev).ToString(CultureInfo.InvariantCulture) };
            var begin = new TextBlock { Text = station.Begin?.ToString("yyyy-MM-dd") ?? "" };
            var end = new TextBlock { Text = station.End?.ToString("yyyy-MM-dd") ?? "" };
            var network = new TextBlock { Text = station.Network };

            row.Children.Add(stid);
            row.Children.Add(name);
            row.Children.Add(lat);
            row.Children.Add(lon);
            row.Children.Add(elev);
            row.Children.Add(begin);
            row.Children.Add(end);
            row.Children.Add(network);

            Grid.SetColumn(stid, 0);
            Grid.SetColumn(name, 1);
            Grid.SetColumn(lat, 2);
            Grid.SetColumn(lon, 3);
            Grid.SetColumn(elev, 4);
            Grid.SetColumn(begin, 5);
            Grid.SetColumn(end, 6);
            Grid.SetColumn(network, 7);

            row.Tag = station;
            _allGrids.Add(row);
        }

        _grids1.Clear();
        _grids1.AddRange(_allGrids);
        IemStationsListBox.ItemsSource = _grids1;
        _currentGrid = 1;
    }

    private void UpdateStationList()
    {
        var grid = _currentGrid == 1 ? _grids2 : _grids1;

        grid.Clear();
        foreach (var g in _allGrids)
        {
            IemStation s = (IemStation)g.Tag!;

            if (!string.IsNullOrWhiteSpace(_stidSearch) && !s.Stid.ToLower().Contains(_stidSearch.ToLower())) continue;
            if (!string.IsNullOrWhiteSpace(_nameSearch) && !s.Name.ToLower().Contains(_nameSearch.ToLower())) continue;

            grid.Add(g);
        }

        IemStationsListBox.ItemsSource = grid;
        _currentGrid = 1 - _currentGrid;
    }

    private void StidSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _stidSearch = ((TextBox)sender!).Text;
        UpdateStationList();
    }

    private void NameSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _nameSearch = ((TextBox)sender!).Text;
        UpdateStationList();
    }

    private void IemStationsListBox_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        var lb = (ListBox)sender!;
        if (lb.SelectedItem is not Grid g) return;
        var station = (IemStation)g.Tag!;
        SelectedStation = station;
        Close(SelectedStation);
    }
}
