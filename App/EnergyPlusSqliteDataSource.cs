using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScottPlot;

namespace csvplot;

public class EnergyPlusSqliteDataSource : IDataSource
{
    private List<Trend>? _trends;
    private readonly string _connectionString;

    public EnergyPlusSqliteDataSource(string sourceFile)
    {
        Header = sourceFile;
        _connectionString = $"Data Source={sourceFile};";
    }

    public async Task<List<Trend>> Trends()
    {
        if (_trends is not null && _trends.Any()) return _trends;

        try
        {
            await using SQLiteConnection conn = new SQLiteConnection(_connectionString);
            conn.Open();
            string sql = "SELECT KeyValue, Name, ReportingFrequency, Units FROM ReportDataDictionary";
            await using SQLiteCommand cmd = new SQLiteCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            _trends = new List<Trend>();

            while (await reader.ReadAsync())
            {
                object keyValueObj = reader["KeyValue"];
                string keyValue;
                if (keyValueObj is DBNull)
                {
                    keyValue = "";
                }
                else
                {
                    keyValue = (string)keyValueObj;
                }

                var name = (string)reader["Name"];
                var units = (string)reader["Units"];
                var reportingFrequency = (string)reader["ReportingFrequency"];

                if (reportingFrequency == "Hourly")
                {
                    string fullName = $"{keyValue}: {name} [{units}]";
                    _trends.Add(new Trend(fullName, units));
                }
            }
        }
        catch
        {
            return new();
        }

        return _trends;
    }

    public async Task<List<double>> GetData(string trend)
    {
        // Split name into keyValue, name, units

        var strings = trend.Split(':');
        var keyValue = strings[0].Trim();
        var name = strings[1].Trim().Split('[')[0].Trim();
        var units = strings[1].Trim().Split('[')[1].Trim().TrimEnd(']');

        await using SQLiteConnection conn = new SQLiteConnection(_connectionString);
        conn.Open();

        // First get the 'ReportDataDictionaryIndex' for the trend
        string sql = $"SELECT ReportDataDictionaryIndex FROM ReportDataDictionary WHERE KeyValue = '{keyValue}' and Name = '{name}' and Units = '{units}'";

        // SQLite returning 64 bit ints for whatever reason.
        object reportDataDictionaryIndex = -1;
        await using (SQLiteCommand cmd = new SQLiteCommand(sql, conn))
        {
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                // Should only be one row
                var hasRow = await reader.ReadAsync();

                // Return empty array if no row found
                if (!hasRow) return new List<double>();

                reportDataDictionaryIndex =  reader["ReportDataDictionaryIndex"];
            }
        }

        // Now get the data for the trend
        // Ex SQL
        // SELECT Time.Year, Time.Month, Time.Day, Time.Hour, Time.Minute, ReportVariableData.VariableValue
        // FROM Time
        // JOIN ReportVariableData ON Time.TimeIndex = ReportVariableData.TimeIndex
        // WHERE (DayType = "Sunday" or DayType = "Monday" or DayType = "Tuesday"
        // or DayType = "Wednesday" or DayType = "Thursday" or DayType = "Friday"
        // or DayType = "Saturday")
        // and ReportVariableData.ReportVariableDataDictionaryIndex = 23 ;

        StringBuilder b = new();
        b.Append("SELECT Time.Year, Time.Month, Time.Day, Time.Hour, Time.Minute, ReportVariableData.VariableValue ");
        b.Append("FROM Time ");
        b.Append("JOIN ReportVariableData ON Time.TimeIndex = ReportVariableData.TimeIndex ");
        b.Append("WHERE (DayType = \"Sunday\" or DayType = \"Monday\" or DayType = \"Tuesday\" ");
        b.Append("or DayType = \"Wednesday\" or DayType = \"Thursday\" or DayType = \"Friday\" ");
        b.Append("or DayType = \"Saturday\") ");
        b.Append($"and ReportVariableData.ReportVariableDataDictionaryIndex = {reportDataDictionaryIndex} ;");

        Stopwatch w = new();
        w.Restart();
        sql = b.ToString();
        var data = new List<double>(8760);
        await using (var cmd = new SQLiteCommand(sql, conn))
        {
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var variableValue = reader.GetDouble("VariableValue");
                    data.Add(variableValue);
                }

            }
        }
        w.Stop();
        Console.Write($"Data read: {w.ElapsedMilliseconds}\n");

        return data;
    }

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

    public Task UpdateCache()
    {
        if (_trends != null) _trends.Clear();
        return Task.CompletedTask;
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

    public Task<DataSourceType> DataSourceType() => Task.FromResult(csvplot.DataSourceType.EnergyModel);
}
