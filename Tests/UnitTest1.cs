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