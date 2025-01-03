namespace csvplot;

public class Trend
{
    public string Name { get; }
    public string Unit { get; }

    public string DisplayName { get; }

    public Trend(string name, string unit, string displayName)
    {
        Name = name;
        Unit = unit;
        DisplayName = displayName;
    }

    public string? GetUnit()
    {
        if (string.IsNullOrWhiteSpace(Unit)) return Name.GetUnit();
        return Unit;
    }
}
