using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SemaphoreSlim = System.Threading.SemaphoreSlim;

namespace csvplot;

public class Bac0DataSource : IDataSource
{
    private readonly string _sqliteFilePath;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private List<string> _trends = new();

    public Bac0DataSource(string sqliteFilePath)
    {
        _sqliteFilePath = sqliteFilePath;
        try
        {
            _trends = LoadTrends();
        }
        catch
        {
            _trends = new();
        }
    }

    private List<string> LoadTrends()
    {
        using SqliteConnection conn = new SqliteConnection(_sqliteFilePath.ToSqliteConnString());
        conn.Open();

        using SqliteCommand cmd = new SqliteCommand("PRAGMA table_info(history)", conn);
        using var reader = cmd.ExecuteReader();

        int nameOrdinal = reader.GetOrdinal("name");
        List<string> trends = new();
        bool skippedFirst = false;
        while (reader.Read())
        {
            if (!skippedFirst)
            {
                skippedFirst = true;
                continue;
            }
            trends.Add(reader.GetString(nameOrdinal));
        }

        return trends;
    }

    public async Task<List<Trend>> Trends()
    {
        await _cacheLock.WaitAsync();
        try
        {
            return _trends.Select(s => new Trend(s, s.GetUnit() ?? "", s)).ToList();
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public Task<List<double>> GetData(string trend)
    {
        return Task.FromResult<List<double>>(new());
    }

    public string Header => _sqliteFilePath;
    public string ShortName => Path.GetFileName(_sqliteFilePath);
    public Task<DataSourceType> DataSourceType() => Task.FromResult(csvplot.DataSourceType.TimeSeries);
    public async Task<TimestampData> GetTimestampData(string trend)
    {
        // Check that trend is in possible trends
        if (!_trends.Contains(trend)) return new TimestampData(new(), new());

        await using SqliteConnection conn = new SqliteConnection(_sqliteFilePath.ToSqliteConnString());
        conn.Open();

        List<DateTime> dates = new();
        List<double> values = new();

        try
        {
            await using SqliteCommand cmd = new SqliteCommand($"SELECT \"index\", \"{trend}\" FROM history", conn);

            await using var reader = await cmd.ExecuteReaderAsync();

            int trendCol = reader.GetOrdinal(trend);
            if (trendCol < 0) return new TimestampData(new(), new());

            while (await reader.ReadAsync())
            {
                string dateTimeStr = reader.GetString(0);
                var dateTimeParseSuccess = DateTime.TryParse(dateTimeStr, out var parsedDateTime);
                if (!dateTimeParseSuccess) continue;

                double value = reader.GetDouble(trendCol);

                dates.Add(parsedDateTime);
                values.Add(value);
            }
        }
        catch
        {
            return new TimestampData(new List<DateTime>(0), new List<double>(0));
        }

        return new TimestampData(dates, values);
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

    public Task UpdateCache()
    {
        return UpdateCacheInternal();
    }

    private async Task UpdateCacheInternal()
    {
        await _cacheLock.WaitAsync();
        try
        {
            List<string> trends = LoadTrends();
            _trends = trends;
        }
        catch
        {
            // Keep the last good snapshot.
        }
        finally
        {
            _cacheLock.Release();
        }
    }
}
