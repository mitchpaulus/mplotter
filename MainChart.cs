using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using ScottPlot.Avalonia;

namespace csvplot;

public static class MainChart
{
}

public interface IDataSource
{
    List<string> Trends { get; }

    double[] GetData(string trend);

    string Header { get; }

    string ShortName { get; }
}


public static class Extensions
{
    public static IEnumerable<string?> SplitLines(this string input)
    {
        using StringReader sr = new StringReader(input);
        while (sr.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}