using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace csvplot;
public class Unit : IEquatable<Unit>
{
    public UnitType Type { get; }
    public readonly double Factor;
    public readonly List<string> Names;
    public readonly int Id;

    public static readonly Dictionary<string, Unit> Map = new();
    private static int _idInc = 0;

    public Unit(List<string> names, UnitType type, double factor)
    {
        Type = type;
        Factor = factor;
        Names = names;

        Id = _idInc;
        _idInc++;

        foreach (var name in Names)
        {
            Map[name] = this;
        }
    }

    public bool Equals(Unit? other)
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
        return Equals((Unit) obj);
    }

    public override int GetHashCode() => Id;

    public static bool operator ==(Unit? left, Unit? right) => Equals(left, right);

    public static bool operator !=(Unit? left, Unit? right) => !Equals(left, right);
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
    private Dictionary<UnitType, Dictionary<string, Unit>> _unitDict = new();

    public void Read()
    {
        _unitDict.Clear();
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (localAppData == "") return;
                Stream s = File.OpenRead(Path.Combine(localAppData, "mplotter", "units.txt"));
                _unitDict = GetUnits(s);
            }
            else
            {
                var home = Environment.GetEnvironmentVariable("HOME");
                if (home is null) return;
                Stream s = File.OpenRead($"{home}/.config/mplotter/units.txt");
                _unitDict = GetUnits(s);
            }
        }
        catch
        {
            // Ignored
        }
    }

    public bool TryGetUnit(string unitStr, [NotNullWhen(true)] out Unit? unit)
    {
        unit = null;
        foreach ((var key, Dictionary<string, Unit>? value) in _unitDict)
        {
            if (!value.TryGetValue(unitStr, out Unit? foundUnit)) continue;

            unit = foundUnit;
            return true;
        }

        return false;
    }


    public Dictionary<UnitType, Dictionary<string, Unit>> GetUnits(Stream stream)
    {
       Dictionary<UnitType, Dictionary<string, Unit>> output = new();

       using StreamReader r = new StreamReader(stream, Encoding.UTF8);

       // Lines are semi-colon separated fields
       // unit name; unit type; factor
       // -- Comments begin with '--'
       while (r.ReadLine() is { } line)
       {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("--")) continue;

            var fields = trimmed.Split(";").Select(s => s.Trim()).ToList();

            if (fields.Count != 3) continue;

            var names = fields[0];
            var typeStr = fields[1];
            var factorStr = fields[2];

            if (!UnitType.Map.TryGetValue(typeStr, out UnitType? unitType)) continue;
            if (!double.TryParse(factorStr, out double factor)) continue;

            List<string> nameList = names.Split(",").Select(s => s.Trim()).ToList();
            Unit unit = new Unit(names.Split(",").Select(s => s.Trim()).ToList(), unitType, factor);

            if (output.TryGetValue(unitType, out var dict))
            {
                foreach (var name in nameList) dict[name] = unit;
            }
            else
            {
                output.Add(unitType, new Dictionary<string, Unit>());
                foreach (var name in nameList) output[unitType][name] = unit;
            }
       }

       return output;
    }

    public Dictionary<UnitType, Dictionary<string, Unit>> GetUnits()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (localAppData == "") return new();
                Stream s = File.OpenRead(Path.Combine(localAppData, "mplotter", "units.txt"));
                return GetUnits(s);
            }
            else
            {
                var home = Environment.GetEnvironmentVariable("HOME");
                if (home is null) return new();
                Stream s = File.OpenRead($"{home}/.config/mplotter/units.txt");
                return GetUnits(s);
            }
        }
        catch
        {
            return new();
        }
    }
}

public class UnitTypeGroup
{
    private readonly UnitType _unitType;

    public UnitTypeGroup(UnitType unitType)
    {
        _unitType = unitType;
    }

}

public class UnitConverterReader
{
    private readonly List<(Regex Regex, string unit)> _conversions = new();

    public void Read(Stream stream)
    {
        _conversions.Clear();
        using StreamReader r = new StreamReader(stream, Encoding.UTF8);

        while (r.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("--")) continue;

            var splitLine = trimmed.Split(";");

            if (splitLine.Length != 2) continue;

            Regex regex;
            try
            {
                regex = new Regex(splitLine[0].Trim());
            }
            catch
            {
                continue;
            }

            _conversions.Add((regex, splitLine[1].Trim()));
        }
    }

    public void Read()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (localAppData == "") return;
                Stream s = File.OpenRead(Path.Combine(localAppData, "mplotter", "conversions.txt"));
                Read(s);
            }
            else
            {
                var home = Environment.GetEnvironmentVariable("HOME");
                if (home is null) return;
                Stream s = File.OpenRead($"{home}/.config/mplotter/conversions.txt");
                Read(s);
            }
        }
        catch
        {
            // ignored
        }
    }

    public bool TryGetConversion(string inputName, UnitReader reader, [NotNullWhen(true)] out Unit? unit)
    {
        unit = null;
        foreach (var conversion in _conversions)
        {
            if (conversion.Regex.IsMatch(inputName) && reader.TryGetUnit(conversion.unit, out unit))
            {
                return true;
            }
        }

        return false;
    }
}