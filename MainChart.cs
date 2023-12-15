using System;
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

    DataSourceType DataSourceType { get; }

    TimestampData GetTimestampData(string trend);
}

public class TimestampData
{
    public readonly List<DateTime> DateTimes;
    public readonly List<double> Values;

    public TimestampData(List<DateTime> dateTimes, List<double> values)
    {
        DateTimes = dateTimes;
        Values = values;
    }

    public bool LengthsEqual => DateTimes.Count == Values.Count;
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