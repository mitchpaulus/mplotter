using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace csvplot;

public class SimpleDelimitedFile : IDataSource
{
    public async Task<List<string>> Trends()
    {
        if (_trends.Any()) return _trends;
        await ReadTrendsAndDelimiter();
        return _trends;
    }

    private char _delimiter = '\t';

    public async Task<DataSourceType> DataSourceType()
    {
        if (_trends.Count == 0) await ReadTrendsAndDelimiter();
        return _dataSourceType;
    }

    private DataSourceType _dataSourceType = csvplot.DataSourceType.TimeSeries;

    private readonly Dictionary<string, List<string>> _cachedData = new();
    private readonly List<DateTime> _cachedParsedDateTimes = new();

    private bool _needsSort = false;
    public FileInfo FileInfo { get; }

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

    private readonly List<string> _trends = new(100);

    private readonly string _source;

    public SimpleDelimitedFile(string source)
    {
        _source = source;
        FileInfo = new(source);
    }

    private async Task ReadTrendsAndDelimiter()
    {
        int tries = 1;
        while (true)
        {
            try
            {
                await using FileStream stream = new(_source, FileMode.Open, FileAccess.Read);
                var sr = new StreamReader(stream);
                var headerLine = await sr.ReadLineAsync();

                _trends.Clear();

                if (headerLine is not null)
                {
                    if (_source.ToLowerInvariant().EndsWith(".tsv") || headerLine.Contains('\t'))
                    {
                        string[] splitHeader = headerLine.Split('\t');
                        _trends.AddRange(splitHeader.Skip(1));
                        _delimiter = '\t';
                    }
                    else if (_source.ToLowerInvariant().EndsWith(".csv"))
                    {
                        var (splitHeader, success) = headerLine.TryParseCsvLine();
                        _trends.AddRange(success ? splitHeader.Skip(1).ToList() : Array.Empty<string>());
                        _delimiter = ',';
                    }
                }

                var nextLine = await sr.ReadLineAsync();
                if (nextLine is not null)
                {
                    (var splitLine, bool success) = Split(nextLine);
                    if (!success)
                    {
                        _dataSourceType = csvplot.DataSourceType.NonTimeSeries;
                    }
                    else
                    {
                        // Try to read first column as a DateTime
                        if (DateTime.TryParse(splitLine[0], out _))
                        {
                            _dataSourceType = csvplot.DataSourceType.TimeSeries;
                        }
                        else
                        {
                            int rows = 1;
                            while (await sr.ReadLineAsync() is not null)
                            {
                                rows++;
                            }

                            _dataSourceType = rows == 8760 ? csvplot.DataSourceType.EnergyModel : csvplot.DataSourceType.NonTimeSeries;
                        }
                    }
                }
                else
                {
                    _dataSourceType = csvplot.DataSourceType.NonTimeSeries;
                }

                break;
            }
            catch
            {
                tries++;
                await Task.Delay(50);
                if (tries <= 20) continue;

                _trends.Clear();
                _cachedData.Clear();
                _cachedParsedDateTimes.Clear();
                return;
            }
        }
    }

    private (List<string>, bool) Split(string inputLine) => _delimiter == ',' ? inputLine.TryParseCsvLine() : (inputLine.Split(_delimiter).ToList(), true);

    private async Task ReadAndCacheData()
    {
        _cachedData.Clear();
        _cachedParsedDateTimes.Clear();
        var tries = 1;
        while (true)
        {
            try
            {
                await using FileStream stream = new FileStream(_source, FileMode.Open, FileAccess.Read);
                var sr = new StreamReader(stream);

                var headerLine = await sr.ReadLineAsync();
                if (headerLine is null) return;

                Func<string, (List<string>, bool)> splitFunc = _delimiter == ','
                    ? Extensions.TryParseCsvLine
                    : s => (s.Split(_delimiter).ToList(), true);

                var (splitHeader, headSuccess) = splitFunc(headerLine);
                if (!headSuccess) return;

                foreach (var head in splitHeader) _cachedData[head] = new List<string>(8760);

                if (_dataSourceType == csvplot.DataSourceType.EnergyModel)
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

                break;
            }
            catch
            {
                tries++;
                await Task.Delay(50);
                if (tries <= 20) continue;

                // Clear cache on failure
                _cachedData.Clear();
                _cachedParsedDateTimes.Clear();
                return;
            }
        }
    }

    public async Task<List<double>> GetData(string trend)
    {
        if (!_trends.Any()) await ReadTrendsAndDelimiter();
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
        if (_dataSourceType == csvplot.DataSourceType.NonTimeSeries) return new TimestampData(new(), new());

        if (!_cachedData.Any()) await ReadAndCacheData();
        if (!_cachedData.TryGetValue(trend, out var strData)) return new TimestampData(new(), new());


        List<double> values = new();
        List<DateTime> newDates = new();
        for (int i = 0; i < strData.Count; i++)
        {
            // Don't add null data. Gaps can be added later.
            if (!double.TryParse(strData[i], out var d)) continue;
            values.Add(d);
            newDates.Add(_cachedParsedDateTimes[i]);
        }

        if (_needsSort)
        {
            // This should be very rare. Will need to copy over the datetimes to new List for sorting.
            var tsData = new TimestampData(newDates, values);
            tsData.Sort();
            return tsData;
        }

        var timestampData = new TimestampData(newDates, values);
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

    public async Task UpdateCache()
    {
        await ReadTrendsAndDelimiter();
        await ReadAndCacheData();
    }

    public string Header => ShortName;
}
