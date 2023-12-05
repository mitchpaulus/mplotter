using System.Drawing;
using Avalonia.Controls;
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
}