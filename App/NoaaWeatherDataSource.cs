using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CCLLCDataSync;

namespace csvplot;

public record NoaaStation
{
    // USAF = Air Force station ID. May contain a letter in the first position. Cols 1-6
    // WBAN = NCDC WBAN number. Cols 8-12
    // STATION NAME = Name of station. Cols 14-42
    // CTRY = FIPS country ID (see country-list.txt). Cols 44-47
    // ST = State for US stations Cols 49-50
    // ICAO = ICAO ID 4-letter station identifier. Cols 52-56
    // LAT = Latitude in thousandths of decimal degrees Cols 58-64
    // LON = Longitude in thousandths of decimal degrees Cols 66-73
    // ELEV = Elevation in meters Cols 75-81
    // BEGIN = Beginning Period Of Record (YYYYMMDD). There may be reporting gaps within the P.O.R. Cols 83-90
    // END = Ending Period Of Record (YYYYMMDD). There may be reporting gaps within the P.O.R. Cols 92-99
    public required string Usaf { get; set; }
    public required string Wban { get; set; }
    public required string StationName { get; set; }
    public required string Ctry { get; set; }
    public required string St { get; set; }
    public required string Icao { get; set; }
    public required double? Lat { get; set; }
    public required double? Lon { get; set; }
    public required double? Elev { get; set; }
    public required DateTime Begin { get; set; }
    public required DateTime End { get; set; }
}


public class NoaaWeather
{
    public static double? ParseFloat(string input) => double.TryParse(input, out var parsed) ? parsed : null;

    public static async Task<List<NoaaStation>> GetStations()
    {
        // Download from: https://www1.ncdc.noaa.gov/pub/data/noaa/isd-history.txt
        var stations = new List<NoaaStation>();

        // First check if we've already downloaded the file today. NOAA doesn't update it that often.
        string? localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (localAppData is not null)
        {
            try
            {
                // First read from LOCALAPPDATA/mplotter/cache.txt. First line has timestamp of last download for isd-history.txt
                string cachePath = Path.Combine(localAppData, "mplotter", "cache.txt");

                StreamReader cacheReader = new StreamReader(cachePath);
                string? lastDownload = await cacheReader.ReadLineAsync();

                // If date of last download was today, read from LOCALAPPDATA/mplotter/isd-history.txt
                if (DateTime.ParseExact(lastDownload!.Trim(), "yyyyMMdd", CultureInfo.InvariantCulture) == DateTime.Today)
                {
                    string isdHistoryPath = Path.Combine(localAppData, "mplotter", "isd-history.txt");
                    using StreamReader isdHistoryReader = new StreamReader(isdHistoryPath);
                    while (await isdHistoryReader.ReadLineAsync() is { } line)
                    {
                        if (TryParseNoaaStationLine(line, out var station)) stations.Add(station);
                    }

                    return stations;
                }
            }
            catch
            {
                // Ignore
            }
        }


        HttpClient client = new HttpClient();
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync("https://www1.ncdc.noaa.gov/pub/data/noaa/isd-history.txt");
        }
        catch
        {
            return new List<NoaaStation>();
        }

        if (!response.IsSuccessStatusCode)
        {
            return new List<NoaaStation>();
        }

        // Read file as UTF-8
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (await reader.ReadLineAsync() is { } line)
        {
            if (TryParseNoaaStationLine(line, out var station)) stations.Add(station);
        }

        stream.Position = 0;
        // Try to Save to LOCALAPPDATA/mplotter/isd-history.txt
        try
        {
            Directory.CreateDirectory(Path.Combine(localAppData!, "mplotter"));
            await using var fileStream = File.Create(Path.Combine(localAppData!, "mplotter", "isd-history.txt"));
            await stream.CopyToAsync(fileStream);

            // Write to LOCALAPPDATA/mplotter/cache.txt. First line has timestamp of last download for isd-history.txt
            await using var cacheStream = File.Create(Path.Combine(localAppData!, "mplotter", "cache.txt"));
            byte[] cacheBytes = Encoding.UTF8.GetBytes(DateTime.Today.ToString("yyyyMMdd") + '\n');
            await cacheStream.WriteAsync(cacheBytes);
        }
        catch
        {
            // Ignore
        }

        return stations;
    }


    static bool TryParseNoaaStationLine(string? origLine, [NotNullWhen(true)] out NoaaStation? station)
    {
        if (origLine is { } line)
        {
            if (line.Length < 99)
            {
                station = null;
                return false;
            }

            // TODO: actually handle bad line for floats, begin/end
            station = new NoaaStation
            {
                Usaf = line.Substring(0, 6).Trim(),
                Wban = line.Substring(7, 5).Trim(),
                StationName = line.Substring(13, 29).Trim(),
                Ctry = line.Substring(43, 4).Trim(),
                St = line.Substring(48, 2).Trim(),
                Icao = line.Substring(51, 4).Trim(),
                Lat = ParseFloat(line.Substring(57, 7).Trim()),
                Lon = ParseFloat(line.Substring(65, 8).Trim()),
                Elev = ParseFloat(line.Substring(74, 7).Trim()),
                Begin = DateTime.ParseExact(line.Substring(82, 8).Trim(), "yyyyMMdd", CultureInfo.InvariantCulture),
                End = DateTime.ParseExact(line.Substring(91, 8).Trim(), "yyyyMMdd", CultureInfo.InvariantCulture)
            };
            return true;
        }

        station = null;
        return false;
    }
}

public class NoaaWeatherDataSource : IDataSource
{
    private readonly NoaaStation _station;

    private readonly List<Trend> _trends = new()
    {
        new Trend("NOAA Dry Bulb Air Temperature (°F)", "°F", "NOAA Dry Bulb Air Temperature (°F)"),
        new Trend("NOAA Dew Point Temperature (°F)", "°F", "NOAA Dew Point Temperature (°F)"),
    };

    public NoaaWeatherDataSource(NoaaStation station)
    {
        _station = station;
        Header = _station.StationName;
        ShortName = _station.StationName;
    }

    public Task<List<Trend>> Trends() => Task.FromResult(_trends);

    public async Task<List<double>> GetData(string trend) => (await GetTimestampData(trend)).Values;

    public string Header { get; }
    public string ShortName { get; }

    public Task<DataSourceType> DataSourceType() => Task.FromResult(csvplot.DataSourceType.TimeSeries);

    public async Task<TimestampData> GetTimestampData(string trend)
    {
        // Pull last 2 years of data by default.
        int currentYear = DateTime.Now.Year;
        int numPastYears = 2;

        List<DateTime> localDateTimes = new();
        List<double> values = new();

        Func<NoaaWeatherRecord, double?> trendSelector;

        if (trend == "NOAA Dry Bulb Air Temperature (°F)")
        {
            trendSelector = record => record.DryBulbAirTemperatureFahrenheit;
        }
        else if (trend == "NOAA Dew Point Temperature (°F)")
        {
            trendSelector = record => record.DewPointTemperatureFahrenheit;
        }
        else
        {
            return new TimestampData(new(), new());
        }

        // TimeZones tzs = new();

        var tz = TimeZoneInfo.Local;

        DateTime lastDate = DateTime.MinValue;
        bool needsSort = false;

        for (int i = numPastYears; i >= 0; i--)
        {
            (bool success, Stream s) = await TryGetStreamFromCache(_station.Usaf, _station.Wban, currentYear - i);
            if (!success) return new TimestampData(new(), new());
            List<NoaaWeatherRecord> records = GetRecordsFromStream(s);

            foreach (var rec in records)
            {
                var recValue = trendSelector(rec);
                if (recValue is { } d and < 200)
                {
                    var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(rec.DateTime, DateTimeKind.Utc), tz);
                    if (local.Year == 2024 && local.Month == 3 && local.Day == 10 && local.Hour == 2)
                    {
                        throw new Exception("This should not happen");
                    }
                    if (local < lastDate) needsSort = true;
                    lastDate = local;
                    localDateTimes.Add(local);
                    values.Add(d);
                }
            }
        }

        TimestampData tsData = new TimestampData(localDateTimes, values);
        if (needsSort) tsData.Sort();

        return tsData;
    }

    public static async Task<(bool, Stream)> TryGetStreamFromCache(string usaf, string wban, int year)
    {
        // Look for cached files
        string? localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        // Path is LOCALAPPDATA/mplotter/weather/USAF-WBAN-Year-YYYYMMDD.gz
        // YYYYMMDD is the date of the last download of the file
        try
        {
            Directory.CreateDirectory(Path.Combine(localAppData!, "mplotter", "weather"));
            string[] files = Directory.GetFiles(Path.Combine(localAppData!, "mplotter", "weather"), $"{usaf}-{wban}-{year}-*.gz");
            if (files.Length > 0)
            {
                foreach (var file in files)
                {
                    // Subtract 1 day to handle edge cases with timezones
                    int currentYear = DateTime.Today.AddDays(-1).Year;

                    if (year < currentYear)
                    {
                        // If the file is from a previous year, just use it, no need to check the date
                        var stream = new FileStream(files[0], FileMode.Open);
                        return (true, stream);
                    }

                    // Try to get the timestamp from the file name
                    string[] parts = Path.GetFileNameWithoutExtension(file).Split('-');

                    if (parts.Length != 4)
                    {
                        // Remove the file
                        File.Delete(file);
                    }

                    string dateString = parts[3];

                    if (dateString.Length != 8)
                    {
                        File.Delete(file);
                    }

                    string yearStr = dateString.Substring(0, 4);
                    string monthStr = dateString.Substring(4, 2);
                    string dayStr = dateString.Substring(6, 2);

                    if (!int.TryParse(yearStr, out var yearInt)) File.Delete(file);
                    if (!int.TryParse(monthStr, out var monthInt)) File.Delete(file);
                    if (!int.TryParse(dayStr, out var dayInt)) File.Delete(file);

                    DateTime lastDownload = new DateTime(yearInt, monthInt, dayInt);

                    if (lastDownload == DateTime.Today)
                    {
                        var stream = new FileStream(file, FileMode.Open);
                        return (true, stream);
                    }
                    File.Delete(file);
                }

                // If we get here, we didn't find a file that matches the current date

                // Get the file from the web, and then store to cache
                var s = await GetNoaaWeatherStreamUncompressedWeb(usaf, wban, year);
                await CacheWeatherStream(s, usaf, wban, year);
                return (true, s);
            }
            else
            {
                // Get the file from the web, and then store to cache
                var s = await GetNoaaWeatherStreamUncompressedWeb(usaf, wban, year);
                await CacheWeatherStream(s, usaf, wban, year);
                return (true, s);
            }
        }
        catch
        {
            return (false, Stream.Null);
        }
    }

    public static async Task CacheWeatherStream(Stream s, string usaf, string wban, int year)
    {
        try
        {
            string? localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            // Write to LOCALAPPDATA/mplotter/weather/USAF-WBAN-Year-YYYYMMDD.gz
            string cachePath = Path.Combine(localAppData!, "mplotter", "weather", $"{usaf}-{wban}-{year}-{DateTime.Today:yyyyMMdd}.gz");
            Directory.CreateDirectory(Path.Combine(localAppData!, "mplotter", "weather"));

            await using var fileStream = File.Create(cachePath);
            await s.CopyToAsync(fileStream);
            s.Position = 0;
        }
        catch
        {
            s.Position = 0;
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

    public async Task<List<TimestampData>> GetTimestampData(List<string> trends, DateTime startDateInc, DateTime endDateExc) => await GetTimestampData(trends);
    public string GetScript(List<string> trends, DateTime startDateInc, DateTime endDateExc)
    {
        throw new NotImplementedException();
    }

    public Task UpdateCache()
    {
        return Task.CompletedTask;
    }

    public static List<NoaaWeatherRecord> GetRecordsFromStream(Stream stream)
    {
        using StreamReader reader = new StreamReader(stream);
        List<NoaaWeatherRecord> records = new();

        while (reader.ReadLine() is { } line)
        {
            if (NoaaWeatherRecord.TryParseRecordLine(line, out var record)) records.Add(record);
        }

        return records;
    }

    public static async Task<Stream> GetNoaaWeatherStreamRawWeb(string usaf, string wban, int year)
    {
        // Typical URL is at: https://www1.ncdc.noaa.gov/pub/data/noaa/2024/<USAF>-<WBAN>-<Year>.gz
        try
        {
            using HttpClient client = new HttpClient();
            string url = $"https://www1.ncdc.noaa.gov/pub/data/noaa/{year}/{usaf}-{wban}-{year}.gz";
            HttpResponseMessage response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode) return Stream.Null;
            return await response.Content.ReadAsStreamAsync();
        }
        catch
        {
            // Return empty Stream
            return Stream.Null;
        }
    }

    public static async Task<Stream> GetNoaaWeatherStreamUncompressedWeb(string usaf, string wban, int year)
    {
        Stream compressedStream = await GetNoaaWeatherStreamRawWeb(usaf, wban, year);
        if (compressedStream == Stream.Null) return Stream.Null;

        // Decompress the stream
        MemoryStream decompressedStream = new MemoryStream();
        await using GZipStream decompressionStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        await decompressionStream.CopyToAsync(decompressedStream);
        decompressedStream.Position = 0;
        return decompressedStream;
    }
}

public class NoaaWeatherRecord
{
    // ISD Format
    // 1-4: Total Variable Characters
    // 5-10: USAF
    // 11-15: WBAN
    // 16-23: Date in YYYYMMDD form
    // 24-27: UTC Time in HHMM form
    // 28: Source
    // 29-34: Latitude [+-][0-9]{5} Scaling factor: 1000 +99999 = Missing
    // 35-41: Longitude [+-][0-9]{5}
    // 42-46: Code
    // 47-51: Elevation (m)
    // 52-56: Call letter identifier
    // 57-60: Quality control process name
    // 61-63: Wind direction angle
    // 64: Wind direction quality
    // 65: Wind direction Type
    // 66-69: Wind speed (m/s) Scaling factor: 10
    // 70: Wind speed quality
    // 71-75: Ceiling Height dimension (m)
    // 76: Ceiling Height Quality
    // 77: Ceiling determination code
    // 78: CAVOK code
    // 79-84: Visibility distance (m)
    // 85: Visibility quality code
    // 86: Visibility variability code
    // 87: Visibility variability quality code
    // 88-92: Dry Bulb Air Temperature (C), Scale factor: 10 [+-][0-9]{4} '+9999' is missing
    // 93: Dry Bulb Air Temperature Quality
    // 94-98: Dew Point Air Temperature (C), Scale factor: 10 [+-][0-9]{4} '+9999' is missing
    // 99: Dew Point Temperature quality code
    // 100-104: Air pressure (Hectopascals), Scale factor: 10
    // 105: Air pressure quality code


    // Keep everything as raw strings until conversion needed.
    // Not interested in all fields, just a few.
    public string Usaf { get; set; }
    public string Wban { get; set; }
    public string Date { get; set; }
    public string Time { get; set; }
    public string DryBulbAirTemperatureRaw { get; set; }
    public string DewPointTemperatureRaw { get; set; }

    public DateTime DateTime { get; }

    public NoaaWeatherRecord(string usaf, string wban, string date, string time, string dryBulbAirTemperatureRaw,
        string dewPointTemperatureRaw)
    {
        Usaf = usaf;
        Wban = wban;
        Date = date;
        Time = time;
        DryBulbAirTemperatureRaw = dryBulbAirTemperatureRaw;
        DewPointTemperatureRaw = dewPointTemperatureRaw;


        if (Date.Length == 8)
        {
            bool success = DateTime.TryParseExact(Date + Time, "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime);
            DateTime = success ? dateTime : DateTime.MinValue;
        }

    }

    public static bool TryParseRecordLine(string? origLine, [NotNullWhen(true)] out NoaaWeatherRecord? record)
    {
        if (origLine is { } line)
        {
            if (line.Length < 105)
            {
                record = null;
                return false;
            }

            var usaf = line.Substring(4, 6);
            var wban = line.Substring(10, 5);
            var date = line.Substring(15, 8);
            var time = line.Substring(23, 4);
            var dryBulbAirTemperatureRaw = line.Substring(87, 5);
            var dewPointTemperatureRaw = line.Substring(93, 5);

            record = new NoaaWeatherRecord(usaf, wban, date, time, dryBulbAirTemperatureRaw, dewPointTemperatureRaw);
            return true;
        }

        record = null;
        return false;
    }

    public double? DryBulbAirTemperatureFahrenheit =>
        double.TryParse(DryBulbAirTemperatureRaw, out var celsius) ? (celsius / 10) * 9 / 5 + 32 : null;

    public double? DewPointTemperatureFahrenheit =>
        double.TryParse(DewPointTemperatureRaw, out var celsius) ? (celsius / 10) * 9 / 5 + 32 : null;


    public override string ToString()
    {
        return $"{Date} {Time} {DryBulbAirTemperatureFahrenheit ?? 0.0} {DewPointTemperatureFahrenheit ?? 0.0}";
    }
}
