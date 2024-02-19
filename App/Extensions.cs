using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;

namespace csvplot;

public static class Extensions
{
    public static IEnumerable<string?> SplitLines(this string input)
    {
        using StringReader sr = new StringReader(input);
        while (sr.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    public static string ToSqliteConnString(this string filepath) => $"Data Source={filepath}";

    /// <summary>
    /// This function return a string representing the best guess at the unit given a full trend name.
    /// The algorithm is simple - it looks for text within parenthesis, square brackets, or curly brackets,
    /// and if more than one, the last one is taken.
    /// </summary>
    /// <returns>string of unit, no surrounding bracket.</returns>
    public static string? GetUnit(this string input)
    {
        Stack<int> leftBracketStack = new();
        Stack<int> leftParenStack = new();
        Stack<int> leftCurlyStack = new();

        string? unit = null;

        int index = 0;
        while (index < input.Length)
        {
            if (input[index] == '(') leftParenStack.Push(index);
            else if (input[index] == '[') leftBracketStack.Push(index);
            else if (input[index] == '{') leftCurlyStack.Push(index);
            else if (input[index] == ')')
            {
                if (leftParenStack.Count == 0) continue;
                if (leftParenStack.Count == 1)
                {
                    var leftIndex = leftParenStack.Pop();
                    unit = input.Substring(leftIndex + 1, index - leftIndex - 1);
                }
                else
                {
                    // Consume and move on.
                    leftParenStack.Pop();
                }
            }
            else if (input[index] == ']')
            {
                if (leftBracketStack.Count == 0) continue;
                if (leftBracketStack.Count == 1)
                {
                    var leftIndex = leftBracketStack.Pop();
                    unit = input.Substring(leftIndex + 1, index - leftIndex - 1);
                }
                else
                {
                    // Consume and move on.
                    leftBracketStack.Pop();
                }
            }
            else if (input[index] == '}')
            {
                if (leftCurlyStack.Count == 0) continue;
                if (leftCurlyStack.Count == 1)
                {
                    var leftIndex = leftCurlyStack.Pop();
                    unit = input.Substring(leftIndex + 1, index - leftIndex - 1);
                }
                else
                {
                    // Consume and move on.
                    leftCurlyStack.Pop();
                }
            }

            index++;
        }

        return unit;
    }

    public static (double Min, double Max) SafeMinMax(this IEnumerable<double> data)
    {
         double min;
         double max;

         // Enumerate once to see if any, then enumerate again through rest to get min and max.
         using var enumerator = data.GetEnumerator();
         bool any = enumerator.MoveNext();

         if (!any) return (0, 1);

         while (true)
         {
             min = enumerator.Current;
             max = enumerator.Current;
             any = enumerator.MoveNext();
             if (!any) break;
         }

         if (Math.Abs(min - max) < 0.00000001) max = min + 1;
         return (min, max);
    }

    public static (double Min, double Max) SafeMinMax(this List<double> data)
    {
         double min;
         double max;
         if (data.Count > 0)
         {
             min = data.Min();
             max = data.Max();
             if (Math.Abs(min - max) < 0.00000001)
             {
                 max = min + 1;
             }
         }
         else
         {
             min = 0;
             max = 1;
         }

         return (min, max);
    }

    public static IEnumerable<(T, int)> WithIndex<T>(this IEnumerable<T> source)
    {
        return source.Select((item, index) => (item, index));
    }

    public static string EscapeUiText(this string input) => input.Replace("_", "__");

    // public static List<List<(DataSourceViewModel s, TrendItemViewModel t)>> GroupTrendsByUnit(this List<(DataSourceViewModel s, TrendItemViewModel t)> list, UnitReader reader, UnitConverterReader unitConverterReader)
    // {
    //
    //
    //
    //
    //
    // }
}
