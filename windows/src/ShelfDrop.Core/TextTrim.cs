using System.Globalization;
using System.Text;

namespace ShelfDrop.Core;

/// <summary>Cuts a long name short in the middle ("Holiday pho…rip.png"), so that both its start and its ending (the file type) stay readable.</summary>
public static class TextTrim
{
    private const string Ellipsis = "…";

    /// <summary>
    /// The text itself if it fits in <paramref name="maxWidth"/>; otherwise the longest version with its middle replaced by "…"
    /// that does fit (just "…" if even that is too wide).
    /// </summary>
    /// <param name="measure">How wide a piece of text is when drawn, in the same unit as <paramref name="maxWidth"/>.</param>
    public static string Middle(string text, Func<string, double> measure, double maxWidth)
    {
        if (text.Length == 0 || measure(text) <= maxWidth) return text;

        // Whole characters as people see them: an emoji or an accented letter is not split.
        var elements = new List<string>();
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext()) elements.Add((string)enumerator.Current);

        // The more is kept, the wider it gets, so the most that fits can be found by halving the range.
        string best = Ellipsis;
        int low = 1;
        int high = elements.Count - 1;
        while (low <= high)
        {
            int keep = (low + high) / 2;
            string candidate = Join(elements, keep);
            if (measure(candidate) <= maxWidth)
            {
                best = candidate;
                low = keep + 1;
            }
            else
            {
                high = keep - 1;
            }
        }
        return best;
    }

    /// <summary>Keeps <paramref name="keep"/> characters: half from each end (the start gets the odd one).</summary>
    private static string Join(List<string> elements, int keep)
    {
        int tail = keep / 2;
        int head = keep - tail;
        var builder = new StringBuilder();
        for (int i = 0; i < head; i++) builder.Append(elements[i]);
        builder.Append(Ellipsis);
        for (int i = elements.Count - tail; i < elements.Count; i++) builder.Append(elements[i]);
        return builder.ToString();
    }
}
