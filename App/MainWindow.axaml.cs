using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Platform.Storage;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Domain;
using ScottPlot;
using ScottPlot.Avalonia;
using Brushes = Avalonia.Media.Brushes;
using File = System.IO.File;

namespace csvplot;


public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    private List<XySerie> _xySeries = new();

    private readonly List<IDataSource> _loadedDataSources = new();

    private readonly List<IDataSource> _selectedDataSources = new();

    private void RenderDataSources()
    {
        DataSourcesList.Children.Clear();

        foreach (var dataSource in _loadedDataSources)
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            g.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            Button b = new Button()
            {
                Content = dataSource.ShortName,
                Tag = dataSource,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background =  _selectedDataSources.Contains(dataSource) ? Brushes.LightBlue : Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };

            b.Click += ClickSource;

            Grid.SetColumn(b, 0);

            Button removeButton = new Button()
            {
                Content = "Remove",
                Tag = dataSource,
            };
            Grid.SetColumn(removeButton, 1);

            g.Children.Add(b);
            g.Children.Add(removeButton);

            removeButton.Click += RemoveDataSource;
            DataSourcesList.Children.Add(g);
        }
    }

    private void ClickSource(object? sender, EventArgs args)
    {
        if (sender is not Button b) return;

        var source = (IDataSource)b.Tag!;

        var removed = _selectedDataSources.Remove(source);
        if (!removed) _selectedDataSources.Add(source);

        RenderDataSources();
    }

    public void AddDataSource(IDataSource source)
    {
        // Don't add dup
        foreach (var s in _loadedDataSources)
        {
            if (s.GetType() != source.GetType()) continue;
            if (s.Header == source.Header) return;
        }

        _loadedDataSources.Add(source);
        _selectedDataSources.Add(source);
        RenderDataSources();
    }

    private void RemoveDataSource(object? sender, EventArgs args)
    {
        if (sender is not Button b) return;

        var source = (IDataSource)b.Tag!;
        _loadedDataSources.Remove(source);
        _selectedDataSources.Remove(source);

        foreach (var c in DataSourcesList.Children)
        {
            if (c is not Grid g) continue;
            if (!g.Children.Any(control => ReferenceEquals(control, b))) continue;

            DataSourcesList.Children.Remove(c);
            break;
        }
    }


    public MainWindow()
    {
        InitializeComponent();

        AvaPlot? plot = this.Find<AvaPlot>("AvaPlot");

        _vm = new MainViewModel(plot!, StorageProvider, this);
        DataContext = _vm;

        UpdateMrus();
    }

    public void UpdateMrus()
    {
        List<string> mrus = MostRecentlyUsedFiles();

        var buttons = mrus.Select(mru =>
            {
                // Add a button to load that file
                Button button = new Button();
                IDataSource source = DataSourceFactory.SourceFromLocalPath(mru);
                button.Click += async (sender, args) =>
                {
                    await _vm.AddDataSource(source);
                };
                button.Content = Path.GetFileName(mru).EscapeUiText();

                var tooltip = new TextBlock { Text = mru };
                ToolTip.SetTip(button, tooltip);
                return button;
            }
        );

        MruPanel.Children.Clear();
        MruPanel.Children.AddRange(buttons);
    }

    public static List<string> MostRecentlyUsedFiles()
    {
        string? localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (localAppData is null) return new List<string>();

        string[] lines;
        try
        {
            lines = File.ReadAllLines($"{localAppData}/mplotter/mru.txt");
        }
        catch
        {
            return new List<string>();
        }

        return lines.Where(File.Exists).Order().ToList();
    }

    public static readonly FilePickerFileType DateFileTypes = new("Data Files")
    {
        Patterns = new[] { "*.csv", "*.tsv", "*.txt", "*.sql", "*.db" }
    };

    private void InputElement_OnKeyDown(object? sender, KeyEventArgs e)
    {
        // On '/', move focus to 'SearchBox'
        if (e.Key == Key.Oem2) // Oem2 is often used for '/'
        {
            // Check if the currently focused control is the search textbox
            if (FocusManager != null && (FocusManager.GetFocusedElement() is not TextBox currentTextBox ||
                                         currentTextBox.Name != "SearchBox"))
            {
                SearchBox.Focus();
                e.Handled = true; // Mark the event as handled to prevent further processing
            }
        }
        else if (e.Key == Key.Escape)
        {
            SearchBox.Clear();
            SearchBox.Focus();
        }
        else if (e.Key == Key.Left)
        {
            // Check that a text box is currently not focused
            if (FocusManager == null || FocusManager.GetFocusedElement() is TextBox) return;

            foreach (var child in PlotStackPanel.Children)
            {
                if (child is not AvaPlot avaPlot) continue;

                // Not a date
                if (avaPlot.Plot.Axes.Bottom.Min < new DateTime(1960, 1, 1).ToOADate()) return;

                var minOaDate = DateTime.FromOADate(avaPlot.Plot.Axes.Bottom.Min);
                var maxOaDate = DateTime.FromOADate(avaPlot.Plot.Axes.Bottom.Max);

                if (minOaDate.Day == 1 && maxOaDate.Day == 1)
                {
                    var monthDiff = (maxOaDate.Year * 12 + maxOaDate.Month) - (minOaDate.Year * 12 + minOaDate.Month);

                    if (monthDiff > 0)
                    {
                        var newMinDate = minOaDate.AddMonths(-monthDiff);
                        var newMaxDate = maxOaDate.AddMonths(-monthDiff);

                        avaPlot.Plot.Axes.SetLimitsX(newMinDate.ToOADate(), newMaxDate.ToOADate());

                        if (monthDiff == 1)
                        {
                            avaPlot.Plot.Axes.Bottom.Label.Text = MonthNames.Names[newMinDate.Month - 1];
                        }

                        avaPlot.Refresh();
                    }
                }
            }
        }
        else if (e.Key == Key.Right)
        {
            // Check that a text box is currently not focused
            if (FocusManager == null || FocusManager.GetFocusedElement() is TextBox) return;

            foreach (var child in PlotStackPanel.Children)
            {
                if (child is not AvaPlot avaPlot) continue;

                // Not a date
                if (avaPlot.Plot.Axes.Bottom.Min < new DateTime(1960, 1, 1).ToOADate()) return;

                var minOaDate = DateTime.FromOADate(avaPlot.Plot.Axes.Bottom.Min);
                var maxOaDate = DateTime.FromOADate(avaPlot.Plot.Axes.Bottom.Max);

                if (minOaDate.Day == 1 && maxOaDate.Day == 1)
                {
                    var monthDiff = (maxOaDate.Year * 12 + maxOaDate.Month) - (minOaDate.Year * 12 + minOaDate.Month);

                    if (monthDiff > 0)
                    {
                        var newMinDate = minOaDate.AddMonths(monthDiff);
                        var newMaxDate = maxOaDate.AddMonths(monthDiff);

                        avaPlot.Plot.Axes.SetLimitsX(newMinDate.ToOADate(), newMaxDate.ToOADate());

                        if (monthDiff == 1)
                        {
                            avaPlot.Plot.Axes.Bottom.Label.Text = MonthNames.Names[newMinDate.Month - 1];
                        }

                        avaPlot.Refresh();
                    }
                }
            }
        }
    }

    private void ClearSelections(object? sender, RoutedEventArgs e) => _vm.ClearSelected();

    private async void InfluxButtonClick(object? sender, RoutedEventArgs e)
    {
        // await _vm.AddDataSource(new InfluxDataSource("511 John Carpenter"));

        var influxDialog = new InfluxDialog();
        influxDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        influxDialog.Width = 500;
        influxDialog.Height = 1000;

        InfluxEnv env = InfluxDataSource.GetEnv();
        if (!env.IsValid()) return;

        // Get available buckets

        try
        {
            var client = new InfluxDBClient(env.InfluxHost, env.InfluxToken);
            var api = client.GetBucketsApi();

            var buckets = new List<string>();
            var offset = 0;

            while (true)
            {
                List<Bucket>? bucketResponse = await api.FindBucketsAsync(limit: 100, offset: offset);

                if (bucketResponse is null) break;

                buckets.AddRange(bucketResponse.Select(bucket => bucket.Name));
                if (bucketResponse.Count < 100) break;
                offset += 100;
            }

            influxDialog.InfluxBucketListBox.ItemsSource = buckets.Order().ToList();

            var selected = await influxDialog.ShowDialog<string>(this);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                await _vm.AddDataSource(new InfluxDataSource(selected));
            }
        }
        catch
        {
            // Empty
        }
    }

    private void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        _vm.UpdateTrendFilter(((TextBox)sender!).Text ?? "");
    }

    private void ExportButtonOnClick(object? sender, RoutedEventArgs e)
    {
        if (ExportStartYearComboBox.SelectedItem is not ComboBoxItem { Content: string startYearString }) return;
        if (ExportStartMonthComboBox.SelectedItem is not ComboBoxItem { Content: string startMonthString }) return;
        if (ExportStartDayComboBox.SelectedItem is not ComboBoxItem { Content: string startDayString }) return;
        if (ExportEndYearComboBox.SelectedItem is not ComboBoxItem { Content: string endYearString }) return;
        if (ExportEndMonthComboBox.SelectedItem is not ComboBoxItem { Content: string endMonthString }) return;
        if (ExportEndDayComboBox.SelectedItem is not ComboBoxItem { Content: string endDayString }) return;

        int startYear = int.Parse(startYearString);
        int startMonth = MonthNames.ShortNames.IndexOf(startMonthString) + 1;
        int startDay = int.Parse(startDayString);

        int endYear = int.Parse(endYearString);
        int endMonth = MonthNames.ShortNames.IndexOf(endMonthString) + 1;
        int endDay = int.Parse(endDayString);

        // Truncate day to max in the month
        startDay = Math.Min(startDay, MonthNames.DaysInMonth[startMonth - 1]);
        endDay = Math.Min(endDay, MonthNames.DaysInMonth[endMonth - 1]);

        DateTime start = new(startYear, startMonth, startDay);
        DateTime end = new(endYear, endMonth, endDay);

        foreach (var sourcePair in _vm.SourceTrendPairs.GroupBy(pair => pair.Source))
        {
            var trends = sourcePair.Select(pair => pair.Name).ToList();
            var output = sourcePair.Key.GetTimestampData(trends, start, end);
        }
    }

    private async void SelectTrendClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new TrendDialog(_loadedDataSources);
        await dialog.ShowDialog(this);
    }
}

public class PlotTrendConfig : IEquatable<PlotTrendConfig>
{
    public readonly IDataSource DataSource;
    public readonly string TrendName;

    public PlotTrendConfig(IDataSource dataSource, string trendName)
    {
        DataSource = dataSource;
        TrendName = trendName;
    }

    public bool Equals(PlotTrendConfig? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return DataSource.Equals(other.DataSource) && TrendName == other.TrendName;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((PlotTrendConfig)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(DataSource, TrendName);
    }

    public static bool operator ==(PlotTrendConfig? left, PlotTrendConfig? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(PlotTrendConfig? left, PlotTrendConfig? right)
    {
        return !Equals(left, right);
    }
}

public class XySerie
{
    public readonly PlotTrendConfig? XTrend;
    public readonly PlotTrendConfig? YTrend;

    public XySerie(PlotTrendConfig? xTrend, PlotTrendConfig? yTrend)
    {
        XTrend = xTrend;
        YTrend = yTrend;
    }
}

public static class MonthNames
{
    public static readonly List<string> Names = new() { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
    public static readonly List<string> ShortNames = new() { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
    public static readonly List<int> DaysInMonth = new() { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
}
