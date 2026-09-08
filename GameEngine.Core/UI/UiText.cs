using System.Globalization;
using SkiaSharp;

namespace GameEngine.Core.UI;

public static class UiText
{
    public static string FitLine(string text, float size, float width)
    {
        if (!float.IsFinite(size) || size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        using var font = new SKFont(SKTypeface.Default, size);
        return Fit(text.Replace("\r", "").Replace("\n", " "), font, width);
    }

    public static IReadOnlyList<string> Wrap(string text, float size, float width, int maxLines)
    {
        if (!float.IsFinite(size) || size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
        if (!float.IsFinite(width) || width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (maxLines <= 0) throw new ArgumentOutOfRangeException(nameof(maxLines));
        using var font = new SKFont(SKTypeface.Default, size);
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            int paragraphStart = lines.Count;
            string line = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length > 0 && font.MeasureText(line + " " + word) > width)
                {
                    lines.Add(line);
                    line = "";
                }
                var remaining = word;
                while (font.MeasureText(remaining) > width)
                {
                    int length = PrefixLength(remaining, font, width);
                    if (length == 0) { remaining = ""; lines.Add(Fit(word, font, width)); break; }
                    lines.Add(remaining[..length]);
                    remaining = remaining[length..];
                }
                if (remaining.Length > 0) line += (line.Length == 0 ? "" : " ") + remaining;
            }
            if (line.Length > 0 || lines.Count == paragraphStart) lines.Add(line);
        }
        if (lines.Count <= maxLines) return lines;
        return lines.Take(maxLines - 1).Append(Fit(lines[maxLines - 1] + "…", font, width)).ToArray();
    }

    private static string Fit(string text, SKFont font, float width)
    {
        if (width <= 0 || !float.IsFinite(width)) return "";
        if (font.MeasureText(text) <= width) return text;
        float ellipsis = font.MeasureText("…");
        if (ellipsis > width) return "";
        return text[..PrefixLength(text, font, width - ellipsis)] + "…";
    }

    private static int PrefixLength(string text, SKFont font, float width)
    {
        int length = 0;
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            int end = elements.ElementIndex + elements.GetTextElement().Length;
            if (font.MeasureText(text[..end]) > width) break;
            length = end;
        }
        return length;
    }
}
