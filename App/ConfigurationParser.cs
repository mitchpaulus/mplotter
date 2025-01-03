using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace csvplot;

public class ConfigurationParser
{
    public static Configuration LoadConfiguration(Stream stream)
    {
        JsonDocument doc;
        Configuration cfg = new Configuration();
        try
        {
            doc = JsonDocument.Parse(stream);
        }
        catch
        {
            return cfg;
        }

        if (doc.RootElement.ValueKind != JsonValueKind.Object) return cfg;

        // Look for "equips" property
        if (doc.RootElement.TryGetProperty("equips", out var equipsElement))
        {
            if (equipsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var equipElement in equipsElement.EnumerateArray())
                {
                    if (equipElement.ValueKind == JsonValueKind.Object)
                    {
                        string name = "";
                        string equipRef = "";

                        if (equipElement.TryGetProperty("name", out var nameElement))
                        {
                            name = nameElement.GetString() ?? "";
                        }

                        if (equipElement.TryGetProperty("equipRef", out var equipRefElement))
                        {
                            equipRef = equipRefElement.GetString() ?? "";
                        }

                        cfg.Equips.Add(new EquipConfig(name, equipRef));
                    }
                }
            }
        }

        if (doc.RootElement.TryGetProperty("points", out var pointsElement))
        {
            if (pointsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var pointElement in pointsElement.EnumerateArray())
                {
                    if (pointElement.ValueKind == JsonValueKind.Object)
                    {
                        string name = "";
                        HashSet<string> tags = new HashSet<string>();
                        string equipRef = "";
                        string unit = "";
                        string displayName = "";

                        if (pointElement.TryGetProperty("name", out var nameElement))
                        {
                            name = nameElement.GetString() ?? "";
                        }

                        if (pointElement.TryGetProperty("tags", out var tagsElement))
                        {
                            if (tagsElement.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var tagElement in tagsElement.EnumerateArray())
                                {
                                    tags.Add(tagElement.GetString() ?? "");
                                }
                            }
                        }

                        if (pointElement.TryGetProperty("equipRef", out var equipRefElement))
                        {
                            equipRef = equipRefElement.GetString() ?? "";
                        }

                        if (pointElement.TryGetProperty("unit", out var unitElement))
                        {
                            unit = unitElement.GetString() ?? "";
                        }

                        if (pointElement.TryGetProperty("displayName", out var displayNameElement))
                        {
                            displayName = displayNameElement.GetString() ?? "";
                        }

                        // Don't use empty display name
                        if (string.IsNullOrEmpty(displayName))
                        {
                            displayName = name;
                        }

                        cfg.Points.Add(new PointConfig(name, tags, equipRef, unit, displayName));
                    }
                }
            }
        }

        return cfg;
    }
}

public class Configuration
{
    public List<EquipConfig> Equips = new();
    public List<PointConfig> Points = new();
}


public class EquipConfig
{
    public string Name { get; set; }
    public string EquipRef { get; set; }

    public EquipConfig(string name, string equipRef)
    {
        Name = name;
        EquipRef = equipRef;
    }
}

public class PointConfig
{
    public string Name { get; set; }
    public HashSet<string> Tags { get; set; }
    public string EquipRef { get; set; }
    public string Unit { get; set; }

    public string DisplayName { get; set; }

    public PointConfig(string name, HashSet<string> tags, string equipRef, string unit, string displayName)
    {
        Name = name;
        Tags = tags;
        EquipRef = equipRef;
        Unit = unit;
        DisplayName = displayName;
    }
}
