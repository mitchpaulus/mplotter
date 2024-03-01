using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;
using ScottPlot.Statistics;

namespace csvplot;

public class MainViewModel : INotifyPropertyChanged
{
    // public readonly AvaPlot AvaPlot;
    private readonly IStorageProvider _storageProvider;
    private readonly MainWindow _window;

    private readonly UnitConverterReader _unitConverterReader = new();
    private readonly UnitReader _unitReader = new();

    public readonly DateTimeState EndDateLocal;
    public readonly DateTimeState StartDateLocal;

    private ComputedDateTimeState _computedDateTime;
    private readonly ComputedDateTimeState _secondComputedDateTime;

    private readonly List<IDataSource> _dataSources = new();

    public MainViewModel(AvaPlot avaPlot, IStorageProvider storageProvider, MainWindow window)
    {
        // AvaPlot = avaPlot;
        _storageProvider = storageProvider;
        _window = window;
        _unitConverterReader.Read();
        _unitReader.Read();

        EndDateLocal = new DateTimeState(DateTime.Now, (newVal) =>
        {
            window.EndDateTextBlock.Text = $"End Date: {newVal:yyyy-MM-dd}";
        }, this);

        StartDateLocal = new DateTimeState(EndDateLocal.Value.AddDays(-7), newVal =>
        {
            window.StartDateTextBlock.Text = $"Start Date: {newVal:yyyy-MM-dd}";
        }, this);

        _computedDateTime = new ComputedDateTimeState(model => model.EndDateLocal.Value.AddDays(10), time =>
        {
            window.ComputedDateTextBlock.Text = $"Computed Date: {time:yyyy-MM-dd}";
        }, this);

        _secondComputedDateTime = new ComputedDateTimeState(model => model._computedDateTime.Value.AddDays(10), time =>
        {
            window.ChainedComputedDate.Text = $"Chained Computed Date: {time:yyyy-MM-dd}";
        }, this);


        EndDateLocal.AddSubscriber(_computedDateTime);
        _computedDateTime.AddSubscriber(_secondComputedDateTime);

        IgnoreMonday2 = new UiState<bool>(false, b => { UpdatePlots(); }, this);
        IgnoreTuesday2 = new UiState<bool>(false, b => { UpdatePlots(); }, this);
        IgnoreWednesday2 = new UiState<bool>(false, b => { UpdatePlots(); }, this);
        IgnoreThursday2 = new UiState<bool>(false, b => { UpdatePlots(); }, this);
        IgnoreFriday2 = new UiState<bool>(false, b => { UpdatePlots(); }, this);
        IgnoreSaturday2 = new UiState<bool>(false, b => { UpdatePlots(); }, this);
        IgnoreSunday2 = new UiState<bool>(false, b => { UpdatePlots(); }, this);
    }

    public UiState<bool> IgnoreMonday2 { get; set; }
    public UiState<bool> IgnoreTuesday2 { get; set; }
    public UiState<bool> IgnoreWednesday2 { get; set; }
    public UiState<bool> IgnoreThursday2 { get; set; }
    public UiState<bool> IgnoreFriday2 { get; set; }
    public UiState<bool> IgnoreSaturday2 { get; set; }
    public UiState<bool> IgnoreSunday2 { get; set; }


    public void UpdatePlots()
    {
        _window.PlotStackPanel.Children.Clear();

        // var unitGrouped = SourceTrendPairs.GroupBy(pair => pair.Name.GetUnit());

        var unitGrouped = _window.SelectedTimeSeriesTrends.GroupBy(config => config.TrendName.GetUnit());

        List<AvaPlot> plots = new();

        Stopwatch watch = new Stopwatch();
        foreach (var unitGroup in unitGrouped)
        {
            var plot = new AvaPlot();

            foreach ((PlotTrendConfig sourcePair, int trendIndex) in unitGroup.WithIndex())
            {
                 var t = sourcePair.TrendName;
                 var source = sourcePair.DataSource;

                 if (_window.Mode == PlotMode.Ts)
                 {
                     plot.Plot.Axes.DateTimeTicksBottom();
                     watch.Restart();
                     var tsData = source.GetTimestampData(t);
                     watch.Stop();

                     Console.Write($"{watch.ElapsedMilliseconds}\n");

                     // Bail if we messed up.
                     if (!tsData.LengthsEqual) continue;

                     // double[] yData = tsData.Values.ToArray();
                     List<double> yData = tsData.Values;

                     // bool didConvert = false;
                     // if (_unitConverterReader.TryGetConversion(t.Name, _unitReader, out Unit? unitToConvertTo))
                     // {
                     //     foreach ((var unitTypeKey, Dictionary<string, Unit> associatedUnits) in _units)
                     //     {
                     //         if (associatedUnits.TryGetValue(unit, out Unit? fromUnit) && associatedUnits.TryGetValue(unitToConvertTo, out var toUnit))
                     //         {
                     //             yData = yData.Select(d =>
                     //                     d * fromUnit.Factor / toUnit.Factor)
                     //                     .ToArray();
                     //             didConvert = true;
                     //             break;
                     //         }
                     //     }
                     // }

                     string label = unitGroup.Count(tuple => tuple.TrendName == t) < 2 ? t : $"{source.ShortName}: {t}";
                     // if (didConvert) label = $"{label} in {unitToConvertTo}";

                     if (source.DataSourceType == DataSourceType.EnergyModel || tsData.Values.Count == 8760)
                     {
                         var signalPlot = plot.Plot.Add.Signal(yData, (double)1/24);
                         signalPlot.Label = label;
                         signalPlot.Data.XOffset = tsData.DateTimes.First().ToOADate();
                     }
                     else
                     {
                         List<double> xData = tsData.DateTimes.Select(time => time.ToOADate()).ToList();
                         // TODO: add safety here
                         // DateTime dateTimeStart = new DateTime(DateTime.Now.Year, 1, 1);
                         var scatter = plot.Plot.Add.Scatter(xData, yData);
                         scatter.Label = label;
                     }
                 }
                 else if (_window.Mode == PlotMode.Histogram)
                 {
                     var data = source.DataSourceType == DataSourceType.NonTimeSeries
                         ? source.GetData(t)
                         : source.GetTimestampData(t).Values;

                     (double min, double max) = data.SafeMinMax();

                     var hist = new Histogram(min, max, 50);
                     hist.AddRange(data);

                     var values = hist.Counts;
                     var binCenters = hist.BinCenters;

                     List<Bar> allBars = new(binCenters.Length);

                     var colors = ColorCycle.GetColors(unitGroup.Count());

                     for (int i = 0; i < values.Length; i++)
                     {
                         allBars.Add(new Bar
                         {
                             Value = values[i],
                             Position = binCenters[i],
                             Size = hist.BinSize,
                             FillColor = colors[trendIndex]
                         });
                     }

                     plot.Plot.Add.Bars(allBars);
                     plot.Plot.Legend.ManualItems.Add(new LegendItem
                     {
                         Label = t,
                         Line  = LineStyle.None,
                         Marker = new MarkerStyle { Shape = MarkerShape.FilledSquare },
                         FillColor = colors[trendIndex]
                     });
                 }
            }


            if (_window.Mode == PlotMode.Histogram)
            {
                plot.Plot.Axes.Left.Label.Text = "Count";
                plot.Plot.Axes.Bottom.Label.Text = unitGroup.Count() == 1 ? unitGroup.First().TrendName : unitGroup.Key ?? "";
            }
            else
            {
                plot.Plot.Axes.Left.Label.Text = unitGroup.Count() == 1 ? unitGroup.First().TrendName : unitGroup.Key ?? "";
            }

            plot.Plot.Axes.AutoScale();
            plot.Plot.Legend.IsVisible = unitGroup.Count() > 1;
            plot.Plot.Legend.Location = Alignment.UpperRight;

            plots.Add(plot);
        }

        _window.PlotStackPanel.RowDefinitions.Clear();
        for (var index = 0; index < plots.Count; index++)
        {
            var p = plots[index];
            RowDefinition r = new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star),
                MinHeight = 100
            };
            _window.PlotStackPanel.RowDefinitions.Add(r);
            Grid.SetRow(p, index);
        }

        _window.PlotStackPanel.Children.AddRange(plots);

        foreach (var p in plots)
        {
            p.Refresh();
        }
    }

    private List<AvaPlot> AllPlots()
    {
        return _window.PlotStackPanel.Children.Where(control => control is AvaPlot).Cast<AvaPlot>().ToList();
    }

    private void FixMonth(int month)
    {
        if (month is < 1 or > 12) throw new ArgumentException("Month should be passed as 1-12");
        int currentYear = DateTime.Now.Year;
        foreach (var p in AllPlots())
        {
            if (month < 12)
            {
                p.Plot.Axes.Bottom.Min = new DateTime(currentYear, month, 1).ToOADate();
                p.Plot.Axes.Bottom.Max = new DateTime(currentYear, month + 1, 1).ToOADate();
                StartDateLocal.Update(new DateTime(currentYear, month, 1));
                EndDateLocal.Update(new DateTime(currentYear, month + 1, 1));
            }
            else
            {
                p.Plot.Axes.Bottom.Min = new DateTime(currentYear, month, 1).ToOADate();
                p.Plot.Axes.Bottom.Max = new DateTime(currentYear + 1, 1, 1).ToOADate();
                StartDateLocal.Update(new DateTime(currentYear, month, 1));
                EndDateLocal.Update(new DateTime(currentYear + 1, 1, 1));
            }

            p.Plot.Axes.Bottom.Label.Text = MonthNames.Names[month - 1];
            p.Refresh();
        }

        if (month < 12)
        {
            StartDateLocal.Update(new DateTime(currentYear, month, 1));
            EndDateLocal.Update(new DateTime(currentYear, month + 1, 1));
        }
        else
        {
            StartDateLocal.Update(new DateTime(currentYear, month, 1));
            EndDateLocal.Update(new DateTime(currentYear + 1, 1, 1));
        }

    }

    public void MakeJan() => FixMonth(1);
    public void MakeFeb() => FixMonth(2);

    public void MakeMar() => FixMonth(3);

    public void MakeApr() => FixMonth(4);

    public void MakeMay() => FixMonth(5);

    public void MakeJun() => FixMonth(6);

    public void MakeJul() => FixMonth(7);


    public void MakeAug() => FixMonth(8);

    public void MakeSep() => FixMonth(9);


    public void MakeOct() => FixMonth(10);


    public void MakeNov() => FixMonth(11);

    public void MakeDec() => FixMonth(12);

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

    public static async Task SaveToMru(string localPath)
    {
        // Save to MRU file list (up to 20)
        if (OperatingSystem.IsWindows())
        {
            string? localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (localAppData is not null)
            {
                int tries = 0;
                while (tries < 3)
                {
                    try
                    {
                        Directory.CreateDirectory($"{localAppData}\\mplotter");
                        var path = $"{localAppData}\\mplotter\\mru.txt";

                        List<string> lines;
                        try
                        {
                            lines = (await File.ReadAllLinesAsync(path)).ToList();
                        }
                        catch (FileNotFoundException)
                        {
                            lines = new List<string>();
                        }

                        var newLines = new List<string>();
                        newLines.Add(localPath);

                        foreach (var line in lines)
                        {
                            if (newLines.Contains(line)) continue;
                            newLines.Add(line);
                        }

                        await File.WriteAllLinesAsync(path, newLines, new UTF8Encoding(false));
                        break;
                    }
                    catch (Exception)
                    {
                        tries++;
                    }
                }
            }
        }
    }
}
