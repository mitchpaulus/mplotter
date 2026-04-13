using System;
using System.Collections.Generic;
using System.Linq;

namespace csvplot;

public static class UnitChoices
{
    private static readonly string[] DefaultUnits =
    [
        "°F",
        "GPM",
        "%",
        "Tons",
        "Count",
        "Amps"
    ];

    public static IReadOnlyList<string> GetUnits()
    {
        UnitReader reader = new();
        List<string> units = DefaultUnits
            .Concat(reader.GetUnits().SelectMany(pair => pair.Value.Keys))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(unit => unit, StringComparer.OrdinalIgnoreCase)
            .ToList();
        units.Insert(0, "");
        return units;
    }
}
