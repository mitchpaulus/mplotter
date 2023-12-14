using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;

namespace csvplot;

public class EnergyPlusSqliteDataSource : IDataSource
{
    public EnergyPlusSqliteDataSource(string sourceFile)
    {
        Header = sourceFile;

        string connectionString = $"Data Source={sourceFile};";

        using SQLiteConnection conn = new SQLiteConnection(connectionString);
        conn.Open();

        string sql = "SELECT KeyValue, Name, ReportingFrequency, Units FROM ReportDataDictionary";

        using SQLiteCommand cmd = new SQLiteCommand(sql, conn);

        using var reader = cmd.ExecuteReader();

        Trends = new List<string>();

        while (reader.Read())
        {
            object keyValueObj = reader["KeyValue"];
            string keyValue;
            if (keyValueObj is DBNull)
            {
                keyValue = "";
            }
            else
            {
                keyValue = (string) keyValueObj;
            }

            var name = (string) reader["Name"];
            var units = (string) reader["Units"];
            var reportingFrequency = (string) reader["ReportingFrequency"];

            if (reportingFrequency == "Hourly")
            {
                Trends.Add($"{keyValue}: {name} [{units}]");
            }
        }
    }

    public List<string> Trends { get; }
    public double[] GetData(string trend)
    {
        // Split name into keyValue, name, units

        var keyValue = trend.Split(':')[0].Trim();
        var name = trend.Split(':')[1].Trim().Split('[')[0].Trim();
        var units = trend.Split(':')[1].Trim().Split('[')[1].Trim().TrimEnd(']');

        string connectionString = $"Data Source={Header};";

        using SQLiteConnection conn = new SQLiteConnection(connectionString);
        conn.Open();

        // First get the 'ReportDataDictionaryIndex' for the trend

        string sql = $"SELECT ReportDataDictionaryIndex FROM ReportDataDictionary WHERE KeyValue = '{keyValue}' and Name = '{name}' and Units = '{units}'";

        // SQLite returning 64 bit ints for whatever reason.
        object reportDataDictionaryIndex = -1;
        using (SQLiteCommand cmd = new SQLiteCommand(sql, conn))
        {
            using (var reader = cmd.ExecuteReader())
            {
                // Should only be one row
                var hasRow = reader.Read();

                // Return empty array if no row found
                if (!hasRow) return new double[0];

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

        sql = b.ToString();

        using (var cmd = new SQLiteCommand(sql, conn))
        {
            using (var reader = cmd.ExecuteReader())
            {
                var data = new List<double>(8760);

                while (reader.Read())
                {
                    var variableValue = (double) reader["VariableValue"];
                    data.Add(variableValue);
                }

                return data.ToArray();
            }
        }
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
}
