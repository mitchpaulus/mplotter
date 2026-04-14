using System;
using System.Collections.Generic;
using System.Linq;

namespace csvplot;

public class Trend
{
    private const int MaxDisplayedTags = 5;

    public string Name { get; }
    public string Unit { get; set; }
    public List<string> Tags { get; set; }

    public string DisplayName { get; }

    public bool HasDisplayMetadata => !string.IsNullOrWhiteSpace(Unit) || Tags.Count > 0;

    public string MetadataDisplaySuffix
    {
        get
        {
            List<string> metadataParts = new();

            if (!string.IsNullOrWhiteSpace(Unit))
            {
                metadataParts.Add(Unit);
            }

            List<string> normalizedTags = Tags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToList();

            metadataParts.AddRange(normalizedTags.Take(MaxDisplayedTags));

            int remainingTagCount = normalizedTags.Count - MaxDisplayedTags;
            if (remainingTagCount > 0)
            {
                metadataParts.Add($"{remainingTagCount} more..");
            }

            return metadataParts.Count == 0 ? "" : $" [{string.Join(", ", metadataParts)}]";
        }
    }

    public string DisplayLabel => $"{DisplayName}{MetadataDisplaySuffix}";

    public Trend(string name, string unit, string displayName, IEnumerable<string>? tags = null)
    {
        Name = name;
        Unit = unit ?? "";
        DisplayName = displayName;
        Tags = tags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>();
    }
}
