using System.Collections.Generic;
using System.IO;

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

    public static string GetUnit(this string input)
    {
        Stack<int> leftBracketStack = new();
        Stack<int> leftParenStack = new();
        Stack<int> leftCurlyStack = new();

        string unit = "";

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
                    unit = input.Substring(leftIndex + 1, index - leftIndex + 1);
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
                    unit = input.Substring(leftIndex + 1, index - leftIndex + 1);
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
                    unit = input.Substring(leftIndex + 1, index - leftIndex + 1);
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
}
