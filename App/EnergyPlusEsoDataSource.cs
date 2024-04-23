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

    private readonly Dictionary<string, int> _dataDictionary = new();
    private readonly Dictionary<int, List<string>> _dataValues = new();

    private readonly Dictionary<int, List<double>> _cachedData = new();
    private bool _loaded = false;

    public EnergyPlusEsoDataSource(string filePath, TrendMatcher matcher)
    {
        _filePath = filePath;
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

            while (await r.ReadLineAsync() is { } line)
            {
                var firstCommaIndex = line.IndexOf(',');
                if (firstCommaIndex < 0) break; // We've reached the 'End of Data' line.
                var key = int.Parse(line[..firstCommaIndex]);

                if (_dataValues.TryGetValue(key, out var list))
                {
                    list.Add(line);
                }
                else
                {
                    _dataValues[key] = new List<string> { line };
                }
            }

            _loaded = true;
        }
        catch (Exception)
        {
            // Ignore
        }
    }

    public async Task<List<string>> Trends()
    {
        if (!_loaded) await ParseData();
        return _dataDictionary.Keys.ToList();
    }

    public async Task<List<double>> GetData(string trend)
    {
        if (!_loaded) await ParseData();
        if (!_dataDictionary.TryGetValue(trend, out int id)) return new List<double>();

        if (_cachedData.TryGetValue(id, out var cached)) return cached;

        List<double> toReturn = new List<double>(8760);

        if (_dataValues.TryGetValue(id, out var values))
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
        return toReturn;
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

    public DataSourceType DataSourceType => DataSourceType.EnergyModel;

    public async Task<TimestampData> GetTimestampData(string trend)
    {
        int year = DateTime.Now.Year;
        List<double> data = await GetData(trend);

        List<DateTime> dateTimes = new(8760);

        DateTime time = new DateTime(year, 1, 1);

        foreach (var t in data)
        {
            dateTimes.Add(time);
            time = time.AddHours(1);
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
        List<TimestampData> data = new();
        foreach (var trend in trends)
        {
            TimestampData tsData = await GetTimestampData(trend);
            tsData.TrimDates(startDateInc, endDateExc);
            data.Add(tsData);
        }

        return data;
    }

    public string GetScript(List<string> trends, DateTime startDateInc, DateTime endDateExc)
    {
        throw new NotImplementedException();
    }
}
