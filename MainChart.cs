using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using ScottPlot.Avalonia;

namespace csvplot;

public static class MainChart
{
}


public class SimpleDelimitedFile : IDataSource
{
    public List<string> Trends { get; }

    public SimpleDelimitedFile(string source)
    {
        Header = source;
        using FileStream stream = new(Header, FileMode.Open);
        var sr = new StreamReader(stream);
        var headerLine = sr.ReadLine();

        if (headerLine is not null)
        {
            string[] splitHeader = headerLine.Split('\t');
            Trends = splitHeader.ToList();
        }
        else
        {
            Trends = Array.Empty<string>().ToList();
        }
    }

    public double[] GetData(string trend)
    {
        using FileStream stream = new FileStream(Header, FileMode.Open);
        var sr = new StreamReader(stream);
        var headerLine = sr.ReadLine();

        if (headerLine is null) return Array.Empty<double>();

        string[] splitHeader = headerLine.Split('\t');
        int col = -1;

        for (int i = 0; i < splitHeader.Length; i++)
        {
            if (trend == splitHeader[i]) col = i;
        }

        List<double> values = new();
        while (sr.ReadLine() is { } line)
        {
            string[] splitLine = line.Split('\t');
            values.Add(double.Parse(splitLine[col]));
        }

        return values.ToArray();
    }

    public string Header { get; }
}

public interface IDataSource
{
    List<string> Trends { get; } 

    double[] GetData(string trend);
    
    string Header { get; }
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