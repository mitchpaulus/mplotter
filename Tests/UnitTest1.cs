using System.Data.SQLite;
using System.Text;
using csvplot;

namespace Tests;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        string? unit = "Trend name (kWh)".GetUnit();
        Assert.That(unit, Is.EqualTo("kWh"));
    }

    [Test]
    public void ReadUnits()
    {
        string fileContents = "btu/hr;power;0.29307107\ntr;power;3516.8528\n";
        MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(fileContents));
        UnitReader reader = new();
        var result = reader.GetUnits(stream);

        foreach ((var key, Dictionary<string, Unit>? value) in result)
        {
            Console.Write($"-- {key} --\n");
            foreach (var (name, unit) in value)
            {
                Console.Write($"  {name} = {unit.Factor}\n");
            }
        }
    }

    [Test]
    public void SelectTest()
    {
        List<TestClass> list1 = new List<TestClass>() { new TestClass("init", 1), new TestClass("init2", 2) };

        var list2 = list1.Where(@class => @class.Name == "init").Select(s => s).ToList();

        list1[0].Name = "new";

        Console.WriteLine(list2[0].Name);
    }

    [Test]
    public void TestSqliteSchema()
    {
        string filepath =
            @"C:\Users\mpaulus\Command Commissioning\Jobs - 23-TBNTX-001-ARX North Fort Worth Broadcast Center - ARX\2 Data\Claraxio\AFPT\2023-12-28_Trends\VAV_08_01.db";

        string connectionString = $"Data Source={filepath}";

        using SQLiteConnection conn = new SQLiteConnection(connectionString);

        conn.Open();

        var dataTable = conn.GetSchema("TABLES");

        var table2 = conn.GetSchema("COLUMNS", new[] { null, null, "history" });
    }

    [Test]
    public async Task TestNoaaStations()
    {
        var stations = await NoaaWeather.GetStations();
        var txStations = stations.Where(station => station.St == "TX");
    }


    [Test]
    public async Task TestReadNoaaData()
    {
        string usaf = "722590";
        string wban = "03927";
        int year = 2024;
        var stream = await NoaaWeatherDataSource.GetNoaaWeatherStreamUncompressedWeb(usaf, wban, year);

        List<NoaaWeatherRecord> records = NoaaWeatherDataSource.GetRecordsFromStream(stream);

        foreach (var record in records)
        {
            Console.Write(record.ToString());
            Console.Write('\n');
        }
    }

    [Test]
    public async Task TestNoaaCache()
    {
        string usaf = "722590";
        string wban = "03927";
        int year = 2023;

        var s = await NoaaWeatherDataSource.TryGetStreamFromCache(usaf, wban, year);
        s = await NoaaWeatherDataSource.TryGetStreamFromCache(usaf, wban, year);
    }


    [Test]
    public void CsvLineParseTests()
    {
        string line = "abc,def";

        var (output, _) = line.TryParseCsvLine();

        Assert.That(output.Count, Is.EqualTo(2));
        Assert.That(output[0], Is.EqualTo("abc"));
        Assert.That(output[1], Is.EqualTo("def"));

        line = "abc,\"def,\"";
        (output, _) = line.TryParseCsvLine();
        Assert.That(output.Count, Is.EqualTo(2));
        Assert.That(output[0], Is.EqualTo("abc"));
        Assert.That(output[1], Is.EqualTo("def,"));

        line = ",,";
        (output, _) = line.TryParseCsvLine();

        Assert.That(output.Count, Is.EqualTo(3));
        Assert.That(output[0], Is.EqualTo(""));
        Assert.That(output[1], Is.EqualTo(""));
        Assert.That(output[2], Is.EqualTo(""));

        line = "\"\"\"\"";
        (output, _) = line.TryParseCsvLine();

        Assert.That(output.Count, Is.EqualTo(1));
        Assert.That(output[0], Is.EqualTo("\""));

    }
}

public class TestClass
{
    public string Name { get; set; }
    public double Value { get; set; }

    public TestClass(string name, double value)
    {
        Name = name;
        Value = value;
    }
}
