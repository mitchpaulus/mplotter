using System.Text;
using csvplot;
using Microsoft.Data.Sqlite;

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

        using SqliteConnection conn = new SqliteConnection(connectionString);

        conn.Open();

        using SqliteCommand tableCmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type='table'", conn);
        using var tableReader = tableCmd.ExecuteReader();
        while (tableReader.Read())
        {
            Console.WriteLine(tableReader.GetString(0));
        }

        using SqliteCommand columnsCmd = new SqliteCommand("PRAGMA table_info(history)", conn);
        using var columnsReader = columnsCmd.ExecuteReader();
        int nameOrdinal = columnsReader.GetOrdinal("name");
        while (columnsReader.Read())
        {
            Console.WriteLine(columnsReader.GetString(nameOrdinal));
        }
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

    [TestCase(PlotMode.Ts, false, false)]
    [TestCase(PlotMode.Ts, true, true)]
    [TestCase(PlotMode.Xy, false, true)]
    [TestCase(PlotMode.Xy, true, true)]
    [TestCase(PlotMode.Histogram, false, true)]
    public void DateRangeChangeRequiresPlotRebuildTests(PlotMode mode, bool anyDbSourcesSelected, bool expected)
    {
        bool result = MainWindow.DateRangeChangeRequiresPlotRebuild(mode, anyDbSourcesSelected);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void ConfigurationParserReadsExampleShape()
    {
        string json = """
                      {
                        "version": 1,
                        "influx": {
                          "buckets": {
                            "bucket one": {
                              "containers": {
                                "equipment name 1": {
                                  "tags": ["tag1", "tag2"],
                                  "parent": "equipment name 2"
                                }
                              },
                              "points": {
                                "Device 1/analog input 1": {
                                  "tags": ["tag1", "tag2"],
                                  "container": "equipment name 1",
                                  "unit": "°F",
                                  "alias": "Better name for this trend."
                                }
                              }
                            }
                          }
                        }
                      }
                      """;

        using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
        Configuration config = ConfigurationParser.LoadConfiguration(stream);

        Assert.That(config.Version, Is.EqualTo(1));
        Assert.That(config.Influx.Buckets, Is.Not.Null);
        Assert.That(config.Influx.Buckets!, Contains.Key("bucket one"));
        Assert.That(config.Influx.Buckets!["bucket one"].Points, Is.Not.Null);
        Assert.That(config.Influx.Buckets!["bucket one"].Points!, Contains.Key("Device 1/analog input 1"));
        Assert.That(config.Influx.Buckets!["bucket one"].Points!["Device 1/analog input 1"].Unit, Is.EqualTo("°F"));
        Assert.That(config.Influx.Buckets!["bucket one"].Points!["Device 1/analog input 1"].Alias, Is.EqualTo("Better name for this trend."));
    }

    [Test]
    public void ConfigurationParserReadsPascalCaseWrittenConfig()
    {
        string json = """
                      {
                        "Version": 1,
                        "Influx": {
                          "Buckets": {
                            "Bucket One": {
                              "Points": {
                                "Trend A": {
                                  "Unit": "GPM",
                                  "Alias": "Flow A"
                                }
                              }
                            }
                          }
                        }
                      }
                      """;

        using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
        Configuration config = ConfigurationParser.LoadConfiguration(stream);

        Assert.That(config.Influx.Buckets, Is.Not.Null);
        Assert.That(config.Influx.Buckets!, Contains.Key("Bucket One"));
        Assert.That(config.Influx.Buckets!["Bucket One"].Points, Is.Not.Null);
        Assert.That(config.Influx.Buckets!["Bucket One"].Points!, Contains.Key("Trend A"));
        Assert.That(config.Influx.Buckets!["Bucket One"].Points!["Trend A"].Unit, Is.EqualTo("GPM"));
        Assert.That(config.Influx.Buckets!["Bucket One"].Points!["Trend A"].Alias, Is.EqualTo("Flow A"));
    }

    [Test]
    public void ConfigurationParserWritesCamelCase()
    {
        Configuration configuration = new();
        configuration.GetOrCreateInfluxPoint("bucket one", "Trend A").Unit = "GPM";
        configuration.GetOrCreateInfluxPoint("bucket one", "Trend A").Tags = new List<string> { "tag1" };

        using MemoryStream stream = new();
        ConfigurationParser.SaveConfiguration(configuration, stream);
        string output = Encoding.UTF8.GetString(stream.ToArray());

        Assert.That(output, Does.Contain("\"version\""));
        Assert.That(output, Does.Contain("\"influx\""));
        Assert.That(output, Does.Contain("\"buckets\""));
        Assert.That(output, Does.Contain("\"bucket one\""));
        Assert.That(output, Does.Contain("\"points\""));
        Assert.That(output, Does.Contain("\"unit\""));
        Assert.That(output, Does.Contain("\"tags\""));
        Assert.That(output, Does.Not.Contain("\"Version\""));
        Assert.That(output, Does.Not.Contain("\"Influx\""));
        Assert.That(output, Does.Not.Contain("\"Points\""));
        Assert.That(output, Does.Not.Contain("\"Unit\""));
    }

    [Test]
    public void TrendDisplayLabelIncludesUnitAndTags()
    {
        Trend trend = new("Trend A", "GPM", "Trend A", new[] { "tag2", "tag1" });

        Assert.That(trend.DisplayLabel, Is.EqualTo("Trend A [GPM, tag1, tag2]"));
    }

    [Test]
    public void TrendDisplayLabelTruncatesAfterFiveTags()
    {
        Trend trend = new("Trend A", "GPM", "Trend A", new[] { "tag1", "tag2", "tag3", "tag4", "tag5", "tag6", "tag7" });

        Assert.That(trend.DisplayLabel, Is.EqualTo("Trend A [GPM, tag1, tag2, tag3, tag4, tag5, 2 more..]"));
    }

    [Test]
    public void TrendDisplayLabelUsesAliasForDisplay()
    {
        Trend trend = new("raw.measurement.name", "GPM", "Chilled Water Flow", new[] { "primary" });

        Assert.That(trend.DisplayLabel, Is.EqualTo("Chilled Water Flow [GPM, primary]"));
    }

    [Test]
    public void TagSearchMatchesExactTokensWithAndSemantics()
    {
        Assert.That(TagSearchHelper.MatchesAllTokens(new[] { "Primary", "CHW", "Plant" }, "primary chw"), Is.True);
        Assert.That(TagSearchHelper.MatchesAllTokens(new[] { "Primary", "Plant" }, "primary chw"), Is.False);
        Assert.That(TagSearchHelper.MatchesAllTokens(new[] { "Primary", "CHW" }, "pri"), Is.False);
    }

    [Test]
    public void TagSearchSuggestionsPreferPrefixMatchesAndKeepPreviousTokens()
    {
        List<string> suggestions = TagSearchHelper.GetSuggestions(
            "chw pri",
            new[] { "Primary", "Secondary", "AirPrimary", "Plant", "CHW" });

        Assert.That(suggestions, Is.EqualTo(new[] { "Primary", "AirPrimary" }));
        Assert.That(TagSearchHelper.ApplySuggestion("chw pri", "Primary"), Is.EqualTo("chw Primary "));
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
