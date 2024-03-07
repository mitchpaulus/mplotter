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
            if (_source.Contains("\\"))
            {
                return _source.Split('\\').Last();
            }

            if (_source.Contains('/'))
            {
                return _source.Split('/').Last();
            }

            return _source;
        }
    }

    private readonly List<string> _trends;

    private readonly string _source;

    public SimpleDelimitedFile(string source)
    {
        _source = source;
        using FileStream stream = new(source, FileMode.Open);
        var sr = new StreamReader(stream);
        var headerLine = sr.ReadLine();

        if (headerLine is not null)
        {
            if (source.ToLowerInvariant().EndsWith(".tsv") || headerLine.Contains('\t'))
            {
                string[] splitHeader = headerLine.Split('\t');
                _trends = splitHeader.Skip(1).ToList();
                _delimiter = '\t';
            }
            else if (source.ToLowerInvariant().EndsWith(".csv"))
            {
                string[] splitHeader = headerLine.Split(',');
                _trends = splitHeader.Skip(1).ToList();
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
        using FileStream stream = new FileStream(_source, FileMode.Open);
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

        using FileStream stream = new FileStream(_source, FileMode.Open);
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

        var data = new TimestampData(dateTimes, values);
        data.Sort();
        return data;
    }

    public List<TimestampData> GetTimestampData(List<string> trends) => trends.Select(GetTimestampData).ToList();

    public List<TimestampData> GetTimestampData(List<string> trends, DateTime startDate, DateTime endDate)
    {
        List<TimestampData> data = new();
        foreach (var trend in trends)
        {
            TimestampData tsData = GetTimestampData(trend);
            tsData.TrimDates(startDate, endDate);
            data.Add(tsData);
        }

        return data;
    }

    public string Header => ShortName;
}
