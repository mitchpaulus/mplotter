using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace csvplot;

public class TrendConfigListener : MconfigBaseListener
{
    public Dictionary<string, TrendMatcher> absPathMatches = new();
    public Dictionary<string, TrendMatcher> typeMatches = new();

    private List<string> _fields = new();
    private int _index = 0;
    private readonly ParseTreeWalker _walker = new();

    public void Load()
    {
        // Look for ".mpconfig" files in %LOCALAPPDATA%/mplotter/config, recursively
        // If found, load the file and parse it
        try
        {
            IEnumerable<string> files = Directory.EnumerateFiles(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mplotter",
                    "config"), "*.mpconfig", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                LoadFile(file);
            }
        }
        catch
        {
            // Ignore
        }
    }

    private bool Consume()
    {
        _index++;
        return _index >= _fields.Count;
    }

    private void LoadFile(string file)
    {
        AntlrInputStream stream = new AntlrFileStream(file, Encoding.UTF8);

        var errorListener = new ErrorListener();

        MconfigLexer lex = new MconfigLexer(stream);
        lex.RemoveErrorListeners();
        lex.AddErrorListener(errorListener);
        CommonTokenStream commonTokenStream = new CommonTokenStream(lex);
        MconfigParser parser = new MconfigParser(commonTokenStream);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(errorListener);
        var parsedFile = parser.file();

        if (errorListener.Messages.Any()) return;

        _walker.Walk(this, parsedFile);
    }

    // Record: SourceSelector TrendSelector Map
    // SourceSelector: TypeSelector | PathSelector
    // TypeSelector: 'type' FileType
    // FileType: 'eso'
    // PathSelector: 'path' STRING
    // TrendSelector: RegexSelector | NameSelector
    // RegexSelector: 're' STRING
    // NameSelector: 'name' STRING
    // Map: UnitMap | Rename | UnitConvert | Replace
    // UnitMap : 'unit' STRING
    // Rename: 'rename' STRING
    // UnitConvert: 'convert' STRING STRING
    // Replace: 'replace' STRING STRING
    private void LoadFileOld(string file)
    {
        var lines = File.ReadAllLines(file);

        foreach (var line in lines)
        {
            var split = line.Split("\t");
            if (split.Length == 0) continue;

            if (split[0] == "type")
            {
                if (split.Length < 2) continue;

                if (split[1] == "eso")
                {
                    // ParseTrendSelector
                    if (split.Length < 3) continue;
                    if (split[2] == "re")
                    {
                        if (split.Length < 4) continue;
                        string regex = split[3];

                        // Parse Transform
                        if (split.Length < 5) continue;

                        if (split[4] == "convert")
                        {
                            if (split.Length < 7) continue;
                            string fromUnit = split[5];
                            string toUnit = split[6];
                            UnitConvertTransform trans = new UnitConvertTransform(fromUnit, toUnit);

                            if (typeMatches.TryGetValue("eso", out var matcher))
                            {
                                matcher.RegexTransforms.Add((new Regex(regex), trans));
                            }
                            else
                            {
                                var newMatcher = new TrendMatcher();
                                typeMatches["eso"] = newMatcher;
                                newMatcher.RegexTransforms.Add((new Regex(regex), trans));
                            }
                        }
                        else if (split[4] == "replace")
                        {
                             if (split.Length < 7) continue;
                             string findRegex = split[5];
                             string replaceRegex = split[6];
                             FindReplaceTransform trans = new FindReplaceTransform(findRegex, replaceRegex);

                             if (typeMatches.TryGetValue("eso", out var matcher))
                             {
                                 matcher.RegexTransforms.Add((new Regex(regex), trans));
                             }
                             else
                             {
                                 var newMatcher = new TrendMatcher();
                                 typeMatches["eso"] = newMatcher;
                                 newMatcher.RegexTransforms.Add((new Regex(regex), trans));
                             }
                        }
                    }
                }
            }
            else if (split[0] == "path")
            {

            }
        }
    }

    public override void EnterRecord(MconfigParser.RecordContext context)
    {
        var transform = context.transform();

        Transform t;
        if (transform is MconfigParser.UnitTransformContext unitTransformContext)
        {
            return;
        }
        else if (transform is MconfigParser.ConvertTransformContext convertTransformContext)
        {
            var from = convertTransformContext.STRING(0).GetText().ParseString();
            var to = convertTransformContext.STRING(1).GetText().ParseString();
            t = new UnitConvertTransform(from, to);
        }
        else if (transform is MconfigParser.RenameTransformContext renameTransformContext)
        {
            return;
        }
        else if (transform is MconfigParser.ReplaceTransformContext replaceTransformContext)
        {
            var findRegex = replaceTransformContext.STRING(0).GetText().ParseString();
            var replaceRegex = replaceTransformContext.STRING(1).GetText().ParseString();
            t = new FindReplaceTransform(findRegex, replaceRegex);
        }
        else
        {
            return;
        }

        var sourceSelector = context.sourceSelector();
        if (sourceSelector is MconfigParser.TypeSelectorInfoContext typeSelectorInfoContext)
        {
            var typeString = typeSelectorInfoContext.typeSelector().STRING().GetText().ParseString();

            if (!typeMatches.ContainsKey(typeString)) typeMatches[typeString] = new TrendMatcher();
            var matcher = typeMatches[typeString];

            var trendSelector = context.trendSelector();
            if (trendSelector is MconfigParser.RegexTrendSelectorContext regexTrendSelectorContext)
            {
                string regex = regexTrendSelectorContext.STRING().GetText().ParseString();
                matcher.RegexTransforms.Add((new Regex(regex), t));
            }
            else if (trendSelector is MconfigParser.NameTrendSelectorContext nameTrendSelectorContext)
            {
                string name = nameTrendSelectorContext.STRING().GetText().ParseString();
                matcher.NameTransforms.TryAdd(name, t);
            }
            else
            {
                return;
            }
        }
        else if (sourceSelector is MconfigParser.AbsPathSelectorInfoContext absPathSelectorInfoContext)
        {

        }
    }

}


public class TrendMatcher
{
    public Dictionary<string, Transform> NameTransforms = new();
    public List<(Regex, Transform)> RegexTransforms = new();
}

public interface ISourceMatcher
{
}

public interface TrendSelector
{

}

public abstract class Transform
{

}

public class UnitConvertTransform : Transform
{
    public readonly string From;
    public readonly string To;

    public UnitConvertTransform(string from, string to)
    {
        From = from;
        To = to;
    }
}

public class FindReplaceTransform : Transform
{
    public readonly string FindRegex;
    public readonly string ReplaceRegex;

    public FindReplaceTransform(string findRegex, string replaceRegex)
    {
        FindRegex = findRegex;
        ReplaceRegex = replaceRegex;
    }
}

public class ErrorListener : IAntlrErrorListener<IToken>, IAntlrErrorListener<int>
{
    public readonly List<string>  Messages = new();

    public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
    {
        Messages.Add(msg);
    }

    public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
    {
        Messages.Add(msg);
    }
}
