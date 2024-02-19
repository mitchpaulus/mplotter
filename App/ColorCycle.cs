using System.Collections.Generic;
using System.Linq;
using ScottPlot;

namespace csvplot;

public class ColorCycle
{
    private static readonly List<string> InitialColorList = new()
    {
        "004987",
        // "54585a",
        "500000",
        "19913a",
        "fdc400",
    };


    public static List<Color> GetColors(int numColors)
    {
        if (numColors <= InitialColorList.Count)
        {
            return InitialColorList.Take(numColors).Select(Color.FromHex).ToList();
        }

        // Else just evenly space Hues in HSL space. Use 0.7 for Saturation and 0.5 for Lightness
        float sat = 0.7f;
        float light = 0.5f;

        float hueStep = 1.0f / numColors;

        List<Color> colors = new();

        for (int i = 0; i < numColors; i++)
        {
            colors.Add(Color.FromHSL((float)hueStep * i, sat, light));
        }

        return colors;
    }
}
