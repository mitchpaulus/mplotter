using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace csvplot;

public partial class NoaaDialog : Window
{
    public NoaaDialog()
    {
        InitializeComponent();
    }


    public async Task AddStations()
    {
        List<NoaaStation> stations = await NoaaWeather.GetStations();

        var usStations = stations.Where(s => s.Ctry == "US").ToList();
        // For each station, add a single row horizontal grid with the properties of the station.

        var grids = new List<Grid>();
        foreach (var station in usStations)
        {
            var row = new Grid();

            // 11 columns
            for (int i = 0; i < 11; i++)
            {
                row.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            }

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
            begin.Text = station.Begin.ToString();

            var end = new TextBlock();
            end.Text = station.End.ToString();

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

            grids.Add(row);

        }

        NoaaStationsListBox.ItemsSource = grids;
    }
}
