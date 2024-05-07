using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace csvplot;

public class Bac0DataSource : IDataSource
{
    private readonly string _sqliteFilePath;
    private readonly List<string> _trends = new();

    public Bac0DataSource(string sqliteFilePath)
    {
        _sqliteFilePath = sqliteFilePath;
        RefreshTrends();
    }

    private void RefreshTrends()
    {
        using SQLiteConnection conn = new SQLiteConnection(_sqliteFilePath.ToSqliteConnString());
        conn.Open();
        var historyCols = conn.GetSchema("COLUMNS", new[] { null, null, "history" });

        _trends.Clear();
        for (int i = 1; i < historyCols.Rows.Count; i++)
        {
            _trends.Add((string)historyCols.Rows[i]["COLUMN_NAME"]);
        }
    }

    public Task<List<string>> Trends() => Task.FromResult(_trends);

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

        await using SQLiteConnection conn = new SQLiteConnection(_sqliteFilePath.ToSqliteConnString());
        conn.Open();

        List<DateTime> dates = new();
        List<double> values = new();

        try
        {
            await using SQLiteCommand cmd = new SQLiteCommand($"SELECT \"index\", \"{trend}\" FROM history", conn);

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
        RefreshTrends();
        return Task.CompletedTask;
    }
}
