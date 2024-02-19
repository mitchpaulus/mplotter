using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace csvplot;

public class SimpleDelimitedFile : IDataSource
{
    public Task<List<string>> Trends() => Task.FromResult(_trends);

    private readonly char _delimiter = '\t';

    public DataSourceType DataSourceType { get; }

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

    private readonly List<string> _trends;

    public SimpleDelimitedFile(string source)
    {
        Header = source;
        using FileStream stream = new(Header, FileMode.Open);
        var sr = new StreamReader(stream);
        var headerLine = sr.ReadLine();

        if (headerLine is not null)
        {
            if (source.ToLowerInvariant().EndsWith(".tsv") || headerLine.Contains('\t'))
            {
                string[] splitHeader = headerLine.Split('\t');
                _trends = splitHeader.ToList();
                _delimiter = '\t';
            }
            else if (source.ToLowerInvariant().EndsWith(".csv"))
            {
                string[] splitHeader = headerLine.Split(',');
                _trends = splitHeader.ToList();
                _delimiter = ',';
            }
            else
            {
                _trends = Array.Empty<string>().ToList();
            }
        }
        else
        {
            _trends = Array.Empty<string>().ToList();
        }

        var nextLine = sr.ReadLine();
        if (nextLine is not null)
        {
            var splitLine = nextLine.Split(_delimiter);
            // Try to read first column as a DateTime
            if (DateTime.TryParse(splitLine[0], out _))
            {
                DataSourceType = DataSourceType.TimeSeries;
            }
            else
            {
                int rows = 1;
                while (sr.ReadLine() is not null)
                {
                    rows++;
                }

                DataSourceType = rows == 8760 ? DataSourceType.EnergyModel : DataSourceType.NonTimeSeries;
            }
        }
        else
        {
            DataSourceType = DataSourceType.NonTimeSeries;
        }
    }

    public List<double> GetData(string trend)
    {
        using FileStream stream = new FileStream(Header, FileMode.Open);
        var sr = new StreamReader(stream);
        var headerLine = sr.ReadLine();

        if (headerLine is null) return new List<double>();

        string[] splitHeader = headerLine.Split(_delimiter);
        int col = -1;

        for (int i = 0; i < splitHeader.Length; i++)
        {
            if (trend == splitHeader[i]) col = i;
        }

        List<double> values = new();
        while (sr.ReadLine() is { } line)
        {
            string[] splitLine = line.Split(_delimiter);
            if (double.TryParse(splitLine[col], out var val))
            {
                values.Add(val);
            }
        }

        return values;
    }

    public TimestampData GetTimestampData(string trend)
    {
        if (DataSourceType == DataSourceType.NonTimeSeries) return new TimestampData(new(), new());

        using FileStream stream = new FileStream(Header, FileMode.Open);
        var sr = new StreamReader(stream);
        var headerLine = sr.ReadLine();

        if (headerLine is null) return new TimestampData(new(), new());

        string[] splitHeader = headerLine.Split(_delimiter);
        int col = -1;

        for (int i = 0; i < splitHeader.Length; i++)
        {
            if (trend == splitHeader[i]) col = i;
        }

        List<DateTime> dateTimes = new();
        List<double> values = new();

        if (DataSourceType == DataSourceType.EnergyModel)
        {
             DateTime date = new DateTime(DateTime.Now.Year, 1, 1);
             while (sr.ReadLine() is { } line)
             {
                 string[] splitLine = line.Split(_delimiter);
                 if (col >= splitLine.Length) continue;
                 if (!double.TryParse(splitLine[col], out var d)) continue;

                 dateTimes.Add(date);
                 date = date.AddHours(1);
                 values.Add(d);
             }
        }
        else if (DataSourceType == DataSourceType.TimeSeries)
        {
            while (sr.ReadLine() is { } line)
            {
                string[] splitLine = line.Split(_delimiter);
                if (!DateTime.TryParse(splitLine[0], out var dt)) continue;
                if (col >= splitLine.Length) continue;

                if (!double.TryParse(splitLine[col], out var d)) continue;

                dateTimes.Add(dt);
                values.Add(d);
            }
        }

        return new TimestampData(dateTimes, values);
    }

    public string Header { get; }
}
