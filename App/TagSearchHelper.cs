using System;
using System.Collections.Generic;
using System.Linq;

namespace csvplot;

public static class TagSearchHelper
{
    private const int DefaultMaxSuggestions = 8;

    public static List<string> GetTokens(string? searchText)
    {
        return (searchText ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToList();
    }

    public static bool MatchesAllTokens(IEnumerable<string>? tags, string? searchText)
    {
        List<string> tokens = GetTokens(searchText);
        if (tokens.Count == 0) return true;
        if (tags is null) return false;

        List<string> normalizedTags = tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToList();

        return tokens.All(token => normalizedTags.Any(tag => string.Equals(tag, token, StringComparison.OrdinalIgnoreCase)));
    }

    public static List<string> GetSuggestions(string? searchText, IEnumerable<string> availableTags, int maxSuggestions = DefaultMaxSuggestions)
    {
        string currentToken = GetCurrentToken(searchText);
        if (string.IsNullOrWhiteSpace(currentToken))
        {
            return new List<string>();
        }

        HashSet<string> committedTokens = new(GetCommittedTokens(searchText), StringComparer.OrdinalIgnoreCase);

        return availableTags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(tag => !committedTokens.Contains(tag))
            .Where(tag => tag.Contains(currentToken, StringComparison.OrdinalIgnoreCase))
            .OrderBy(tag => !tag.StartsWith(currentToken, StringComparison.OrdinalIgnoreCase))
            .ThenBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .Take(maxSuggestions)
            .ToList();
    }

    public static string ApplySuggestion(string? searchText, string suggestion)
    {
        string currentText = searchText ?? "";
        if (string.IsNullOrWhiteSpace(currentText))
        {
            return $"{suggestion} ";
        }

        if (char.IsWhiteSpace(currentText[^1]))
        {
            return $"{currentText}{suggestion} ";
        }

        int lastSpaceIndex = currentText.LastIndexOf(' ');
        if (lastSpaceIndex < 0)
        {
            return $"{suggestion} ";
        }

        return $"{currentText[..(lastSpaceIndex + 1)]}{suggestion} ";
    }

    private static string GetCurrentToken(string? searchText)
    {
        string currentText = searchText ?? "";
        if (string.IsNullOrWhiteSpace(currentText)) return "";
        if (char.IsWhiteSpace(currentText[^1])) return "";

        int lastSpaceIndex = currentText.LastIndexOf(' ');
        return lastSpaceIndex < 0
            ? currentText.Trim()
            : currentText[(lastSpaceIndex + 1)..].Trim();
    }

    private static List<string> GetCommittedTokens(string? searchText)
    {
        List<string> tokens = GetTokens(searchText);
        string currentText = searchText ?? "";

        if (tokens.Count == 0) return tokens;
        if (!string.IsNullOrEmpty(currentText) && char.IsWhiteSpace(currentText[^1])) return tokens;

        tokens.RemoveAt(tokens.Count - 1);
        return tokens;
    }
}
