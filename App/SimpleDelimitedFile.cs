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

    private readonly Dictionary<string, List<string>> _cachedData = new();
    private readonly List<DateTime> _cachedParsedDateTimes = new();

    private bool _needsSort = false;

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
                var (splitHeader, success) = headerLine.TryParseCsvLine();
                _trends = success ? splitHeader.Skip(1).ToList() : Array.Empty<string>().ToList();
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

        try
        {
            // Fully resolve the directory path
            // var dir = Path.GetDirectoryName(_source);
            // if (dir is not null)
            // {
            //     var filename = Path.GetFileName(_source);
            //     FileSystemWatcher watcher = new FileSystemWatcher(dir, "");
            //     watcher.NotifyFilter = NotifyFilters.Attributes
            //                            | NotifyFilters.CreationTime
            //                            | NotifyFilters.DirectoryName
            //                            | NotifyFilters.FileName
            //                            | NotifyFilters.LastAccess
            //                            | NotifyFilters.LastWrite
            //                            | NotifyFilters.Security
            //                            | NotifyFilters.Size;
            //     watcher.Changed += WatcherOnChanged;
            //     watcher.Renamed += WatcherOnChanged;
            //     watcher.EnableRaisingEvents = true;
            // }
        }
        catch
        {
            // Ignored
        }

    }

    // private async void WatcherOnChanged(object sender, FileSystemEventArgs e)
    // {
    //     _cachedData.Clear();
    //     _cachedParsedDateTimes.Clear();
    //     _ = ReadAndCacheData();
    // }

    private async Task ReadAndCacheData()
    {
        try
        {
            await using FileStream stream = new FileStream(_source, FileMode.Open);
            var sr = new StreamReader(stream);

            var headerLine = await sr.ReadLineAsync();
            if (headerLine is null) return;

            Func<string, (List<string>, bool)> splitFunc = _delimiter == ',' ? Extensions.TryParseCsvLine : s => (s.Split(_delimiter).ToList(), true);

            var (splitHeader, headSuccess) = splitFunc(headerLine);
            if (!headSuccess) return;

            foreach (var head in splitHeader) _cachedData[head] = new List<string>();

            if (DataSourceType == DataSourceType.EnergyModel)
            {
                while (await sr.ReadLineAsync() is { } line)
                {
                    var (splitLine, _) = splitFunc(line);
                    for (int i = 0; i < Math.Min(splitLine.Count, splitHeader.Count); i++)
                    {
                        _cachedData[splitHeader[i]].Add(splitLine[i]);
                    }
                }

                DateTime startDateTime = new DateTime(DateTime.Now.Year, 1, 1);
                for (int i = 0; i < 8760; i++) _cachedParsedDateTimes.Add(startDateTime.AddHours(i));
            }
            else
            {
                DateTime prevDateTime = DateTime.MinValue;
                while (await sr.ReadLineAsync() is { } line)
                {
                    var (splitLine, success) = splitFunc(line);
                    if (!success) continue;

                    // string[] splitLine = line.Split(_delimiter);

                    if (!DateTime.TryParse(splitLine[0], out var parsedDateTime)) continue;
                    {
                        if (parsedDateTime < prevDateTime) _needsSort = true;
                        _cachedParsedDateTimes.Add(parsedDateTime);
                    }

                    for (int i = 1; i < Math.Min(splitLine.Count, splitHeader.Count); i++)
                    {
                        _cachedData[splitHeader[i]].Add(splitLine[i]);
                    }
                }
            }
        }
        catch
        {
            // Clear cache on failure
            _cachedData.Clear();
            _cachedParsedDateTimes.Clear();
        }
    }

    public async Task<List<double>> GetData(string trend)
    {
        if (!_cachedData.Any()) await ReadAndCacheData();
        List<double> doubleData = new();
        if (!_cachedData.TryGetValue(trend, out var data)) return doubleData;
        foreach (var d in data)
        {
            if (double.TryParse(d, out var parsed))
            {
                doubleData.Add(parsed);
            }
        }

        return doubleData;
    }

    public async Task<TimestampData> GetTimestampData(string trend)
    {
        if (DataSourceType == DataSourceType.NonTimeSeries) return new TimestampData(new(), new());

        if (!_cachedData.Any()) await ReadAndCacheData();
        if (!_cachedData.TryGetValue(trend, out var strData)) return new TimestampData(new(), new());


        List<double> values = strData.Select(strD => double.TryParse(strD, out var parsed) ? parsed : double.NaN).ToList();

        if (_needsSort)
        {
            // This should be very rare. Will need to copy over the datetimes to new List for sorting.
            List<DateTime> newDates = _cachedParsedDateTimes.Select(time => time).ToList();

            var tsData = new TimestampData(newDates, values);
            tsData.Sort();
            return tsData;
        }

        var timestampData = new TimestampData(_cachedParsedDateTimes, values);
        return timestampData;
    }

    public async Task<List<TimestampData>> GetTimestampData(List<string> trends)
    {
        var data = new List<TimestampData>();
        foreach (var t in trends)
        {
            var d = await GetTimestampData(t);
            data.Add(d);
        }

        return data;
    }

    public async Task<List<TimestampData>> GetTimestampData(List<string> trends, DateTime startDate, DateTime endDate)
    {
        List<TimestampData> data = new();
        foreach (var trend in trends)
        {
            TimestampData tsData = await GetTimestampData(trend);
            tsData.TrimDates(startDate, endDate);
            data.Add(tsData);
        }

        return data;
    }

    public string GetScript(List<string> trends, DateTime startDateInc, DateTime endDateExc)
    {
        throw new NotImplementedException();
    }

    public string Header => ShortName;
}
