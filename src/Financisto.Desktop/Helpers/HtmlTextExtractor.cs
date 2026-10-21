using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Financisto.Desktop.Helpers;

/// <summary>
/// The text of a receipt saved as an HTML page (shops export their e-receipts that way). A receipt is usually a monospaced text in a
/// <c>&lt;pre&gt;</c> with one <c>&lt;span&gt;</c> per printed line: that text is taken as it is (line breaks and the alignment of the columns kept),
/// the tags and the entities (<c>&amp;oacute;</c>, <c>&amp;#39;</c>) are gone. A page without <c>&lt;pre&gt;</c> is read like a browser would show it:
/// <c>&lt;br&gt;</c>, paragraphs and table rows start a new line, the other whitespace is one space.
/// </summary>
public static class HtmlTextExtractor
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant;

    private static readonly Regex Ignored = new(@"<!--.*?-->|<head\b.*?</head>|<script\b.*?</script>|<style\b.*?</style>|<title\b.*?</title>", Options, Timeout);
    private static readonly Regex PreBlock = new(@"<pre\b[^>]*>(?<text>.*?)</pre>", Options, Timeout);
    private static readonly Regex LineBreak = new(@"<br\s*/?>|</(?:p|div|tr|li|ul|ol|table|h[1-6]|section|article|header|footer)\s*>", Options, Timeout);
    private static readonly Regex CellEnd = new(@"</t[dh]\s*>", Options, Timeout);
    private static readonly Regex Tag = new(@"<[^>]*>", Options, Timeout);
    private static readonly Regex Spaces = new(@"[ \t\r\n\f]+", Options, Timeout);

    public static string ExtractTextFromFile(string filePath) => ExtractText(File.ReadAllText(filePath));

    /// <summary>The text lines of the page; empty when the page has no text.</summary>
    public static string ExtractText(string html)
    {
        html = Ignored.Replace(html, string.Empty);

        var pre = PreBlock.Matches(html).Select(match => match.Groups["text"].Value).ToList();
        string text;
        if (pre.Count > 0)
        {
            // the line breaks are in the source: tags just go
            text = string.Join("\n", pre.Select(block => Tag.Replace(LineBreak.Replace(block, "\n"), string.Empty)));
        }
        else
        {
            // like a browser: the source's line breaks are spaces, the tags tell where a line ends
            var flat = Spaces.Replace(html, " ");
            flat = CellEnd.Replace(flat, " ");
            text = Tag.Replace(LineBreak.Replace(flat, "\n"), string.Empty);
        }

        text = WebUtility.HtmlDecode(text).Replace(' ', ' ').Replace("\r\n", "\n").Replace('\r', '\n');

        var lines = text.Split('\n').Select(line => line.TrimEnd()).ToList();
        if (pre.Count == 0)
        {
            lines = lines.Select(line => line.Trim()).ToList();
        }

        // no blank lines at the ends; a run of blank lines inside is one
        var result = new System.Collections.Generic.List<string>();
        foreach (var line in lines)
        {
            if (line.Length == 0 && (result.Count == 0 || result[^1].Length == 0))
            {
                continue;
            }

            result.Add(line);
        }

        while (result.Count > 0 && result[^1].Length == 0)
        {
            result.RemoveAt(result.Count - 1);
        }

        return string.Join(Environment.NewLine, result);
    }
}
