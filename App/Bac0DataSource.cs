using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;

namespace csvplot;

public class Bac0DataSource : IDataSource
{
    private readonly string _sqliteFilePath;
    private readonly List<string> _trends;

    public Bac0DataSource(string sqliteFilePath)
    {
        _sqliteFilePath = sqliteFilePath;

        using SQLiteConnection conn = new SQLiteConnection(_sqliteFilePath.ToSqliteConnString());

        conn.Open();

        var historyCols = conn.GetSchema("COLUMNS", new[] { null, null, "history" });

        _trends = new List<string>();
        for (int i = 1; i < historyCols.Rows.Count; i++)
        {
            _trends.Add((string)historyCols.Rows[i]["COLUMN_NAME"]);
        }
    }

    public Task<List<string>> Trends() => Task.FromResult(_trends);

    public List<double> GetData(string trend)
    {
        throw new NotImplementedException();
    }

    public string Header => _sqliteFilePath;
    public string ShortName => Path.GetFileName(_sqliteFilePath);
    public DataSourceType DataSourceType => DataSourceType.TimeSeries;
    public TimestampData GetTimestampData(string trend)
    {
        // Check that trend is in possible trends
        if (!_trends.Contains(trend)) return new TimestampData(new(), new());

        using SQLiteConnection conn = new SQLiteConnection(_sqliteFilePath.ToSqliteConnString());
        conn.Open();

        List<DateTime> dates = new();
        List<double> values = new();

        try
        {
            using SQLiteCommand cmd = new SQLiteCommand($"SELECT \"index\", \"{trend}\" FROM history", conn);

            using SQLiteDataReader reader = cmd.ExecuteReader();

            int trendCol = reader.GetOrdinal(trend);
            if (trendCol < 0) return new TimestampData(new(), new());

            while (reader.Read())
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

    public List<TimestampData> GetTimestampData(List<string> trends, DateTime startDateInc, DateTime endDateExc)
    {
        List<TimestampData> data = new();
        foreach (var trend in trends)
        {
            TimestampData tsData = GetTimestampData(trend);
            tsData.TrimDates(startDateInc, endDateExc);
            data.Add(tsData);
        }

        return data;
    }
}