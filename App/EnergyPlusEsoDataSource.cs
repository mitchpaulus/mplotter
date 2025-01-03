using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace csvplot;

public class EnergyPlusEsoDataSource : IDataSource
{
    private readonly string _filePath;
    private readonly TrendMatcher _matcher;

    // This is a mapping from an integer Id, to the point/trend name.
    private readonly Dictionary<string, int> _dataDictionary = new();

    // Note here that we have left everything as a string.
    // Note that we have a list because you get a different output for each 'Run'
    private readonly List<Dictionary<int, List<string>>> _dataValues = new();

    // Cached data is here to cache all the parsing of strings to doubles.
    private readonly Dictionary<int, List<double>> _cachedData = new();
    private bool _loaded = false;

    public FileInfo FileInfo { get; }

    public EnergyPlusEsoDataSource(string filePath, TrendMatcher matcher)
    {
        _filePath = filePath;
        FileInfo = new(_filePath);
        _matcher = matcher;
        Header = filePath;
    }

    private async Task ParseData()
    {
        try
        {
            using StreamReader r = new StreamReader(_filePath);
            // Read title line
            await r.ReadLineAsync();
            while (await r.ReadLineAsync() is { } line)
            {
                if (line.Length > 0 && !char.IsDigit(line[0]))
                {
                    break;
                }

                var split = line.Split(",");
                if (split.Length < 4) continue;

                if (!split[3].EndsWith("!Hourly")) continue;
                // 8 is for '!Hourly' plus previous space.
                var trendType = split[3].Substring(0, split[3].Length - 8);
                var trendName = $"{split[2]} {trendType}";

                // foreach (var findreplace in _matcher.regextransforms)
                // {
                //     var match = findreplace.item1.match(trendname);
                //     if (match.success)
                //     {
                //         foreach (var )
                //     }
                // }

                _dataDictionary[trendName] = int.Parse(split[0]);
            }

            int dataValueIndex = -1;
            while (await r.ReadLineAsync() is { } line)
            {
                var firstCommaIndex = line.IndexOf(',');
                if (firstCommaIndex < 0) break; // We've reached the 'End of Data' line.

                var key = int.Parse(line[..firstCommaIndex]);

                if (key == 1)
                {
                    dataValueIndex++;
                    _dataValues.Add(new Dictionary<int, List<string>>());
                }

                if (_dataValues[dataValueIndex].TryGetValue(key, out var list))
                {
                    list.Add(line);
                }
                else
                {
                    _dataValues[dataValueIndex][key] = new List<string>(8760) { line };
                }
            }

            _loaded = true;
        }
        catch (Exception)
        {
            // Ignore
        }
    }

    public async Task<List<Trend>> Trends()
    {
        if (!_loaded) await ParseData();
        return _dataDictionary.Keys.Select(name => new Trend(name, "")).ToList();
    }

    public async Task<List<double>> GetData(string trend)
    {
        if (!_loaded) await ParseData();
        if (!_dataDictionary.TryGetValue(trend, out int id)) return new List<double>();

        if (_cachedData.TryGetValue(id, out var cached))
        {
            // Return a clone. I've had too many issues tracking out other places in which that list is then modified.
            return cached.Select(d => d).ToList();
        }

        List<double> toReturn = new List<double>(8760);

        // Make the assumption that the last EnergyPlus "run" is the one of interest.
        // Mostly, the sizing runs should all come first.
        if (_dataValues[^1].TryGetValue(id, out var values))
        {
            foreach (var line in values)
            {
                var split = line.Split(",");
                if (split.Length != 2) return new List<double>();
                if (!double.TryParse(split[1], out var parsed)) return new List<double>();

                toReturn.Add(parsed);
            }
        }

        _cachedData[id] = toReturn;
        return toReturn.Select(d => d).ToList();
    }

    public string Header { get; }

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

    public Task<DataSourceType> DataSourceType() => Task.FromResult(csvplot.DataSourceType.EnergyModel);

    public async Task<TimestampData> GetTimestampData(string trend)
    {
        List<double> data = await GetData(trend);

        int year = DateTime.Now.Year;
        List<DateTime> dateTimes = new(8760);
        DateTime time = new DateTime(year, 1, 1);

        // EnergyPlus simulations are many times not tied to a year, but we tie them to today's year by default.
        // So in order to not have things shifted by a day, while maintaining the ability to align to any real data,
        // I'm putting in a Gap.
        if (!year.IsLeapYear() || data.Count == 8784)
        {
            foreach (var _ in data)
            {
                dateTimes.Add(time);
                time = time.AddHours(1);
            }
        }
        else
        {
            int janAndFebHours = Math.Min((31 + 28) * 24, data.Count);
            for (int i = 0; i < janAndFebHours; i++)
            {
                dateTimes.Add(time);
                time = time.AddHours(1);
            }
            time = new DateTime(year, 3, 1);
            while (dateTimes.Count < data.Count)
            {
                dateTimes.Add(time);
                time = time.AddHours(1);
            }
        }

        return new TimestampData(dateTimes, data);
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

    public async Task<List<TimestampData>> GetTimestampData(List<string> trends, DateTime startDateInc, DateTime endDateExc)
    {
        // Always give back full thing.
        return await GetTimestampData(trends);
        // List<TimestampData> data = new();
        // foreach (var trend in trends)
        // {
        //     TimestampData tsData = await GetTimestampData(trend);
        //     tsData.TrimDates(startDateInc, endDateExc);
        //     data.Add(tsData);
        // }
        //
        // return data;
    }

    public string GetScript(List<string> trends, DateTime startDateInc, DateTime endDateExc)
    {
        throw new NotImplementedException();
    }

    public async Task UpdateCache()
    {
        _cachedData.Clear();
        _dataDictionary.Clear();
        _dataValues.Clear();
        await ParseData();
    }
}
