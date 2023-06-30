// See https://aka.ms/new-console-template for more information
using System.ComponentModel;
using System.Runtime.Intrinsics.X86;
using System.Text.RegularExpressions;
using System;
using System.Xml.Linq;
using System.Reflection;
using System.Runtime.InteropServices;



IEnumerable<string> ReadAllLines(string filePath)
{
    using (var sr = new StreamReader(filePath))
    {
        while (!sr.EndOfStream)
        {
            var line = sr.ReadLine();
            if (line is null)
            {
                break;
            }
            yield return line;
        }
    }
}

const string ControllerStart = "BeginController";
const string InfinetCtlrStart = "InfinetCtlr";
const string ObjectStart = "Object";

string[] starts = new string[] { ControllerStart, InfinetCtlrStart, ObjectStart };
bool IsStart(string line)
{
    var kv = line.Split(':');
    if (kv.Length != 2)
    {
        return false;
    }
    var key = kv[0].Trim();
    return starts.Any(key.Equals);
}

const string ControllerEnd = "EndController";
const string InfinetCtlrEnd = "EndInfinetCtlr";
const string ObjectEnd = "EndObject";

string[] ends = new string[] { ControllerEnd, InfinetCtlrEnd, ObjectEnd };
bool IsEnd(string line)
{
    var tag = line.Trim();
    return ends.Any(s => tag.Equals(s));
}

const string ByteCodeStart = "ByteCode";

bool IsNotStartOfByteCode(string line) => !line.Contains(ByteCodeStart);

const string ByteCodeEnd = "EndByteCode";
bool IsNotEndOfByteCode(string line) => !line.Contains(ByteCodeEnd);

/// <summary> reads the value of a colon seperated key:value pair </summary>
string ParseValue(string line)
{
    // sample
    // @" Object : ReadCompressors"
    var kv = line.Split(':');
    if (kv.Length < 2)
    {
        return string.Empty;
    }
    return kv[1].Trim();
}

///
ValueTuple<string, string> ObjectScanner(ValueTuple<string, string> state, string line)
{
    var (filename, lastLine) = state;
    if (IsEnd(lastLine))
    {
        filename = System.IO.Path.GetDirectoryName(filename) ?? string.Empty;
    }
    if (IsStart(line))
    {
        filename = System.IO.Path.Join(filename, ParseValue(line));
    }
    return (filename, line);
};



/// <summary>LineScanner tags double empty lines.</summary>
ValueTuple<LineState, string> LineScanner(ValueTuple<LineState, string> state, string line) =>
    line switch
    {
        "" => state switch
        {
            (LineState.Empty0, _) => (LineState.Empty1, line),
            _ => (LineState.Empty0, line),
        },
        _ => (LineState.Data, line),
    };

IEnumerable<string> ReadByteCode(IEnumerable<string> lines) =>
    lines
    .SkipWhile(IsNotStartOfByteCode)
    .Skip(1)
    .TakeWhile(IsNotEndOfByteCode);


void Write(IEnumerable<string> lines, string dirPath)
{
    ValueTuple<string, string> state = (String.Empty, String.Empty);

    var objects = lines
        .Scan(ObjectScanner, state)
        .GroupBy(s => s.Item1)
        .Where(g => g.Key != String.Empty)
        .Select(g => (g.Key, g.Select(t => t.Item2)))
        ;

    foreach (var (filename, objectLines) in objects)
    {
        if (objectLines == null) { continue; }

        // remove the even empty lines (0,2,4, ... )
        // files are created with double new lines
        // this is a fix to remove the extra new lines
        var codeLines = ReadByteCode(objectLines)
                    .Scan(LineScanner, (LineState.Data, ""))
                    .Where(t => t.Item1 != LineState.Empty0)
                    .Select(t => t.Item2);

        if (!codeLines.Any()) { continue; }

        var peFile = Path.Combine(dirPath, filename + ".pe");
        var peDir = Path.GetDirectoryName(peFile) ?? ".";
        if (!Directory.Exists(peDir))
        {
            Directory.CreateDirectory(peDir);
        }
        using var sr = new StreamWriter(peFile);
        foreach (var item in codeLines)
        {
            sr.WriteLine(item);
        }
        sr.Flush();
    };
}

void WriteLines(string dirPath, IEnumerable<string> lines)
{
    Write(lines, dirPath);
}


void Usage()
{
    var an = Assembly.GetExecutingAssembly().GetName();
    Console.Write($"""
        DmpExtractPE is a tool for extracting programs from continuum dump files.

        Usage:
            {an.Name} version   
                print the version information

            {an.Name} <dump file> [destination]
                extract the programs from the dump file and write them out to individual files
                in the destination directory.

                dump file   - (required) the source continuum dump file.
                destination - default is the current directory.
         
        """);
}
void Version()
{
    var an = Assembly.GetExecutingAssembly().GetName();
    Console.Write($""" 
        {an.Name} version {an.Version}
        """);
}

if (args.Length < 1)
{
    Usage();
    return;
}

var src = args[0];

if (src.ToLower() == "version" || src.ToLower() == "-version")
{
    Version();
    return;
}

var dest = Environment.CurrentDirectory;

if (args.Length > 1)
{
    dest = args[1];
}

var lines = ReadAllLines(src);
WriteLines(dest, lines);

enum LineState
{
    Data,
    Empty0,
    Empty1,
}

public static partial class Enumerable
{


    /// <summary>
    /// An iterator that scans each item of an array and returns a new item.
    /// </summary>
    /// <typeparam name="TSource">The type of the source array.</typeparam>
    /// <typeparam name="TResult">The type of the return array.</typeparam>
    public static IEnumerable<TResult> Scan<TSource, TResult>(
        this IEnumerable<TSource> source, Func<TResult, TSource, TResult> scanner, TResult initialState)
    {
        if (source == null)
        {
            throw new ArgumentNullException("source");
        }

        if (scanner == null)
        {
            throw new ArgumentNullException("scanner");
        }

        TResult state = initialState;
        var selector = (TSource t) =>
        {
            state = scanner(state, t);
            return state;
        };

        return source.Select(selector);
    }
}