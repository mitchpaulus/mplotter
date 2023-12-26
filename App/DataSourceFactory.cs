namespace csvplot;

public class DataSourceFactory
{
    public static IDataSource SourceFromLocalPath(string localPath)
    {
        if (localPath.EndsWith(".sql"))
        {
           return new EnergyPlusSqliteDataSource(localPath);
        }
        else
        {
            return new SimpleDelimitedFile(localPath);
        }
    }
}