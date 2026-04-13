using System;

namespace csvplot;

public class Trend
{
    public string Name { get; }
    public string Unit { get; set; }

    public string DisplayName { get; }

    public bool HasDisplayUnit => !string.IsNullOrWhiteSpace(Unit);

    public string UnitDisplaySuffix => HasDisplayUnit ? $" [{Unit}]" : "";

    public string DisplayLabel => $"{DisplayName}{UnitDisplaySuffix}";

    public Trend(string name, string unit, string displayName)
    {
        Name = name;
        Unit = unit ?? "";
        DisplayName = displayName;
    }
}
