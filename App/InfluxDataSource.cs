using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using InfluxDB.Client;
using InfluxDB.Client.Core.Flux.Domain;

namespace csvplot;

public class InfluxEnv
{
    public readonly string InfluxToken;
    public readonly string InfluxHost;
    public readonly string InfluxOrg;

    public InfluxEnv(string influxToken, string influxHost, string influxOrg)
    {
        InfluxToken = influxToken;
        InfluxHost = influxHost;
        InfluxOrg = influxOrg;
    }

    public bool IsValid() => !(string.IsNullOrWhiteSpace(InfluxToken) || string.IsNullOrWhiteSpace(InfluxHost) || string.IsNullOrWhiteSpace(InfluxOrg));
}

public class InfluxDataSource : IDataSource, IEditableTrendUnitSource, IEditableTrendTagSource
{
    private readonly string _bucket;
    private Configuration _configuration = new();

    private readonly string _influxToken;
    private readonly string _influxOrg;
    private readonly string _influxHost;

    private bool _isValid = true;

    public InfluxDataSource(string bucket)
    {
        string? influxToken = Environment.GetEnvironmentVariable("INFLUX_TOKEN");
        if (influxToken is null) _isValid = false;
        _influxToken = influxToken ?? "";

        string? influxHost = Environment.GetEnvironmentVariable("INFLUX_HOST");
        if (influxHost is null) _isValid = false;
        _influxHost = influxHost ?? "";

        string? influxOrg = Environment.GetEnvironmentVariable("INFLUX_ORG");
        if (influxOrg is null) _isValid = false;
        _influxOrg = influxOrg ?? "";

        _bucket = bucket;

        Header = $"Influx: {bucket}";
        ShortName = $"Influx: {bucket}";

        LoadConfiguration();
    }

    public static InfluxEnv GetEnv()
    {
        try
        {
            string? influxToken = Environment.GetEnvironmentVariable("INFLUX_TOKEN");
            string? influxHost = Environment.GetEnvironmentVariable("INFLUX_HOST");
            string? influxOrg = Environment.GetEnvironmentVariable("INFLUX_ORG");
            return new InfluxEnv(influxToken ?? "", influxHost ?? "", influxOrg ?? "");
        }
        catch
        {
            return new InfluxEnv(string.Empty, string.Empty, string.Empty);
        }
    }


    public async Task<List<Trend>> Trends()
    {
        if (!_isValid) return new List<Trend>();
        LoadConfiguration();

        try
        {
            // Create a client to connect to InfluxDB
            using var client = new InfluxDBClient(_influxHost, _influxToken);

            // Create a query that lists measurements
            var query = $"import \"influxdata/influxdb/schema\"\n\nschema.measurements(bucket: \"{_bucket}\")";

            var queryApi = client.GetQueryApi();
            var measurements = await queryApi.QueryAsync(query, _influxOrg);

            var allMeasurementStrings = measurements
                .SelectMany(table => table.Records)
                .Select(record => record.GetValue() as string ?? string.Empty).ToList();

            var measurementStrings = allMeasurementStrings
                .Where(s => s != "sensor-node").ToList();

            List<Trend> trends = new(measurementStrings.Count);

            foreach (var measurement in measurementStrings)
            {
                string unit = GetExplicitUnit(measurement) ?? measurement.GetUnit() ?? "";
                string displayName = GetDisplayName(measurement) ?? measurement;
                trends.Add(new Trend(measurement, unit, displayName, GetExplicitTags(measurement)));
            }

            if (measurementStrings.Count != allMeasurementStrings.Count)
            {
                StringBuilder queryBuiler = new();
                queryBuiler.Append($"from(bucket: \"{_bucket}\")\n");
                queryBuiler.Append(" |> range(start: -3y)\n");
                queryBuiler.Append(" |> filter(fn: (r) =>\n");
                queryBuiler.Append("  exists r._measurement and");
                queryBuiler.Append("  exists r._field and");
                queryBuiler.Append("  exists r.name and");
                queryBuiler.Append("  exists r.uuid)\n");
                queryBuiler.Append(" |> group(columns: [\"_measurement\", \"_field\", \"name\", \"uuid\"])\n");
                queryBuiler.Append(" |> first()\n");
                queryBuiler.Append(" |> yield()");
                // string query = "from(bucket: \"Siemens Grand Prairie RTUs\")\n  |> range(start: -90d)            // widen as needed\n  |> keep(columns: [\"_measurement\", \"_field\"])\n  |> group(columns: [\"_measurement\", \"_field\"])\n  |> unique(column: \"_field\")      // emits one row per unique pair\n  |> sort(columns: [\"_measurement\", \"_field\"])\n  |> yield()"


                var sensorMeasurements = await queryApi.QueryAsync(queryBuiler.ToString(), _influxOrg);

                foreach (var table in sensorMeasurements)
                {
                    var field = table.Records[0].GetField();
                    var name = (string)table.Records[0].GetValueByKey("name");
                    var uuid = (string)table.Records[0].GetValueByKey("uuid");

                    string unit = "";
                    if (field == "temp") unit = "°F";
                    else if (field == "humidity") unit = "%";
                    else if (field.StartsWith("temperature")) unit = "°F";
                    else if (field == "pressure") unit = "inH2O";

                    trends.Add(new Trend($"sensor\t{name}\t{uuid}\t{field}", unit,  $"sensor\t{name}\t{uuid}\t{field}"));
                }
            }

            return trends;
        }
        catch
        {
            return new ();
        }
    }

    public Task<List<double>> GetData(string trend)
    {
        return Task.FromResult(new List<double>());
        // throw new System.NotImplementedException();
    }

    public string Header { get; }
    public string ShortName { get; }

    public Task<DataSourceType> DataSourceType() => Task.FromResult(csvplot.DataSourceType.Database);

    public Task SetUnit(Trend trend, string? unit)
    {
        return SetUnits(new[] { trend }, unit);
    }

    public Task SetUnits(IEnumerable<Trend> trends, string? unit)
    {
        LoadConfiguration();
        string? normalizedUnit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim();
        List<Trend> trendList = trends.DistinctBy(trend => trend.Name).ToList();

        foreach (Trend trend in trendList)
        {
            PointConfig pointConfig = _configuration.GetOrCreateInfluxPoint(_bucket, trend.Name);
            pointConfig.Unit = normalizedUnit;
            _configuration.RemoveInfluxPointIfEmpty(_bucket, trend.Name);
        }

        string configPath = ConfigurationParser.GetDefaultConfigurationPath();

        try
        {
            Console.WriteLine(
                $"[InfluxConfig] Writing unit override batch. Bucket='{_bucket}', Count='{trendList.Count}', Unit='{normalizedUnit ?? "(automatic)"}', Path='{configPath}'");
            ConfigurationParser.SaveConfiguration(_configuration);
            Console.WriteLine(
                $"[InfluxConfig] Write succeeded. Bucket='{_bucket}', Count='{trendList.Count}', Path='{configPath}'");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[InfluxConfig] Write failed. Bucket='{_bucket}', Count='{trendList.Count}', Unit='{normalizedUnit ?? "(automatic)"}', Path='{configPath}', Error='{ex.Message}'");
            throw;
        }
    }

    public Task SetTags(Trend trend, IReadOnlyList<string>? tags)
    {
        return SetTags(new[] { trend }, tags);
    }

    public Task SetTags(IEnumerable<Trend> trends, IReadOnlyList<string>? tags)
    {
        LoadConfiguration();
        List<string>? normalizedTags = tags?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalizedTags is { Count: 0 })
        {
            normalizedTags = null;
        }

        List<Trend> trendList = trends.DistinctBy(trend => trend.Name).ToList();

        foreach (Trend trend in trendList)
        {
            PointConfig pointConfig = _configuration.GetOrCreateInfluxPoint(_bucket, trend.Name);
            pointConfig.Tags = normalizedTags is null ? null : normalizedTags.ToList();
            _configuration.RemoveInfluxPointIfEmpty(_bucket, trend.Name);
        }

        string configPath = ConfigurationParser.GetDefaultConfigurationPath();

        try
        {
            Console.WriteLine(
                $"[InfluxConfig] Writing tag override batch. Bucket='{_bucket}', Count='{trendList.Count}', Tags='{string.Join(",", normalizedTags ?? new List<string>())}', Path='{configPath}'");
            ConfigurationParser.SaveConfiguration(_configuration);
            Console.WriteLine(
                $"[InfluxConfig] Write succeeded. Bucket='{_bucket}', Count='{trendList.Count}', Path='{configPath}'");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[InfluxConfig] Write failed. Bucket='{_bucket}', Count='{trendList.Count}', Tags='{string.Join(",", normalizedTags ?? new List<string>())}', Path='{configPath}', Error='{ex.Message}'");
            throw;
        }
    }

    public async Task<TimestampData> GetTimestampData(string trend)
    {
        return (await GetTimestampData(new List<string>() { trend }, DateTime.Now.Date.AddDays(-7), DateTime.Now.Date.AddDays(1))).First();
    }

    public async Task<List<TimestampData>> GetTimestampData(List<string> trends) => await GetTimestampData(trends, DateTime.Now.Date.AddDays(-7), DateTime.Now.Date.AddDays(1));

    public async Task<List<TimestampData>> GetTimestampData(List<string> trends, DateTime startDateInc, DateTime endDateExc)
    {
        var startDateIncLocal = new LocalDateTime(DateTime.SpecifyKind(startDateInc, DateTimeKind.Local));
        var endDateExcLocal = new LocalDateTime(DateTime.SpecifyKind(endDateExc, DateTimeKind.Local));
        return await GetTimestampData(trends, startDateIncLocal, endDateExcLocal, 7500);
    }

    public async Task<string?> QueryFromFilter(InfluxDBClient client, UtcDateTime startDateIncUtc, UtcDateTime endDateExcUtc, string measFilter, int trendCount, int countLimit)
    {
        var queryApi = client.GetQueryApi();
        var countQuery = $"from(bucket: \"{_bucket}\") |> range(start: {startDateIncUtc.ToRfc3339Second()}, stop: {endDateExcUtc.ToRfc3339Second()}) |> filter(fn: (r) => {measFilter}) |> count() |> yield() ";
        List<FluxTable>? countTables;
        try
        {
            countTables = await queryApi.QueryAsync(countQuery, _influxOrg);
        }
        catch
        {
            return null;
            // return measurementTrends.Select(s => new TimestampData(new(), new())).ToList();
        }

        long totalCount = 0;
        foreach (var t in countTables)
        {
            foreach (var r in t.Records)
            {
                if (r.GetValue() is long l) totalCount += l;
            }
        }

        if (totalCount < countLimit)
        {
            return $"from(bucket: \"{_bucket}\") |> range(start: {startDateIncUtc.ToRfc3339Second()}, stop: {endDateExcUtc.ToRfc3339Second()}) |> filter(fn: (r) => {measFilter}) |> yield()";
        }
        else
        {
            List<int> possibleMinuteIntervals = new List<int>() { 1, 5, 10, 15, 30, 60, 120, 240, 480, 1440 };

            // Since we should be able to switch to a signal plot, our max value count limit can be very large. 105120 is 5 minute data for a year.
            long minuteInterval = trendCount * (int)(endDateExcUtc.Value - startDateIncUtc.Value).TotalMinutes / 105120;
            foreach (var interval in possibleMinuteIntervals)
            {
                if (interval >= minuteInterval)
                {
                    minuteInterval = interval;
                    break;
                }
            }
            return $"from(bucket: \"{_bucket}\") |> range(start: {startDateIncUtc.ToRfc3339Second()}, stop: {endDateExcUtc.ToRfc3339Second()}) |> filter(fn: (r) => {measFilter}) |> aggregateWindow(every: {minuteInterval}m, fn: mean, createEmpty: false) |> yield()";
        }
    }

    private static int SelectMinuteInterval(int trendCount, DateTime startUtc, DateTime endUtc, int countLimit)
    {
        List<int> possibleMinuteIntervals = new() { 1, 5, 10, 15, 30, 60, 120, 240, 480, 1440 };

        long minuteInterval = trendCount * (int)(endUtc - startUtc).TotalMinutes / countLimit;
        foreach (int interval in possibleMinuteIntervals)
        {
            if (interval >= minuteInterval)
            {
                return interval;
            }
        }

        return possibleMinuteIntervals.Last();
    }

    private static bool HasExpectedIntervals(IReadOnlyList<DateTime> timestamps, TimeSpan expectedInterval)
    {
        for (int i = 1; i < timestamps.Count; i++)
        {
            if (timestamps[i] - timestamps[i - 1] != expectedInterval)
            {
                return false;
            }
        }

        return true;
    }

    private static TimeSeriesData ToTimeSeriesData(TimestampData tsData, int? aggregatedMinuteInterval)
    {
        if (aggregatedMinuteInterval is int minuteInterval &&
            tsData.DateTimes.Count > 0 &&
            HasExpectedIntervals(tsData.DateTimes, TimeSpan.FromMinutes(minuteInterval)))
        {
            return new TimeSeriesData(
                new UniformTimeAxis(tsData.DateTimes[0], TimeSpan.FromMinutes(minuteInterval), tsData.Values.Count),
                tsData.Values);
        }

        return new TimeSeriesData(new ExplicitTimeAxis(tsData.DateTimes), tsData.Values);
    }

    private async Task<int?> GetMeasurementAggregateMinuteInterval(
        QueryApi queryApi,
        List<string> measurementTrends,
        UtcDateTime startDateIncUtc,
        UtcDateTime endDateExcUtc,
        int countLimit)
    {
        if (!measurementTrends.Any()) return null;

        string measFilter = string.Join(" or ", measurementTrends.Select(s => $"r._measurement == \"{s}\""));
        string countQuery =
            $"from(bucket: \"{_bucket}\") |> range(start: {startDateIncUtc.ToRfc3339Second()}, stop: {endDateExcUtc.ToRfc3339Second()}) |> filter(fn: (r) => {measFilter}) |> count() |> yield() ";

        List<FluxTable>? countTables = await queryApi.QueryAsync(countQuery, _influxOrg);
        long totalCount = 0;
        foreach (var t in countTables)
        {
            foreach (var r in t.Records)
            {
                if (r.GetValue() is long l) totalCount += l;
            }
        }

        return totalCount >= countLimit
            ? SelectMinuteInterval(measurementTrends.Count, startDateIncUtc.Value, endDateExcUtc.Value, countLimit)
            : null;
    }

    private async Task<int?> GetSensorAggregateMinuteInterval(
        QueryApi queryApi,
        List<string> sensorTrends,
        UtcDateTime startDateIncUtc,
        UtcDateTime endDateExcUtc,
        int countLimit)
    {
        if (!sensorTrends.Any()) return null;

        int batchSize = 50;
        int i = 0;
        long totalCount = 0;

        while (i < sensorTrends.Count)
        {
            var batchTrends = sensorTrends.Skip(i).Take(batchSize).ToList();
            i += batchSize;

            var filter = batchTrends.Select(fullName =>
            {
                var splitName = fullName.Split("\t").Select(s => s.Trim()).ToList();
                var name = splitName[1];
                var uuid = splitName[2];
                var field = splitName[3];
                return $"(r._measurement == \"sensor-node\" and r.name == \"{name}\" and r.uuid == \"{uuid}\" and r._field == \"{field}\")";
            });

            string joinedFilter = string.Join(" or \n", filter);
            string batchCountQuery =
                $"from(bucket: \"{_bucket}\") |> range(start: {startDateIncUtc.ToRfc3339Second()}, stop: {endDateExcUtc.ToRfc3339Second()}) |> filter(fn: (r) =>  {joinedFilter}) |> count() |> yield() ";

            List<FluxTable>? countTables = await queryApi.QueryAsync(batchCountQuery, _influxOrg);
            foreach (var t in countTables)
            {
                foreach (var r in t.Records)
                {
                    if (r.GetValue() is long l) totalCount += l;
                }
            }
        }

        return totalCount >= countLimit
            ? SelectMinuteInterval(sensorTrends.Count, startDateIncUtc.Value, endDateExcUtc.Value, countLimit)
            : null;
    }

    public async Task<List<TimestampData>> GetTimestampDataClaraxioSensors(List<string> trends, UtcDateTime startDateIncUtc, UtcDateTime endDateExcUtc, int countLimit)
    {
        // Get the last year of data from InfluxDb.
        if (!_isValid) return trends.Select(s => new TimestampData(new(), new())).ToList();


        // var httpClientHandler = new HttpClientHandler();
        // var httpClient = new HttpClient(httpClientHandler)
        // {
        //     Timeout = TimeSpan.FromMinutes(5)
        // };

        var options = InfluxDBClientOptions.Builder.CreateNew().Url(_influxHost).AuthenticateToken(_influxToken).TimeOut(TimeSpan.FromMinutes(1));
        using var client = new InfluxDBClient(options.Build());

        var sensorTrends = trends.Where(s => s.StartsWith("sensor\t")).ToList();

        int batchSize = 50;
        int i = 0;

        long totalCount = 0;
        var queryApi = client.GetQueryApi();
        while (i < sensorTrends.Count)
        {
            var batchTrends = sensorTrends.Skip(i).Take(batchSize).ToList();
            i += batchSize;

            var filter = batchTrends.Select(fullName =>
            {
                var splitName = fullName.Split("\t").Select(s => s.Trim()).ToList();
                var name = splitName[1];
                var uuid = splitName[2];
                var field = splitName[3];
                return $"(r._measurement == \"sensor-node\" and r.name == \"{name}\" and r.uuid == \"{uuid}\" and r._field == \"{field}\")";

            });

            var joinedFilter = string.Join(" or \n", filter);
            var batchCountQuery= $"from(bucket: \"{_bucket}\") |> range(start: {startDateIncUtc.ToRfc3339Second()}, stop: {endDateExcUtc.ToRfc3339Second()}) |> filter(fn: (r) =>  {joinedFilter}) |> count() |> yield() ";
            List<FluxTable>? countTables;
            try
            {
                countTables = await queryApi.QueryAsync(batchCountQuery, _influxOrg);
            }
            catch
            {
                return sensorTrends.Select(s => new TimestampData(new(), new())).ToList();
            }

            foreach (var t in countTables)
            {
                foreach (var r in t.Records)
                {
                    if (r.GetValue() is long l) totalCount += l;
                }
            }
        }

        // var countQuery = $"from(bucket: \"{_bucket}\") |> range(start: {startDateInc:yyyy-MM-dd}, stop: {endDateExc:yyyy-MM-dd}) |> map(fn: (r) => {{ r with _k:  strings.joinStr(arr: [\"sensor\", r.name, r.uuid, r._field], v: \":\") }} ) |> filter(fn: (r) => contains(set: allow, value: r._k)) |> count() |> yield() ";
        // b.Append(countQuery);

        List<TimestampData> data = new();

        if (totalCount < countLimit)
        {
            // query = $"from(bucket: \"{_bucket}\") |> range(start: {startDateInc:yyyy-MM-dd}, stop: {endDateExc:yyyy-MM-dd}) |> filter(fn: (r) => contains(set: allow, value: r._k)) |> yield()";
            i = 0;
            while (i < sensorTrends.Count)
            {
                var batchTrends = sensorTrends.Skip(i).Take(batchSize).ToList();
                i += batchSize;

                var filter = batchTrends.Select(fullName =>
                {
                    var splitName = fullName.Split("\t").Select(s => s.Trim()).ToList();
                    var name = splitName[1];
                    var uuid = splitName[2];
                    var field = splitName[3];
                    return $"(r._measurement == \"sensor-node\" and r.name == \"{name}\" and r.uuid == \"{uuid}\" and r._field == \"{field}\")";

                });

                var joinedFilter = string.Join(" or \n", filter);
                var batchQuery = $"from(bucket: \"{_bucket}\") |> range(start: {startDateIncUtc.ToRfc3339Second()}, stop: {endDateExcUtc.ToRfc3339Second()}) |> filter(fn: (r) =>  {joinedFilter}) |> yield() ";

                data.AddRange(await GetDataFromQuery(queryApi, batchQuery, batchTrends));
            }
        }
        else
        {
            List<int> possibleMinuteIntervals = new List<int>() { 1, 5, 10, 15, 30, 60, 120, 240, 480, 1440 };

            long minuteInterval = sensorTrends.Count * (int)(endDateExcUtc.Value - startDateIncUtc.Value).TotalMinutes / countLimit;
            foreach (var interval in possibleMinuteIntervals)
            {
                if (interval >= minuteInterval)
                {
                    minuteInterval = interval;
                    break;
                }
            }

            i = 0;
            while (i < sensorTrends.Count)
            {
                var batchTrends = sensorTrends.Skip(i).Take(batchSize).ToList();
                i += batchSize;

                var filter = batchTrends.Select(fullName =>
                {
                    var splitName = fullName.Split("\t").Select(s => s.Trim()).ToList();
                    var name = splitName[1];
                    var uuid = splitName[2];
                    var field = splitName[3];
                    return $"(r._measurement == \"sensor-node\" and r.name == \"{name}\" and r.uuid == \"{uuid}\" and r._field == \"{field}\")";

                });

                var joinedFilter = string.Join(" or \n", filter);
                var batchQuery = $"from(bucket: \"{_bucket}\") |> range(start: {startDateIncUtc.ToRfc3339Second()}, stop: {endDateExcUtc.ToRfc3339Second()}) |> filter(fn: (r) =>  {joinedFilter}) |> aggregateWindow(every: {minuteInterval}m, fn: mean, createEmpty: false ) |> yield() ";

                data.AddRange(await GetDataFromQuery(queryApi, batchQuery, batchTrends));
            }
        }

        // var queryApi = client.GetQueryApi();

        return data;
    }

    public async Task<TimeSeriesData> GetTimeSeriesData(string trend)
    {
        return (await GetTimeSeriesData(new List<string>() { trend }, DateTime.Now.Date.AddDays(-7), DateTime.Now.Date.AddDays(1))).First();
    }

    public async Task<List<TimeSeriesData>> GetTimeSeriesData(List<string> trends) =>
        await GetTimeSeriesData(trends, DateTime.Now.Date.AddDays(-7), DateTime.Now.Date.AddDays(1));

    public async Task<List<TimeSeriesData>> GetTimeSeriesData(List<string> trends, DateTime startDateInc, DateTime endDateExc)
    {
        var startDateIncLocal = new LocalDateTime(DateTime.SpecifyKind(startDateInc, DateTimeKind.Local));
        var endDateExcLocal = new LocalDateTime(DateTime.SpecifyKind(endDateExc, DateTimeKind.Local));
        return await GetTimeSeriesData(trends, startDateIncLocal, endDateExcLocal, 7500);
    }

    private async Task<List<TimestampData>> GetDataFromQuery(QueryApi queryApi, string query, List<string> sensorTrends)
    {
        List<FluxTable>? tables;
        try
        {
            tables = await queryApi.QueryAsync(query, _influxOrg);
        }
        catch
        {
            return new List<TimestampData>();
        }

        Dictionary<string, List<FluxRecord>> trendData = tables.SelectMany(table => table.Records)
            .GroupBy(record =>  $"sensor\t{record.GetValueByKey("name")}\t{record.GetValueByKey("uuid")}\t{record.GetField()}")
            .ToDictionary(records => records.Key, records => records.ToList());

        List<TimestampData> data = new();
        foreach (var trend in sensorTrends)
        {
            List<DateTime> timestamps = new();
            List<double> values = new();
            if (trendData.TryGetValue(trend, out var dataRecords))
            {
                foreach (var record in dataRecords)
                {
                    if (record.GetValue() is not double doubleValue || record.GetTimeInDateTime() is not { } d)
                        continue;
                    values.Add(doubleValue);

                    var dLocal = TimeZoneInfo.ConvertTimeFromUtc(d, TimeZoneInfo.Local);
                    timestamps.Add(dLocal);
                }
            }
            data.Add(new TimestampData(timestamps, values));
        }

        return data;
    }


    /// <summary>
    /// Get Timestamp Data from Influx using our normal Influx schemas
    /// </summary>
    /// <param name="trends">Trend names that we are looking for.</param>
    /// <param name="startDateIncLocal">This is a start date, viewed from the time zone of the user.</param>
    /// <param name="endDateExcLocal">This is the end date, viewed from the time zone of the user.</param>
    /// <param name="countLimit"></param>
    /// <returns></returns>
    public async Task<List<TimestampData>> GetTimestampData(List<string> trends, LocalDateTime startDateIncLocal, LocalDateTime endDateExcLocal, int countLimit)
    {
        // Get the last year of data from InfluxDb.
        if (!_isValid) return trends.Select(s => new TimestampData(new(), new())).ToList();

        var startDateIncUtc = new UtcDateTime(TimeZoneInfo.ConvertTimeToUtc(startDateIncLocal.Value, TimeZoneInfo.Local));
        var endDateExcUtc =   new UtcDateTime(TimeZoneInfo.ConvertTimeToUtc(endDateExcLocal.Value, TimeZoneInfo.Local));
        // var httpClientHandler = new HttpClientHandler();
        // var httpClient = new HttpClient(httpClientHandler)
        // {
        //     Timeout = TimeSpan.FromMinutes(5)
        // };

        var options = InfluxDBClientOptions.Builder.CreateNew().Url(_influxHost).AuthenticateToken(_influxToken).TimeOut(TimeSpan.FromMinutes(1));
        using var client = new InfluxDBClient(options.Build());

        List<string> measurementTrends = new List<string>();
        List<string> claraxioSensorTrends = new List<string>();

        foreach (var t in trends)
        {
            if (t.StartsWith("sensor\t"))
            {
                claraxioSensorTrends.Add(t);
            }
            else
            {
                measurementTrends.Add(t);
            }
        }

        List<TimestampData> data = new();
        if (measurementTrends.Any())
        {
            // using var client = new InfluxDBClient(_influxHost, _influxToken);
            string measFilter = string.Join(" or ", measurementTrends.Select(s => $"r._measurement == \"{s}\""));

            var measurementTrendQuery = await QueryFromFilter(client, startDateIncUtc, endDateExcUtc, measFilter, measurementTrends.Count, countLimit);

            var queryApi = client.GetQueryApi();

            List<FluxTable>? tables;
            try
            {
                tables = await queryApi.QueryAsync(measurementTrendQuery, _influxOrg);
            }
            catch
            {
                return measurementTrends.Select(s => new TimestampData(new(), new())).ToList();
            }

            Dictionary<string, List<FluxRecord>> trendData = tables.SelectMany(table => table.Records)
                .GroupBy(record => record.GetMeasurement())
                .ToDictionary(records => records.Key, records => records.ToList());

            foreach (var trend in measurementTrends)
            {
                List<DateTime> timestamps = new();
                List<double> values = new();
                if (trendData.TryGetValue(trend, out var dataRecords))
                {
                    foreach (var record in dataRecords)
                    {
                        if (record.GetValue() is not double doubleValue || record.GetTimeInDateTime() is not { } d)
                            continue;
                        values.Add(doubleValue);
                        var dLocal = TimeZoneInfo.ConvertTimeFromUtc(d, TimeZoneInfo.Local);
                        timestamps.Add(dLocal);
                    }
                }
                data.Add(new TimestampData(timestamps, values));
            }
        }

        List<TimestampData> claraxioData = await GetTimestampDataClaraxioSensors(claraxioSensorTrends, startDateIncUtc, endDateExcUtc, countLimit);
        data.AddRange(claraxioData);
        return data;
    }

    public async Task<List<TimeSeriesData>> GetTimeSeriesData(List<string> trends, LocalDateTime startDateIncLocal, LocalDateTime endDateExcLocal, int countLimit)
    {
        List<TimestampData> timestampData = await GetTimestampData(trends, startDateIncLocal, endDateExcLocal, countLimit);
        if (!_isValid)
        {
            return timestampData
                .Select(tsData => new TimeSeriesData(new ExplicitTimeAxis(tsData.DateTimes), tsData.Values))
                .ToList();
        }

        List<string> measurementTrends = trends.Where(t => !t.StartsWith("sensor\t")).ToList();
        List<string> claraxioSensorTrends = trends.Where(t => t.StartsWith("sensor\t")).ToList();

        var startDateIncUtc = new UtcDateTime(TimeZoneInfo.ConvertTimeToUtc(startDateIncLocal.Value, TimeZoneInfo.Local));
        var endDateExcUtc = new UtcDateTime(TimeZoneInfo.ConvertTimeToUtc(endDateExcLocal.Value, TimeZoneInfo.Local));

        var options = InfluxDBClientOptions.Builder.CreateNew().Url(_influxHost).AuthenticateToken(_influxToken).TimeOut(TimeSpan.FromMinutes(1));
        using var client = new InfluxDBClient(options.Build());
        var queryApi = client.GetQueryApi();

        int? measurementAggregateMinuteInterval = null;
        int? sensorAggregateMinuteInterval = null;

        try
        {
            measurementAggregateMinuteInterval = await GetMeasurementAggregateMinuteInterval(
                queryApi,
                measurementTrends,
                startDateIncUtc,
                endDateExcUtc,
                countLimit);

            sensorAggregateMinuteInterval = await GetSensorAggregateMinuteInterval(
                queryApi,
                claraxioSensorTrends,
                startDateIncUtc,
                endDateExcUtc,
                countLimit);
        }
        catch
        {
        }

        List<TimeSeriesData> data = new(timestampData.Count);
        for (int i = 0; i < trends.Count && i < timestampData.Count; i++)
        {
            int? aggregateMinuteInterval = trends[i].StartsWith("sensor\t")
                ? sensorAggregateMinuteInterval
                : measurementAggregateMinuteInterval;
            data.Add(ToTimeSeriesData(timestampData[i], aggregateMinuteInterval));
        }

        return data;
    }

    public string GetScript(List<string> trends, DateTime startDateIncLocal, DateTime endDateExcLocal)
    {
        var startDateIncUtc = new UtcDateTime(TimeZoneInfo.ConvertTimeToUtc(startDateIncLocal, TimeZoneInfo.Local));
        var endDateExcUtc = new UtcDateTime(TimeZoneInfo.ConvertTimeToUtc(endDateExcLocal, TimeZoneInfo.Local));
        string measFilter = string.Join(" or ", trends.Select(s => $"r._measurement == \"{s}\""));
        string query = $"from(bucket: \"{_bucket}\") |> range(start: {startDateIncUtc.ToRfc3339Second()}, stop: {endDateExcUtc.ToRfc3339Second()}) |> filter(fn: (r) => {measFilter}) |> yield()";
        return $"influx query -r \"{query}\"";
    }

    public Task UpdateCache()
    {
        return Task.CompletedTask;
    }

    private void LoadConfiguration()
    {
        _configuration = ConfigurationParser.LoadConfiguration();

        try
        {
            string legacyConfigPath = GetLegacyBucketConfigurationPath();
            using FileStream stream = new(legacyConfigPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Configuration legacyConfiguration = ConfigurationParser.LoadConfiguration(stream);
            MergeMissingPointConfiguration(legacyConfiguration);
        }
        catch
        {
            // Ignore missing legacy configs.
        }
    }

    private void MergeMissingPointConfiguration(Configuration other)
    {
        Dictionary<string, PointConfig>? otherPoints = other.GetInfluxPoints(_bucket);
        if (otherPoints is null) return;

        foreach (var (pointName, otherPoint) in otherPoints)
        {
            PointConfig targetPoint = _configuration.GetOrCreateInfluxPoint(_bucket, pointName);

            if (string.IsNullOrWhiteSpace(targetPoint.Unit) && !string.IsNullOrWhiteSpace(otherPoint.Unit))
            {
                targetPoint.Unit = otherPoint.Unit;
            }

            if (string.IsNullOrWhiteSpace(targetPoint.Alias) && !string.IsNullOrWhiteSpace(otherPoint.Alias))
            {
                targetPoint.Alias = otherPoint.Alias;
            }

            if (string.IsNullOrWhiteSpace(targetPoint.Container) && !string.IsNullOrWhiteSpace(otherPoint.Container))
            {
                targetPoint.Container = otherPoint.Container;
            }

            if ((targetPoint.Tags is null || targetPoint.Tags.Count == 0) && otherPoint.Tags is { Count: > 0 })
            {
                targetPoint.Tags = otherPoint.Tags.ToList();
            }
        }
    }

    private string? GetExplicitUnit(string trendName)
    {
        Dictionary<string, PointConfig>? points = _configuration.GetInfluxPoints(_bucket);
        if (points is null) return null;
        if (!points.TryGetValue(trendName, out PointConfig? pointConfig)) return null;
        return string.IsNullOrWhiteSpace(pointConfig.Unit) ? null : pointConfig.Unit;
    }

    private string? GetDisplayName(string trendName)
    {
        Dictionary<string, PointConfig>? points = _configuration.GetInfluxPoints(_bucket);
        if (points is null) return null;
        if (!points.TryGetValue(trendName, out PointConfig? pointConfig)) return null;
        return string.IsNullOrWhiteSpace(pointConfig.Alias) ? null : pointConfig.Alias;
    }

    private List<string>? GetExplicitTags(string trendName)
    {
        Dictionary<string, PointConfig>? points = _configuration.GetInfluxPoints(_bucket);
        if (points is null) return null;
        if (!points.TryGetValue(trendName, out PointConfig? pointConfig)) return null;
        if (pointConfig.Tags is not { Count: > 0 }) return null;
        return pointConfig.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.Ordinal).ToList();
    }

    private string GetLegacyBucketConfigurationPath()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mplotter", "influx", $"{_bucket}.json");
        }

        string? home = Environment.GetEnvironmentVariable("HOME");
        return home is null
            ? Path.Combine("influx", $"{_bucket}.json")
            : Path.Combine(home, ".config", "mplotter", "influx", $"{_bucket}.json");
    }
}
