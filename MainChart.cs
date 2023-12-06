using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScottPlot.Avalonia;

namespace csvplot;

public static class MainChart
{
    private static List<string> _sources = new List<string>();

    public static AvaPlot Plot = new();

    public static void AddSource(string source)
    {
        _sources.Add(source);
    }
}


public class SimpleDelimitedFile
{
    private readonly string _source;

    public SimpleDelimitedFile(string source)
    {
        _source = source;
    }

    public double[] GetData(string trend)
    {
        using FileStream stream = new FileStream(_source, FileMode.Open);
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