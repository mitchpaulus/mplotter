using System;
using System.Collections.Generic;
using System.Linq;
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
        DataSourceType = DataSourceType.Database;
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


    public async Task<List<string>> Trends()
    {
        if (!_isValid) return new List<string>();

        try
        {
            // Create a client to connect to InfluxDB
            using var client = new InfluxDBClient(_influxHost, _influxToken);

            // Create a query that lists measurements
            var query = $"import \"influxdata/influxdb/schema\"\n\nschema.measurements(bucket: \"{_bucket}\")";

            var queryApi = client.GetQueryApi();
            var measurements = await queryApi.QueryAsync(query, _influxOrg);

            var measurementStrings = measurements
                .SelectMany(table => table.Records)
                .Select(record => record.GetValue() as string ?? string.Empty).ToList();

            // Print out the list of measurements
            return measurementStrings;
        }
        catch
        {
            return new List<string>();
        }
    }

    public List<double> GetData(string trend)
    {
        return new List<double>();
        // throw new System.NotImplementedException();
    }

    public string Header { get; }
    public string ShortName { get; }
    public DataSourceType DataSourceType { get; }

    public TimestampData GetTimestampData(string trend)
    {
        // Get the last year of data from InfluxDb.
        if (!_isValid) return new TimestampData(new List<DateTime>(), new List<double>());

        using var client = new InfluxDBClient(_influxHost, _influxToken);

        var query = $"from(bucket: \"{_bucket}\") |> range(start: -1y) |> filter(fn: (r) => r._measurement == \"{trend}\") |> yield()";

        var queryApi = client.GetQueryApi();
        List<FluxTable>? tables;
        try
        {
            tables = queryApi.QueryAsync(query, _influxOrg).Result;
        }
        catch
        {
            return new TimestampData(new(), new());
        }

        var timestamps = new List<DateTime>();
        var values = new List<double>();

        foreach (var record in tables.SelectMany(table => table.Records))
        {
            // Try to get value as double, else skip
            if (record.GetValue() is not double doubleValue || record.GetTimeInDateTime() is not { } d) continue;
            values.Add(doubleValue);
            timestamps.Add(d);
        }

        return new TimestampData(timestamps, values);
    }

    public List<TimestampData> GetTimestampData(List<string> trends) => GetTimestampData(trends, DateTime.Now.Date.AddDays(-365), DateTime.Now.Date.AddDays(1));

    public List<TimestampData> GetTimestampData(List<string> trends, DateTime startDateInc, DateTime endDateExc)
    {
        // Get the last year of data from InfluxDb.
        if (!_isValid) return trends.Select(s => new TimestampData(new(), new())).ToList();

        using var client = new InfluxDBClient(_influxHost, _influxToken);

        string measFilter = string.Join(" or ", trends.Select(s => $"r._measurement == \"{s}\""));

        var query = $"from(bucket: \"{_bucket}\") |> range(start: {startDateInc:yyyy-MM-dd}, stop: {endDateExc:yyyy-MM-dd}) |> filter(fn: (r) => {measFilter}) |> yield()";

        var queryApi = client.GetQueryApi();
        List<FluxTable>? tables;
        try
        {
            tables = queryApi.QueryAsync(query, _influxOrg).Result;
        }
        catch
        {
            return trends.Select(s => new TimestampData(new(), new())).ToList();
        }

        Dictionary<string, List<FluxRecord>> trendData = tables.SelectMany(table => table.Records)
            .GroupBy(record => record.GetMeasurement())
            .ToDictionary(records => records.Key, records => records.ToList());

        List<TimestampData> data = new();
        foreach (var trend in trends)
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
}
