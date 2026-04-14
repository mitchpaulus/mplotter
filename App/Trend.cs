using System;
using System.Collections.Generic;
using System.Linq;

namespace csvplot;

public class Trend
{
    public string Name { get; }
    public string Unit { get; set; }
    public List<string> Tags { get; set; }

    public string DisplayName { get; }

    public bool HasDisplayUnit => !string.IsNullOrWhiteSpace(Unit);

    public string UnitDisplaySuffix => HasDisplayUnit ? $" [{Unit}]" : "";

    public string DisplayLabel => $"{DisplayName}{UnitDisplaySuffix}";

    public Trend(string name, string unit, string displayName, IEnumerable<string>? tags = null)
    {
        Name = name;
        Unit = unit ?? "";
        DisplayName = displayName;
        Tags = tags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>();
    }
}
