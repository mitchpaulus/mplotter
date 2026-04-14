using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace csvplot;

public static class TagChoices
{
    public static IReadOnlyList<string> GetTags()
    {
        try
        {
            string path = GetTagsPath();
            if (!File.Exists(path))
            {
                return Array.Empty<string>();
            }

            return File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Where(line => !line.StartsWith("--", StringComparison.Ordinal))
                .Where(line => !line.StartsWith("#", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string GetTagsPath()
    {
        if (OperatingSystem.IsWindows())
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "mplotter", "tags.txt");
        }

        string? home = Environment.GetEnvironmentVariable("HOME");
        return home is null
            ? "tags.txt"
            : Path.Combine(home, ".config", "mplotter", "tags.txt");
    }
}
