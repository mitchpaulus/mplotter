
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace csvplot;

public class DataSourceFactory
{
    public static IDataSource SourceFromLocalPath(string localPath, TrendConfigListener listener)
    {
        if (localPath.EndsWith(".sql") || localPath.EndsWith(".db"))
        {
            string fullPath = Path.GetFullPath(localPath);

            string connectionString = $"Data Source={fullPath};";

            using SqliteConnection conn = new SqliteConnection(connectionString);
            conn.Open();

            List<string> tableNames = new();
            using (SqliteCommand tableCmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table'", conn))
            using (var tableReader = tableCmd.ExecuteReader())
            {
                while (tableReader.Read())
                {
                    tableNames.Add(tableReader.GetString(0));
                }
            }
            if (tableNames.Count == 1 && tableNames[0] == "history")
            {
                return new Bac0DataSource(fullPath);
            }

            string sql = "SELECT KeyValue, Name, ReportingFrequency, Units FROM ReportDataDictionary";

            using SqliteCommand cmd = new SqliteCommand(sql, conn);

            using var reader = cmd.ExecuteReader();

           return new EnergyPlusSqliteDataSource(localPath);
        }

        if (localPath.EndsWith(".eso"))
        {
            if (listener.typeMatches.TryGetValue("eso", out var trendMatcher))
            {
                return new EnergyPlusEsoDataSource(localPath, trendMatcher);
            }
            else
            {
                return new EnergyPlusEsoDataSource(localPath, new TrendMatcher());
            }
        }

        var simpleDelimitedFile = new SimpleDelimitedFile(localPath);
        return simpleDelimitedFile;
    }
}
