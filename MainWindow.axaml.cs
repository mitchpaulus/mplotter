using System.Drawing;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ScottPlot;
using ScottPlot.Avalonia;

namespace csvplot;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        double[] dataX = new double[] { 1, 2, 3, 4, 5 };
        double[] dataY = new double[] { 1, 4, 9, 16, 25 };

        AvaPlot? plot = this.Find<AvaPlot>("AvaPlot");

        if (plot is not null)
        {
            // Middle click to reset axis limits
            var scatter = plot.Plot.Add.Scatter(dataX, dataY);
            plot.Plot.XLabel("OAT");
            plot.Refresh();
        }
    }

    private FilePickerFileType _dateFileTypes = new FilePickerFileType("Data Files")
    {
        Patterns = new[] { "*.csv", "*.tsv", "*.txt" }
    };

    private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
    {
        // Use StorageProvider to show the dialog
        var result = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            FileTypeFilter = new []{ _dateFileTypes }
        } );

        if (result.Any())
        {
            // Get the selected file path
            IStorageFile? filePath = result.FirstOrDefault();

            if (filePath != default(IStorageFile?))
            {
                MainChart.AddSource(filePath.Path.AbsolutePath);
            }
            // Handle the file path (e.g., updating the ViewModel)
        }
    }
}