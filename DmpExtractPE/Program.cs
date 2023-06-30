// See https://aka.ms/new-console-template for more information
using System.ComponentModel;
using System.Runtime.Intrinsics.X86;
using System.Text.RegularExpressions;
using System;
using System.Xml.Linq;


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
const string InfinetCtlrStart = "InfinetCtlr ";
const string ObjectStart = "Object";

string[] starts = new string[] { ControllerStart, InfinetCtlrStart, ObjectStart };
bool IsStart(string line) => starts.Any(s => line.Contains(s));

const string ControllerEnd = "EndController";
const string InfinetCtlrEnd = "EndInfinetCtlr";
const string ObjectEnd = "EndObject";

string[] ends = new string[] { ControllerEnd, InfinetCtlrEnd, ObjectEnd };
bool IsEnd(string line) => ends.Any(s => line.Contains(s));

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


ValueTuple<bool, string> LineScanner(ValueTuple<bool, string> state, string line) =>
    state switch
    {
        (false, _) => (line.Equals(""), line),
        (true, _) => (false, line)
    };

IEnumerable<string> ReadByteCode(IEnumerable<string> lines) =>
    lines.SkipWhile(IsNotStartOfByteCode).TakeWhile(IsNotEndOfByteCode);


void Write(IEnumerable<string> lines, string dirPath)
{
    ValueTuple<string, string> state = (String.Empty, String.Empty);

    var objects = lines
        .Select(line =>
        {
            state = ObjectScanner(state, line);
            return state;
        })
        .GroupBy(s => s.Item1)
        .Where(g => g.Key != String.Empty)
        .Select(g => (g.Key, g.Select(t => t.Item2)))
        .ToList();

    foreach (var (filename, objectLines) in objects)
    {
        if (objectLines == null) { continue; }

        var state1 = (true, "");
        var codeLines = ReadByteCode(objectLines)
                    .Select(line =>
                    {
                        state1 = LineScanner(state1, line);
                        return state1;
                    })
                    .Where(t => !t.Item1)
                    .Select(t => t.Item2)
                    .Skip(1);

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


var src = args[0];
var dest = args[1];

var lines = ReadAllLines(src);
WriteLines(dest, lines);
