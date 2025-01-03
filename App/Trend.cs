namespace csvplot;

public class Trend
{
    public string Name { get; }
    public string Unit { get; }

    public Trend(string name, string unit)
    {
        Name = name;
        Unit = unit;
    }

    public string? GetUnit()
    {
        if (string.IsNullOrWhiteSpace(Unit)) return Name.GetUnit();
        return Unit;
    }
}
