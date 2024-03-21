
using System.Data.SQLite;
using System.IO;

namespace csvplot;

public class DataSourceFactory
{
    public static IDataSource SourceFromLocalPath(string localPath)
    {
        if (localPath.EndsWith(".sql") || localPath.EndsWith(".db"))
        {
            string fullPath = Path.GetFullPath(localPath);

            string connectionString = $"Data Source={fullPath};";

            using SQLiteConnection conn = new SQLiteConnection(connectionString);
            conn.Open();

            var tables = conn.GetSchema("TABLES");
            if (tables.Rows.Count == 1 && (string)tables.Rows[0]["TABLE_NAME"] == "history")
            {
                return new Bac0DataSource(fullPath);
            }

            string sql = "SELECT KeyValue, Name, ReportingFrequency, Units FROM ReportDataDictionary";

            using SQLiteCommand cmd = new SQLiteCommand(sql, conn);

            using var reader = cmd.ExecuteReader();

           return new EnergyPlusSqliteDataSource(localPath);
        }

        if (localPath.EndsWith(".eso"))
        {
            return new EnergyPlusEsoDataSource(localPath);
        }

        return new SimpleDelimitedFile(localPath);
    }
}
