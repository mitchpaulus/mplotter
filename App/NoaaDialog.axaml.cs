using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace csvplot;

public partial class NoaaDialog : Window
{
    private readonly List<Grid> _allGrids = new();

    private readonly List<Grid> _grids1 = new();
    private readonly List<Grid> _grids2 = new();

    private int _currentGrid = 1;
    private List<NoaaStation> _stations; // Initialized in AddStations.

    private string? _usafSearch = "";
    private string? _wbanSearch = "";
    private string? _stationNameSearch = "";
    private string? _callNumSearch = "";
    private string? _stateSearch = "";

    public NoaaDialog()
    {
        InitializeComponent();
    }

    public void UpdateStationList()
    {
        var grid = _currentGrid == 1 ? _grids2 : _grids1;

        grid.Clear();
        foreach (var g in _allGrids)
        {
            NoaaStation s = (NoaaStation)g.Tag!;

            if (!string.IsNullOrWhiteSpace(_usafSearch)        && !s.Usaf.ToLower().Contains(_usafSearch.ToLower())) continue;
            if (!string.IsNullOrWhiteSpace(_wbanSearch)        && !s.Wban.ToLower().Contains(_wbanSearch.ToLower())) continue;
            if (!string.IsNullOrWhiteSpace(_stationNameSearch) && !s.StationName.ToLower().Contains(_stationNameSearch.ToLower())) continue;
            if (!string.IsNullOrWhiteSpace(_callNumSearch)     && !s.Icao.ToLower().Contains(_callNumSearch.ToLower())) continue;
            if (!string.IsNullOrWhiteSpace(_stateSearch)       && !s.St.ToLower().Contains(_stateSearch.ToLower())) continue;

            grid.Add(g);
        }

        NoaaStationsListBox.ItemsSource = grid;
        _currentGrid = 1 - _currentGrid;
    }

    public async Task AddStations()
    {
        _stations = await NoaaWeather.GetStations();

        var usStations = _stations
            .Where(s => s.Ctry == "US")
            .Where(s => s.End > DateTime.Now.AddYears(-4))
            .ToList();

        // For each station, add a single row horizontal grid with the properties of the station.
        _allGrids.Clear();
        foreach (var station in usStations)
        {
            var row = new Grid();

            row.ColumnDefinitions.Add(new ColumnDefinition(100, GridUnitType.Pixel)); // USAF
            row.ColumnDefinitions.Add(new ColumnDefinition(100, GridUnitType.Pixel)); // WBAN
            row.ColumnDefinitions.Add(new ColumnDefinition(400, GridUnitType.Pixel)); // StationName
            row.ColumnDefinitions.Add(new ColumnDefinition(75, GridUnitType.Pixel)); // Ctry
            row.ColumnDefinitions.Add(new ColumnDefinition(75, GridUnitType.Pixel)); // St
            row.ColumnDefinitions.Add(new ColumnDefinition(100, GridUnitType.Pixel)); // Icao
            row.ColumnDefinitions.Add(new ColumnDefinition(125, GridUnitType.Pixel)); // Lat
            row.ColumnDefinitions.Add(new ColumnDefinition(125, GridUnitType.Pixel)); // Lon
            row.ColumnDefinitions.Add(new ColumnDefinition(100, GridUnitType.Pixel)); // Elev
            row.ColumnDefinitions.Add(new ColumnDefinition(125, GridUnitType.Pixel)); // Begin
            row.ColumnDefinitions.Add(new ColumnDefinition(125, GridUnitType.Pixel)); // End

            // // 11 columns
            // for (int i = 0; i < 11; i++)
            // {
            //     row.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            // }

            // public string Usaf { get; set; }
            // public string Wban { get; set; }
            // public string StationName { get; set; }
            // public string Ctry { get; set; }
            // public string St { get; set; }
            // public string Icao { get; set; }
            // public double? Lat { get; set; }
            // public double? Lon { get; set; }
            // public double? Elev { get; set; }
            // public DateTime Begin { get; set; }
            // public DateTime End { get; set; }

            var usaf = new TextBlock();
            usaf.Text = station.Usaf;

            var wban = new TextBlock();
            wban.Text = station.Wban;

            var stationName = new TextBlock();
            stationName.Text = station.StationName;

            var ctry = new TextBlock();
            ctry.Text = station.Ctry;

            var st = new TextBlock();
            st.Text = station.St;

            var icao = new TextBlock();
            icao.Text = station.Icao;

            var lat = new TextBlock();
            lat.Text = station.Lat.ToString();

            var lon = new TextBlock();
            lon.Text = station.Lon.ToString();

            var elev = new TextBlock();
            elev.Text = station.Elev.ToString();

            var begin = new TextBlock();
            begin.Text = station.Begin.ToString("yyyy-MM-dd");

            var end = new TextBlock();
            end.Text = station.End.ToString("yyyy-MM-dd");

            row.Children.Add(usaf);
            row.Children.Add(wban);
            row.Children.Add(stationName);
            row.Children.Add(ctry);
            row.Children.Add(st);
            row.Children.Add(icao);
            row.Children.Add(lat);
            row.Children.Add(lon);
            row.Children.Add(elev);
            row.Children.Add(begin);
            row.Children.Add(end);

            Grid.SetColumn(usaf, 0);
            Grid.SetColumn(wban, 1);
            Grid.SetColumn(stationName, 2);
            Grid.SetColumn(ctry, 3);
            Grid.SetColumn(st, 4);
            Grid.SetColumn(icao, 5);
            Grid.SetColumn(lat, 6);
            Grid.SetColumn(lon, 7);
            Grid.SetColumn(elev, 8);
            Grid.SetColumn(begin, 9);
            Grid.SetColumn(end, 10);

            row.Tag = station;

            _allGrids.Add(row);
        }

        _grids1.Clear();
        _grids1.AddRange(_allGrids);

        NoaaStationsListBox.ItemsSource = _grids1;
        _currentGrid = 1;
    }

    private void UsafSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _usafSearch = ((TextBox)sender!).Text;
        UpdateStationList();
    }

    private void WbanSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _wbanSearch = ((TextBox)sender!).Text;
        UpdateStationList();
    }

    private void StationNameSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _stationNameSearch = ((TextBox)sender!).Text;
        UpdateStationList();
    }

    private void CallNumSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _callNumSearch = ((TextBox)sender!).Text;
        UpdateStationList();
    }

    private void StateSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _stateSearch = ((TextBox)sender!).Text;
        UpdateStationList();
    }
}
