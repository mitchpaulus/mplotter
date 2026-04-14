using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace csvplot;

public static class TrendTextBlockFactory
{
    public static TextBlock Create(Trend trend, object? tag, string? prefix = null)
    {
        TextBlock textBlock = new()
        {
            Tag = tag,
            TextWrapping = TextWrapping.Wrap
        };

        Apply(textBlock, trend, prefix);
        return textBlock;
    }

    public static void Apply(TextBlock textBlock, Trend trend, string? prefix = null)
    {
        InlineCollection inlines = new();
        inlines.Add($"{prefix ?? ""}{trend.DisplayName}");
        if (trend.HasDisplayMetadata)
        {
            inlines.Add(new Run(trend.MetadataDisplaySuffix)
            {
                Foreground = Brushes.DodgerBlue
            });
        }

        textBlock.Inlines = inlines;
    }
}
