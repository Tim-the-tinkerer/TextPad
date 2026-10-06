using System.Text;
using System.Text.RegularExpressions;
using TextPad.Models;

namespace TextPad.Services;

/// <summary>
/// Writes plain text as Markdown that shows the same words and line breaks.
/// A document whose syntax language is already Markdown is written by the export command as typed.
/// </summary>
public static class PlainTextMarkdown
{
    public static string ToMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        if (lines.Length > 0 && lines[^1].Length == 0)
            lines = lines[..^1];

        var paragraphs = new List<List<string>>();
        var current = new List<string>();
        foreach (var line in lines)
        {
            if (IsBlankLine(line))
            {
                if (current.Count > 0)
                {
                    paragraphs.Add(current);
                    current = new List<string>();
                }
            }
            else
            {
                current.Add(ConvertLine(line));
            }
        }

        if (current.Count > 0)
            paragraphs.Add(current);
        if (paragraphs.Count == 0)
            return "";

        var body = new StringBuilder();
        for (var paragraphIndex = 0; paragraphIndex < paragraphs.Count; paragraphIndex++)
        {
            if (paragraphIndex > 0)
                body.Append("\n\n");
            var paragraph = paragraphs[paragraphIndex];
            for (var lineIndex = 0; lineIndex < paragraph.Count; lineIndex++)
            {
                if (lineIndex > 0)
                    body.Append('\n');
                body.Append(paragraph[lineIndex]);
                if (lineIndex < paragraph.Count - 1)
                    body.Append("  ");
            }
        }

        body.Append('\n');
        return body.ToString();
    }

    /// <summary>
    /// The converter emits LF. Under Preserve, put the source document's line ending back
    /// before the save policy runs. LF and CRLF policies still replace it afterwards.
    /// </summary>
    public static string ApplySourceLineEndings(string markdown, string source)
    {
        return EditorDocument.DetectLineEndings(source) switch
        {
            LineEndingKind.CrLf => markdown.Replace("\n", "\r\n"),
            LineEndingKind.Cr => markdown.Replace("\n", "\r"),
            _ => markdown
        };
    }

    internal static string Escape(string text, bool atLineStart, bool inTable, bool escapePipes = false)
    {
        var prefix = "";
        if (atLineStart)
        {
            if (Regex.IsMatch(text, @"^\d+[.)]\s"))
                prefix = "\\";
            else if (text.Length > 0 && "#>+-=".Contains(text[0]))
                prefix = "\\";
        }

        var escaped = new StringBuilder();
        foreach (var character in text)
        {
            if (character is '\\' or '`' or '*' or '_' or '[' or ']' or '~' or '<' ||
                ((inTable || escapePipes) && character == '|'))
            {
                escaped.Append('\\');
            }

            escaped.Append(character);
        }

        return prefix + escaped;
    }

    private static bool IsBlankLine(string line)
    {
        foreach (var character in line)
        {
            if (character != ' ' && character != '\t')
                return false;
        }

        return true;
    }

    private static string ConvertLine(string line)
    {
        var index = 0;
        var indent = new StringBuilder();
        while (index < line.Length && (line[index] == ' ' || line[index] == '\t'))
        {
            indent.Append('\u00A0', line[index] == '\t' ? 4 : 1);
            index++;
        }

        var rest = TrimTrailingWhitespace(line[index..]);
        var escaped = Escape(rest, atLineStart: true, inTable: false, escapePipes: true);
        return indent + escaped.Replace("\t", new string('\u00A0', 4));
    }

    private static string TrimTrailingWhitespace(string text)
    {
        var end = text.Length;
        while (end > 0 && (text[end - 1] == ' ' || text[end - 1] == '\t'))
            end--;
        return text[..end];
    }
}
