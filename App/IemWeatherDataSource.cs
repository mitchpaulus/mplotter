using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace csvplot;

public class IemWeatherDataSource : IDataSource
{
    private readonly IemStation _station;

    private readonly List<Trend> _trends = new();

    public IemWeatherDataSource(IemStation station)
    {
        _station = station;
        Header = $"IEM {_station.Stid} {_station.Name}";
        ShortName = $"IEM {_station.Stid}";

        _trends.Add(new Trend($"IEM {_station.Stid}: Dry Bulb Air Temperature (°F)", "°F", $"IEM {_station.Stid}: Dry Bulb Air Temperature (°F)"));
        _trends.Add(new Trend($"IEM {_station.Stid}: Dew Point Temperature (°F)", "°F", $"IEM {_station.Stid}: Dew Point Temperature (°F)"));
        _trends.Add(new Trend($"IEM {_station.Stid}: Relative Humidity (%)", "%", $"IEM {_station.Stid} Relative Humidity (%)"));
    }

    public string Header { get; }
    public string ShortName { get; }

    public Task<List<Trend>> Trends() => Task.FromResult(_trends);

    public Task<DataSourceType> DataSourceType() => Task.FromResult(csvplot.DataSourceType.TimeSeries);

    public async Task<List<double>> GetData(string trend) => (await GetTimestampData(trend)).Values;

    public async Task<TimestampData> GetTimestampData(string trend)
    {
        Func<IemWeatherRecord, double?> selector;
        if (trend.Contains("Dry Bulb")) selector = r => r.Tmpf;
        else if (trend.Contains("Dew Point")) selector = r => r.Dwpf;
        else if (trend.Contains("Relative Humidity")) selector = r => r.Relh;
        else return new TimestampData(new(), new());

        List<DateTime> times = new();
        List<double> values = new();

        DateTime last = DateTime.MinValue;
        bool needsSort = false;

        (bool ok, List<IemWeatherRecord> records) = await TryGetRecords(_station.Stid);
        if (!ok) return new TimestampData(new (), new());

        foreach (var rec in records)
        {
            if (selector(rec) is not { } v) continue;
            if (rec.Utc < last) needsSort = true;
            last = rec.Utc;
            times.Add(TimeZoneInfo.ConvertTimeFromUtc(rec.Utc, TimeZoneInfo.Local));
            values.Add(v);
        }

        TimestampData tsData = new(times, values);
        if (needsSort) tsData.Sort();
        return tsData;
    }

    public async Task<List<TimestampData>> GetTimestampData(List<string> trends)
    {
        var data = new List<TimestampData>();
        foreach (var t in trends)
        {
            var timestampData = await GetTimestampData(t);

            data.Add(timestampData);
        }
        return data;
    }

    public async Task<List<TimestampData>> GetTimestampData(List<string> trends, DateTime startDateInc, DateTime endDateExc)
        => await GetTimestampData(trends);

    public string GetScript(List<string> trends, DateTime startDateInc, DateTime endDateExc) => throw new NotImplementedException();

    public Task UpdateCache() => Task.CompletedTask;

    private static readonly ConcurrentDictionary<string, Task<(bool, List<IemWeatherRecord>)>> MemoryCache = new();

    private static Task<(bool, List<IemWeatherRecord>)> TryGetRecords(string stid) => MemoryCache.GetOrAdd(stid, FetchAndParse);

    private static async Task<(bool, List<IemWeatherRecord>)> FetchAndParse(string stid)
    {
        string? localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        string? csv = null;

        try
        {
            if (localAppData is not null)
            {
                string dir = Path.Combine(localAppData, "mplotter", "iem");
                Directory.CreateDirectory(dir);
                string[] files = Directory.GetFiles(dir, $"{stid}-*.csv");

                foreach (var file in files)
                {
                    string[] parts = Path.GetFileNameWithoutExtension(file).Split('-');
                    if (parts.Length != 2 || parts[1].Length != 8) { File.Delete(file); continue; }
                    if (!DateTime.TryParseExact(parts[1], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var stamp))
                    {
                        File.Delete(file);
                        continue;
                    }
                    if (stamp == DateTime.Today)
                    {
                        csv = await File.ReadAllTextAsync(file);
                        break;
                    }
                    File.Delete(file);
                }
            }

            if (csv is null)
            {
                csv = await FetchCsvWeb(stid);
                if (csv is null) return (false, new());

                if (localAppData is not null)
                {
                    try
                    {
                        string dir = Path.Combine(localAppData, "mplotter", "iem");
                        Directory.CreateDirectory(dir);
                        string cachePath = Path.Combine(dir, $"{stid}-{DateTime.Today:yyyyMMdd}.csv");
                        await File.WriteAllTextAsync(cachePath, csv);
                    }
                    catch
                    {
                        // Ignore cache write failures.
                    }
                }
            }

            return (true, ParseCsv(csv));
        }
        catch
        {
            return (false, new());
        }
    }

    public static async Task<string?> FetchCsvWeb(string stid)
    {
        DateTime now = DateTime.Now;
        int currentYear = now.Year;
        DateTime tom = now.AddDays(1);

        try
        {
            using HttpClient client = new();
            string url =
                $"https://mesonet.agron.iastate.edu/cgi-bin/request/asos.py" +
                $"?data=tmpf&data=dwpf&data=relh" +
                $"&station={Uri.EscapeDataString(stid)}" +
                $"&report_type=3&report_type=4" + // Only include the routine and special readings, not the high frequency HFMETAR data.
                $"&tz=UTC" +
                $"&year1={currentYear - 2}&month1=1&day1=1" +
                $"&year2={tom.Year}&month2={tom.Month}&day2={tom.Day}";

            HttpResponseMessage response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsStringAsync();
        }
        catch
        {
            return null;
        }
    }

    public static List<IemWeatherRecord> ParseCsv(string csv)
    {
        List<IemWeatherRecord> records = new();
        using StringReader reader = new(csv);

        string? header = reader.ReadLine();
        if (header is null) return records;

        // Expected header: station,valid,tmpf,dwpf,relh
        string[] headers = header.Split(',');
        int validIdx = Array.IndexOf(headers, "valid");
        int tmpfIdx = Array.IndexOf(headers, "tmpf");
        int dwpfIdx = Array.IndexOf(headers, "dwpf");
        int relhIdx = Array.IndexOf(headers, "relh");
        if (validIdx < 0) return records;

        while (reader.ReadLine() is { } line)
        {
            if (IemWeatherRecord.TryParse(line, validIdx, tmpfIdx, dwpfIdx, relhIdx, out var rec))
                records.Add(rec);
        }

        return records;
    }
}

public class IemWeatherRecord
{
    public DateTime Utc { get; }
    public double? Tmpf { get; }
    public double? Dwpf { get; }
    public double? Relh { get; }

    public IemWeatherRecord(DateTime utc, double? tmpf, double? dwpf, double? relh)
    {
        Utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        Tmpf = tmpf;
        Dwpf = dwpf;
        Relh = relh;
    }

    public static bool TryParse(string line, int validIdx, int tmpfIdx, int dwpfIdx, int relhIdx,
        [NotNullWhen(true)] out IemWeatherRecord? record)
    {
        record = null;
        string[] fields = line.Split(',');
        int maxIdx = Math.Max(validIdx, Math.Max(tmpfIdx, Math.Max(dwpfIdx, relhIdx)));
        if (fields.Length <= maxIdx) return false;

        if (!DateTime.TryParseExact(fields[validIdx].Trim(), "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var ts))
            return false;

        double? tmpf = tmpfIdx >= 0 ? ParseVal(fields[tmpfIdx]) : null;
        double? dwpf = dwpfIdx >= 0 ? ParseVal(fields[dwpfIdx]) : null;
        double? relh = relhIdx >= 0 ? ParseVal(fields[relhIdx]) : null;

        if (tmpf is null && dwpf is null && relh is null) return false;

        record = new IemWeatherRecord(ts, tmpf, dwpf, relh);
        return true;
    }

    private static double? ParseVal(string s)
    {
        if (s.Length == 0 || s == "M") return null;
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
