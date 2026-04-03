using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SemaphoreSlim = System.Threading.SemaphoreSlim;

namespace csvplot;

public class SimpleDelimitedFile : IDataSource
{
    public async Task<List<Trend>> Trends()
    {
        await EnsureSnapshotLoaded();
        await _cacheLock.WaitAsync();
        try
        {
            return _trends.Select(s => new Trend(s, "", s)).ToList();
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private char _delimiter = '\t';
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private bool _hasLoadedSnapshot = false;

    public async Task<DataSourceType> DataSourceType()
    {
        await EnsureSnapshotLoaded();
        await _cacheLock.WaitAsync();
        try
        {
            return _dataSourceType;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private DataSourceType _dataSourceType = csvplot.DataSourceType.TimeSeries;

    private Dictionary<string, List<string>> _cachedData = new();
    private List<DateTime> _cachedParsedDateTimes = new();

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

    private List<string> _trends = new(100);

    private readonly string _source;

    public SimpleDelimitedFile(string source)
    {
        _source = source;
        FileInfo = new(source);
    }

    private sealed class FileSnapshot
    {
        public FileSnapshot(
            List<string> trends,
            char delimiter,
            DataSourceType dataSourceType,
            Dictionary<string, List<string>> cachedData,
            List<DateTime> cachedParsedDateTimes,
            bool needsSort)
        {
            Trends = trends;
            Delimiter = delimiter;
            DataSourceType = dataSourceType;
            CachedData = cachedData;
            CachedParsedDateTimes = cachedParsedDateTimes;
            NeedsSort = needsSort;
        }

        public List<string> Trends { get; }
        public char Delimiter { get; }
        public DataSourceType DataSourceType { get; }
        public Dictionary<string, List<string>> CachedData { get; }
        public List<DateTime> CachedParsedDateTimes { get; }
        public bool NeedsSort { get; }
    }

    private static (List<string>, bool) Split(string inputLine, char delimiter) => delimiter == ',' ? inputLine.TryParseCsvLine() : (inputLine.Split(delimiter).ToList(), true);

    private async Task EnsureSnapshotLoaded()
    {
        if (_hasLoadedSnapshot) return;

        await _cacheLock.WaitAsync();
        try
        {
            if (_hasLoadedSnapshot) return;

            FileSnapshot? snapshot = await LoadSnapshot();
            if (snapshot is not null)
            {
                ApplySnapshot(snapshot);
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private void ApplySnapshot(FileSnapshot snapshot)
    {
        _trends = snapshot.Trends;
        _delimiter = snapshot.Delimiter;
        _dataSourceType = snapshot.DataSourceType;
        _cachedData = snapshot.CachedData;
        _cachedParsedDateTimes = snapshot.CachedParsedDateTimes;
        _needsSort = snapshot.NeedsSort;
        _hasLoadedSnapshot = true;
    }

    private async Task<FileSnapshot?> LoadSnapshot()
    {
        int tries = 1;
        while (true)
        {
            try
            {
                await using FileStream stream = new(_source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var sr = new StreamReader(stream);
                string? headerLine = await sr.ReadLineAsync();
                if (headerLine is null)
                {
                    return new FileSnapshot(new List<string>(), '\t', csvplot.DataSourceType.NonTimeSeries, new(), new(), false);
                }

                char delimiter = '\t';
                List<string> splitHeader;
                if (_source.ToLowerInvariant().EndsWith(".tsv") || headerLine.Contains('\t'))
                {
                    delimiter = '\t';
                    splitHeader = headerLine.Split('\t').ToList();
                }
                else if (_source.ToLowerInvariant().EndsWith(".csv"))
                {
                    var (parsedHeader, success) = headerLine.TryParseCsvLine();
                    if (!success) throw new InvalidDataException("Could not parse header line.");
                    delimiter = ',';
                    splitHeader = parsedHeader;
                }
                else
                {
                    splitHeader = headerLine.Split(delimiter).ToList();
                }

                var trends = splitHeader.Skip(1).ToList();
                List<string> rawDataLines = new();
                while (await sr.ReadLineAsync() is { } line)
                {
                    rawDataLines.Add(line);
                }

                DataSourceType dataSourceType;
                if (rawDataLines.Count == 0)
                {
                    dataSourceType = csvplot.DataSourceType.NonTimeSeries;
                }
                else
                {
                    var (splitLine, success) = Split(rawDataLines[0], delimiter);
                    if (!success || splitLine.Count == 0)
                    {
                        dataSourceType = csvplot.DataSourceType.NonTimeSeries;
                    }
                    else if (DateTime.TryParse(splitLine[0], out _))
                    {
                        dataSourceType = csvplot.DataSourceType.TimeSeries;
                    }
                    else
                    {
                        dataSourceType = rawDataLines.Count == 8760 ? csvplot.DataSourceType.EnergyModel : csvplot.DataSourceType.NonTimeSeries;
                    }
                }

                Dictionary<string, List<string>> cachedData = new();
                foreach (var head in splitHeader)
                {
                    cachedData[head] = new List<string>(Math.Max(rawDataLines.Count, 8760));
                }

                List<DateTime> cachedParsedDateTimes = new(Math.Max(rawDataLines.Count, 8760));
                bool needsSort = false;

                if (dataSourceType == csvplot.DataSourceType.EnergyModel)
                {
                    foreach (var line in rawDataLines)
                    {
                        var (splitLine, success) = Split(line, delimiter);
                        if (!success) continue;

                        for (int i = 0; i < Math.Min(splitLine.Count, splitHeader.Count); i++)
                        {
                            cachedData[splitHeader[i]].Add(splitLine[i]);
                        }
                    }

                    DateTime startDateTime = new(DateTime.Now.Year, 1, 1);
                    for (int i = 0; i < 8760; i++)
                    {
                        cachedParsedDateTimes.Add(startDateTime.AddHours(i));
                    }
                }
                else if (dataSourceType == csvplot.DataSourceType.TimeSeries)
                {
                    DateTime prevDateTime = DateTime.MinValue;
                    foreach (var line in rawDataLines)
                    {
                        var (splitLine, success) = Split(line, delimiter);
                        if (!success || splitLine.Count == 0) continue;
                        if (!DateTime.TryParse(splitLine[0], out var parsedDateTime)) continue;

                        if (parsedDateTime < prevDateTime) needsSort = true;
                        prevDateTime = parsedDateTime;
                        cachedParsedDateTimes.Add(parsedDateTime);

                        for (int i = 1; i < Math.Min(splitLine.Count, splitHeader.Count); i++)
                        {
                            cachedData[splitHeader[i]].Add(splitLine[i]);
                        }
                    }
                }

                return new FileSnapshot(trends, delimiter, dataSourceType, cachedData, cachedParsedDateTimes, needsSort);
            }
            catch
            {
                tries++;
                await Task.Delay(50);
                if (tries <= 20) continue;
                return null;
            }
        }
    }

    public async Task<List<double>> GetData(string trend)
    {
        await EnsureSnapshotLoaded();
        await _cacheLock.WaitAsync();
        try
        {
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
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task<TimestampData> GetTimestampData(string trend)
    {
        await EnsureSnapshotLoaded();
        await _cacheLock.WaitAsync();
        try
        {
            if (_dataSourceType == csvplot.DataSourceType.NonTimeSeries) return new TimestampData(new(), new());
            if (!_cachedData.TryGetValue(trend, out var strData)) return new TimestampData(new(), new());

            List<double> values = new();
            List<DateTime> newDates = new();
            for (int i = 0; i < strData.Count && i < _cachedParsedDateTimes.Count; i++)
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

            return new TimestampData(newDates, values);
        }
        finally
        {
            _cacheLock.Release();
        }
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
        await _cacheLock.WaitAsync();
        try
        {
            FileSnapshot? snapshot = await LoadSnapshot();
            if (snapshot is not null)
            {
                ApplySnapshot(snapshot);
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public string Header => ShortName;
}
