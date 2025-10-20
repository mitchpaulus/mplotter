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

public class InfluxDataSource : IDataSource
{
    private readonly string _bucket;

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

            // Try to read configuration from %APPDATA%/mplotter/influx/{_bucket}.json
            Dictionary<string, string> unitMap = new();
            Dictionary<string, string> displayNameMap = new();
            try
            {
                FileStream stream = new FileStream(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mplotter", "influx", $"{_bucket}.json"), FileMode.Open);
                var config = ConfigurationParser.LoadConfiguration(stream);

                foreach (var point in config.Points)
                {
                    unitMap[point.Name] = point.Unit;
                    displayNameMap[point.Name] = point.DisplayName;
                }
            }
            catch
            {
                // Do nothing
            }

            List<Trend> trends = new(measurementStrings.Count);

            foreach (var measurement in measurementStrings)
            {
                string unit = unitMap.GetValueOrDefault(measurement, "");
                string displayName = displayNameMap.GetValueOrDefault(measurement, measurement);
                trends.Add(new Trend(measurement, unit, displayName));
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

    public async Task<TimestampData> GetTimestampData(string trend)
    {
        return (await GetTimestampData(new List<string>() { trend }, DateTime.Now.Date.AddDays(-7), DateTime.Now.Date.AddDays(1))).First();
    }

    public async Task<List<TimestampData>> GetTimestampData(List<string> trends) => await GetTimestampData(trends, DateTime.Now.Date.AddDays(-7), DateTime.Now.Date.AddDays(1));

    public async Task<List<TimestampData>> GetTimestampData(List<string> trends, DateTime startDateInc, DateTime endDateExc)
    {
        return await GetTimestampData(trends, startDateInc, endDateExc, 7500);
    }

    public async Task<string?> QueryFromFilter(InfluxDBClient client, DateTime startDateInc, DateTime endDateExc, string measFilter, int trendCount, int countLimit)
    {
        var queryApi = client.GetQueryApi();
        var countQuery = $"from(bucket: \"{_bucket}\") |> range(start: {startDateInc:yyyy-MM-dd}, stop: {endDateExc:yyyy-MM-dd}) |> filter(fn: (r) => {measFilter}) |> count() |> yield() ";
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
            return $"from(bucket: \"{_bucket}\") |> range(start: {startDateInc:yyyy-MM-dd}, stop: {endDateExc:yyyy-MM-dd}) |> filter(fn: (r) => {measFilter}) |> yield()";
        }
        else
        {
            List<int> possibleMinuteIntervals = new List<int>() { 1, 5, 10, 15, 30, 60, 120, 240, 480, 1440 };

            long minuteInterval = trendCount * (int)(endDateExc - startDateInc).TotalMinutes / countLimit;
            foreach (var interval in possibleMinuteIntervals)
            {
                if (interval >= minuteInterval)
                {
                    minuteInterval = interval;
                    break;
                }
            }
            return $"from(bucket: \"{_bucket}\") |> range(start: {startDateInc:yyyy-MM-dd}, stop: {endDateExc:yyyy-MM-dd}) |> filter(fn: (r) => {measFilter}) |> aggregateWindow(every: {minuteInterval}m, fn: mean, createEmpty: false) |> yield()";
        }
    }

    public async Task<List<TimestampData>> GetTimestampDataClaraxioSensors(List<string> trends, DateTime startDateInc, DateTime endDateExc, int countLimit)
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
            var batchCountQuery= $"from(bucket: \"{_bucket}\") |> range(start: {startDateInc:yyyy-MM-dd}, stop: {endDateExc:yyyy-MM-dd}) |> filter(fn: (r) =>  {joinedFilter}) |> count() |> yield() ";
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
                var batchQuery = $"from(bucket: \"{_bucket}\") |> range(start: {startDateInc:yyyy-MM-dd}, stop: {endDateExc:yyyy-MM-dd}) |> filter(fn: (r) =>  {joinedFilter}) |> yield() ";

                data.AddRange(await GetDataFromQuery(queryApi, batchQuery, batchTrends));
            }
        }
        else
        {
            List<int> possibleMinuteIntervals = new List<int>() { 1, 5, 10, 15, 30, 60, 120, 240, 480, 1440 };

            long minuteInterval = sensorTrends.Count * (int)(endDateExc - startDateInc).TotalMinutes / countLimit;
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
                var batchQuery = $"from(bucket: \"{_bucket}\") |> range(start: {startDateInc:yyyy-MM-dd}, stop: {endDateExc:yyyy-MM-dd}) |> filter(fn: (r) =>  {joinedFilter}) |> aggregateWindow(every: {minuteInterval}m, fn: mean, createEmpty: false ) |> yield() ";

                data.AddRange(await GetDataFromQuery(queryApi, batchQuery, batchTrends));
            }
        }

        // var queryApi = client.GetQueryApi();

        return data;
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
                    timestamps.Add(d);
                }
            }
            data.Add(new TimestampData(timestamps, values));
        }

        return data;
    }


    public async Task<List<TimestampData>> GetTimestampData(List<string> trends, DateTime startDateInc, DateTime endDateExc, int countLimit)
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
            var measurementTrendQuery = await QueryFromFilter(client, startDateInc, endDateExc, measFilter, measurementTrends.Count, countLimit);

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
                        timestamps.Add(d);
                    }
                }
                data.Add(new TimestampData(timestamps, values));
            }
        }

        List<TimestampData> claraxioData = await GetTimestampDataClaraxioSensors(claraxioSensorTrends, startDateInc, endDateExc, countLimit);
        data.AddRange(claraxioData);
        return data;
    }

    public string GetScript(List<string> trends, DateTime startDateInc, DateTime endDateExc)
    {
        string measFilter = string.Join(" or ", trends.Select(s => $"r._measurement == \"{s}\""));
        string query = $"from(bucket: \"{_bucket}\") |> range(start: {startDateInc:yyyy-MM-dd}, stop: {endDateExc:yyyy-MM-dd}) |> filter(fn: (r) => {measFilter}) |> yield()";
        return $"influx query -r \"{query}\"";
    }

    public Task UpdateCache()
    {
        return Task.CompletedTask;
    }
}
