using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace csvplot;

public class SimpleDelimitedFile : IDataSource
{
    public List<string> Trends { get; }

    public string ShortName
    {
        get
        {
            // Assume Windows Path
            if (Header.Contains("\\"))
            {
                return Header.Split('\\').Last();
            }

            if (Header.Contains('/'))
            {
                return Header.Split('/').Last();
            }

            return Header;
        }
    }

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