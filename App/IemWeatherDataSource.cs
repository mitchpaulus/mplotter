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
    public const string TrendDryBulb = "IEM Dry Bulb Air Temperature (°F)";
    public const string TrendDewPoint = "IEM Dew Point Temperature (°F)";
    public const string TrendRelh = "IEM Relative Humidity (%)";

    private readonly IemStation _station;

    private readonly List<Trend> _trends = new()
    {
        new Trend(TrendDryBulb, "°F", TrendDryBulb),
        new Trend(TrendDewPoint, "°F", TrendDewPoint),
        new Trend(TrendRelh, "%", TrendRelh),
    };

    public IemWeatherDataSource(IemStation station)
    {
        _station = station;
        Header = $"IEM {_station.Stid} {_station.Name}";
        ShortName = $"IEM {_station.Stid}";
    }

    public string Header { get; }
    public string ShortName { get; }

    public Task<List<Trend>> Trends() => Task.FromResult(_trends);

    public Task<DataSourceType> DataSourceType() => Task.FromResult(csvplot.DataSourceType.TimeSeries);

    public async Task<List<double>> GetData(string trend) => (await GetTimestampData(trend)).Values;

    public async Task<TimestampData> GetTimestampData(string trend)
    {
        Func<IemWeatherRecord, double?> selector;
        if (trend == TrendDryBulb) selector = r => r.Tmpf;
        else if (trend == TrendDewPoint) selector = r => r.Dwpf;
        else if (trend == TrendRelh) selector = r => r.Relh;
        else return new TimestampData(new(), new());

        int currentYear = DateTime.UtcNow.Year;
        const int numPastYears = 2;

        List<DateTime> times = new();
        List<double> values = new();

        DateTime last = DateTime.MinValue;
        bool needsSort = false;

        for (int i = numPastYears; i >= 0; i--)
        {
            int year = currentYear - i;
            (bool ok, List<IemWeatherRecord> records) = await TryGetRecords(_station.Stid, year);
            if (!ok) continue;

            foreach (var rec in records)
            {
                if (selector(rec) is not { } v) continue;
                if (rec.Utc < last) needsSort = true;
                last = rec.Utc;
                times.Add(rec.Utc);
                values.Add(v);
            }
        }

        TimestampData tsData = new(times, values);
        if (needsSort) tsData.Sort();
        return tsData;
    }

    public async Task<List<TimestampData>> GetTimestampData(List<string> trends)
    {
        var data = new List<TimestampData>();
        foreach (var t in trends) data.Add(await GetTimestampData(t));
        return data;
    }

    public async Task<List<TimestampData>> GetTimestampData(List<string> trends, DateTime startDateInc, DateTime endDateExc)
        => await GetTimestampData(trends);

    public string GetScript(List<string> trends, DateTime startDateInc, DateTime endDateExc) => throw new NotImplementedException();

    public Task UpdateCache() => Task.CompletedTask;

    private static readonly ConcurrentDictionary<(string Stid, int Year), Task<(bool, List<IemWeatherRecord>)>> _memoryCache = new();

    public static Task<(bool, List<IemWeatherRecord>)> TryGetRecords(string stid, int year)
        => _memoryCache.GetOrAdd((stid, year), key => FetchAndParse(key.Stid, key.Year));

    private static async Task<(bool, List<IemWeatherRecord>)> FetchAndParse(string stid, int year)
    {
        string? localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        string? csv = null;

        try
        {
            if (localAppData is not null)
            {
                string dir = Path.Combine(localAppData, "mplotter", "iem");
                Directory.CreateDirectory(dir);
                string[] files = Directory.GetFiles(dir, $"{stid}-{year}-*.csv");

                int todayMinus1Year = DateTime.Today.AddDays(-1).Year;

                foreach (var file in files)
                {
                    if (year < todayMinus1Year)
                    {
                        csv = await File.ReadAllTextAsync(file);
                        break;
                    }

                    string[] parts = Path.GetFileNameWithoutExtension(file).Split('-');
                    if (parts.Length != 3 || parts[2].Length != 8) { File.Delete(file); continue; }
                    if (!DateTime.TryParseExact(parts[2], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var stamp))
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
                csv = await FetchCsvWeb(stid, year);
                if (csv is null) return (false, new());

                if (localAppData is not null)
                {
                    try
                    {
                        string dir = Path.Combine(localAppData, "mplotter", "iem");
                        Directory.CreateDirectory(dir);
                        string cachePath = Path.Combine(dir, $"{stid}-{year}-{DateTime.Today:yyyyMMdd}.csv");
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

    public static async Task<string?> FetchCsvWeb(string stid, int year)
    {
        try
        {
            using HttpClient client = new();
            string url =
                $"https://mesonet.agron.iastate.edu/cgi-bin/request/asos.py" +
                $"?data=tmpf&data=dwpf&data=relh" +
                $"&station={Uri.EscapeDataString(stid)}" +
                $"&tz=UTC" +
                $"&year1={year}&month1=1&day1=1" +
                $"&year2={year + 1}&month2=1&day2=1";

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
        s = s.Trim();
        if (s.Length == 0 || s == "M") return null;
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
