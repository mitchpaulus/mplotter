using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace csvplot;

public static class ConfigurationParser
{
    private static readonly JsonSerializerOptions SaveOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string GetDefaultConfigurationPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "mplotter", "config.json");
        }

        var home = Environment.GetEnvironmentVariable("HOME");
        return home is null
            ? Path.Combine("config.json")
            : Path.Combine(home, ".config", "mplotter", "config.json");
    }

    public static Configuration LoadConfiguration()
    {
        try
        {
            using FileStream stream = new(GetDefaultConfigurationPath(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return LoadConfiguration(stream);
        }
        catch
        {
            return new Configuration();
        }
    }

    public static Configuration LoadConfiguration(Stream stream)
    {
        Configuration cfg = new();

        try
        {
            using JsonDocument doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return cfg;

            JsonElement root = doc.RootElement;

            if (TryGetProperty(root, "version", out JsonElement versionElement)
                && versionElement.ValueKind == JsonValueKind.Number
                && versionElement.TryGetInt32(out int version))
            {
                cfg.Version = version;
            }

            if (TryGetProperty(root, "influx", out JsonElement influxElement) && influxElement.ValueKind == JsonValueKind.Object)
            {
                ParseInfluxConfiguration(cfg, influxElement);
            }
        }
        catch
        {
            return new Configuration();
        }

        return cfg;
    }

    public static void SaveConfiguration(Configuration configuration)
    {
        string configPath = GetDefaultConfigurationPath();
        string? directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream stream = new(configPath, FileMode.Create, FileAccess.Write, FileShare.None);
        SaveConfiguration(configuration, stream);
    }

    public static void SaveConfiguration(Configuration configuration, Stream stream)
    {
        JsonSerializer.Serialize(stream, configuration, SaveOptions);
    }

    private static void ParseInfluxConfiguration(Configuration cfg, JsonElement influxElement)
    {
        if (TryGetProperty(influxElement, "buckets", out JsonElement bucketsElement) && bucketsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty bucketProp in bucketsElement.EnumerateObject())
            {
                InfluxBucketConfig bucket = new();
                cfg.Influx.Buckets ??= new Dictionary<string, InfluxBucketConfig>(StringComparer.Ordinal);
                cfg.Influx.Buckets[bucketProp.Name] = bucket;

                if (TryGetProperty(bucketProp.Value, "containers", out JsonElement containersElement)
                    && containersElement.ValueKind == JsonValueKind.Object)
                {
                    bucket.Containers ??= new Dictionary<string, InfluxContainerConfig>(StringComparer.Ordinal);
                    foreach (JsonProperty containerProp in containersElement.EnumerateObject())
                    {
                        InfluxContainerConfig container = new();
                        bucket.Containers[containerProp.Name] = container;

                        if (TryGetProperty(containerProp.Value, "tags", out JsonElement tagsElement)
                            && tagsElement.ValueKind == JsonValueKind.Array)
                        {
                            container.Tags = tagsElement
                                .EnumerateArray()
                                .Where(tag => tag.ValueKind == JsonValueKind.String)
                                .Select(tag => tag.GetString())
                                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                                .Cast<string>()
                                .ToList();
                        }

                        if (TryGetProperty(containerProp.Value, "parent", out JsonElement parentElement)
                            && parentElement.ValueKind == JsonValueKind.String)
                        {
                            container.Parent = parentElement.GetString();
                        }
                    }
                }

                if (TryGetProperty(bucketProp.Value, "points", out JsonElement pointsElement)
                    && pointsElement.ValueKind == JsonValueKind.Object)
                {
                    bucket.Points ??= new Dictionary<string, PointConfig>(StringComparer.Ordinal);
                    foreach (JsonProperty pointProp in pointsElement.EnumerateObject())
                    {
                        bucket.Points[pointProp.Name] = ParsePointConfig(pointProp.Value);
                    }
                }
            }
        }
    }

    private static PointConfig ParsePointConfig(JsonElement pointElement)
    {
        PointConfig point = new();

        if (TryGetProperty(pointElement, "tags", out JsonElement tagsElement)
            && tagsElement.ValueKind == JsonValueKind.Array)
        {
            point.Tags = tagsElement
                .EnumerateArray()
                .Where(tag => tag.ValueKind == JsonValueKind.String)
                .Select(tag => tag.GetString())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Cast<string>()
                .ToList();
        }

        if (TryGetProperty(pointElement, "container", out JsonElement containerElement)
            && containerElement.ValueKind == JsonValueKind.String)
        {
            point.Container = containerElement.GetString();
        }

        if (TryGetProperty(pointElement, "unit", out JsonElement unitElement)
            && unitElement.ValueKind == JsonValueKind.String)
        {
            point.Unit = unitElement.GetString();
        }

        if (TryGetProperty(pointElement, "alias", out JsonElement aliasElement)
            && aliasElement.ValueKind == JsonValueKind.String)
        {
            point.Alias = aliasElement.GetString();
        }
        else if (TryGetProperty(pointElement, "displayName", out JsonElement displayNameElement)
                 && displayNameElement.ValueKind == JsonValueKind.String)
        {
            point.Alias = displayNameElement.GetString();
        }

        return point;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)) continue;
            value = property.Value;
            return true;
        }

        value = default;
        return false;
    }
}

public class Configuration
{
    public int Version { get; set; } = 1;
    public InfluxConfiguration Influx { get; set; } = new();

    public InfluxBucketConfig GetOrCreateInfluxBucket(string bucketName)
    {
        Influx.Buckets ??= new Dictionary<string, InfluxBucketConfig>(StringComparer.Ordinal);
        if (Influx.Buckets.TryGetValue(bucketName, out InfluxBucketConfig? bucket))
        {
            return bucket;
        }

        bucket = new InfluxBucketConfig();
        Influx.Buckets[bucketName] = bucket;
        return bucket;
    }

    public Dictionary<string, PointConfig> EnsureInfluxPoints(string bucketName)
    {
        InfluxBucketConfig bucket = GetOrCreateInfluxBucket(bucketName);
        bucket.Points ??= new Dictionary<string, PointConfig>(StringComparer.Ordinal);
        return bucket.Points;
    }

    public Dictionary<string, PointConfig>? GetInfluxPoints(string bucketName)
    {
        if (Influx.Buckets is null) return null;
        if (!Influx.Buckets.TryGetValue(bucketName, out InfluxBucketConfig? bucket)) return null;
        return bucket.Points;
    }

    public PointConfig GetOrCreateInfluxPoint(string bucketName, string pointName)
    {
        Dictionary<string, PointConfig> points = EnsureInfluxPoints(bucketName);
        if (points.TryGetValue(pointName, out PointConfig? pointConfig))
        {
            return pointConfig;
        }

        pointConfig = new PointConfig();
        points[pointName] = pointConfig;
        return pointConfig;
    }

    public void RemoveInfluxPointIfEmpty(string bucketName, string pointName)
    {
        if (Influx.Buckets is null) return;
        if (!Influx.Buckets.TryGetValue(bucketName, out InfluxBucketConfig? bucket)) return;
        if (bucket.Points is null) return;
        if (!bucket.Points.TryGetValue(pointName, out PointConfig? point)) return;
        if (!point.IsEmpty()) return;

        bucket.Points.Remove(pointName);
        if (bucket.Points.Count == 0)
        {
            bucket.Points = null;
        }

        if ((bucket.Points is null || bucket.Points.Count == 0)
            && (bucket.Containers is null || bucket.Containers.Count == 0))
        {
            Influx.Buckets.Remove(bucketName);
        }

        if (Influx.Buckets.Count == 0)
        {
            Influx.Buckets = null;
        }
    }
}

public class InfluxConfiguration
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, InfluxBucketConfig>? Buckets { get; set; }
}

public class InfluxBucketConfig
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, InfluxContainerConfig>? Containers { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, PointConfig>? Points { get; set; }
}

public class InfluxContainerConfig
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Tags { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Parent { get; set; }
}

public class PointConfig
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Tags { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Container { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Unit { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Alias { get; set; }

    public bool IsEmpty()
    {
        return (Tags is null || Tags.Count == 0)
               && string.IsNullOrWhiteSpace(Container)
               && string.IsNullOrWhiteSpace(Unit)
               && string.IsNullOrWhiteSpace(Alias);
    }
}
