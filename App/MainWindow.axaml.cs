using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ScottPlot;
using ScottPlot.Avalonia;

namespace csvplot;


public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();

        AvaPlot? plot = this.Find<AvaPlot>("AvaPlot");

        _vm = new MainViewModel(plot!, StorageProvider, this);
        DataContext = _vm;

        InitMrus();
    }

    public void InitMrus()
    {
        var mrus = MostRecentlyUsedFiles();

        var buttons = mrus.Select(mru =>
            {
                // Add a button to load that file
                Button button = new Button();
                IDataSource source = DataSourceFactory.SourceFromLocalPath(mru);
                button.Click += async (sender, args) =>
                {
                    await _vm.AddDataSource(source);
                };
                button.Content = Path.GetFileName(mru);

                var tooltip = new TextBlock { Text = mru };
                ToolTip.SetTip(button, tooltip);
                return button;
            }
        );

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
        await _vm.AddDataSource(new InfluxDataSource("511 John Carpenter"));
    }

    private void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        _vm.UpdateTrendFilter(((TextBox)sender).Text ?? "");
    }
}

public static class MonthNames
{
    public static readonly List<string> Names = new()
    {
        "January",
        "February",
        "March",
        "April",
        "May",
        "June",
        "July",
        "August",
        "September",
        "October",
        "November",
        "December"
    };
}
