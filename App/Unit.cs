using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace csvplot;
public class Unit
{
    public readonly double Factor;
    public readonly string Name;

    public static readonly Dictionary<string, Unit> Map = new();

    public Unit(string name, UnitType type, double factor)
    {
        Factor = factor;
        Name = name;

        Map[Name] = this;
    }
}

public class UnitType : IEquatable<UnitType>
{
    public int Id { get; }
    public string Name { get; }
    public static readonly Dictionary<string, UnitType> Map =new();
    public static readonly UnitType Power = new("power", 1);

    public UnitType(string name, int id)
    {
        Id = id;
        Name = name;
        Map[name] = this;
    }

    public override string ToString() => Name;

    public bool Equals(UnitType? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((UnitType) obj);
    }

    public override int GetHashCode()
    {
        return Id;
    }

    public static bool operator ==(UnitType? left, UnitType? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(UnitType? left, UnitType? right)
    {
        return !Equals(left, right);
    }
}


public class UnitReader
{
    public Dictionary<UnitType, Dictionary<string, Unit>> GetUnits(Stream stream)
    {
       Dictionary<UnitType, Dictionary<string, Unit>> output = new();

       using StreamReader r = new StreamReader(stream, Encoding.UTF8);

       while (r.ReadLine() is { } line)
       {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("--")) continue;

            var fields = trimmed.Split(";").Select(s => s.Trim()).ToList();

            if (fields.Count != 3) continue;

            var name = fields[0];
            var typeStr = fields[1];
            var factorStr = fields[2];

            if (!UnitType.Map.TryGetValue(typeStr, out UnitType? unitType)) continue;
            if (!double.TryParse(factorStr, out double factor)) continue;

            Unit unit = new Unit(name, unitType, factor);

            if (output.TryGetValue(unitType, out var dict))
            {
                dict[name] = unit;
            }
            else
            {
                output.Add(unitType, new Dictionary<string, Unit>());
                output[unitType][name] = unit;
            }
       }

       return output;
    }
}