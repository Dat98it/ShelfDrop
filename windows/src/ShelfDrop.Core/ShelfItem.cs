using System.Globalization;

namespace ShelfDrop.Core;

public enum ShelfItemKind
{
    File,
    Link,
    Text,
}

/// <summary>
/// One thing sitting on the shelf. Files are kept as references to the original location (nothing is copied),
/// except for payloads that only exist as data (images, virtual files), which are written to a temp folder
/// first so they behave like files everywhere.
/// </summary>
public sealed class ShelfItem
{
    private ShelfItem(ShelfItemKind kind, string title, string kindLabel, long? byteCount, bool isImageFile)
    {
        Kind = kind;
        Title = title;
        KindLabel = kindLabel;
        ByteCount = byteCount;
        IsImageFile = isImageFile;
    }

    public Guid Id { get; } = Guid.NewGuid();
    public ShelfItemKind Kind { get; }

    /// <summary>Full path, for <see cref="ShelfItemKind.File"/> only.</summary>
    public string? Path { get; private init; }

    /// <summary>The address, for <see cref="ShelfItemKind.Link"/> only.</summary>
    public Uri? Url { get; private init; }

    /// <summary>The content, for <see cref="ShelfItemKind.Text"/> only.</summary>
    public string? Text { get; private init; }

    public string Title { get; }

    /// <summary>Short human label for what this is: "Image", "PDF", "Folder", "Link", "Text"...</summary>
    public string KindLabel { get; }

    /// <summary>Size of a plain file; null for folders, links and text.</summary>
    public long? ByteCount { get; }

    /// <summary>True for image files, which get a photo-style thumbnail.</summary>
    public bool IsImageFile { get; }

    public static ShelfItem ForFile(string path)
    {
        FileFacts.Facts facts = FileFacts.Describe(path);
        // A drive root such as "C:\" has no file name, so show the path itself.
        string name = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
        return new ShelfItem(ShelfItemKind.File, name.Length > 0 ? name : path, facts.KindLabel, facts.ByteCount, facts.IsImage)
        {
            Path = path,
        };
    }

    public static ShelfItem ForLink(Uri url)
    {
        string host = url.IsAbsoluteUri && !string.IsNullOrEmpty(url.Host) ? url.Host : url.OriginalString;
        return new ShelfItem(ShelfItemKind.Link, host, "Link", null, false) { Url = url };
    }

    public static ShelfItem ForText(string text)
    {
        string firstLine = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? text;
        return new ShelfItem(ShelfItemKind.Text, Prefix(firstLine, 40), "Text", null, false) { Text = text };
    }

    /// <summary>Second line of a tile: the kind and, for files, the size ("Image · 24 KB").</summary>
    public string Detail(CultureInfo? culture = null) =>
        ByteCount is { } bytes ? $"{KindLabel} · {ByteFormat.Format(bytes, culture)}" : KindLabel;

    /// <summary>Header subtitle for a whole shelf: "7 items · 1.2 MB". Sizes only count plain files.</summary>
    public static string Summary(IReadOnlyCollection<ShelfItem> items, CultureInfo? culture = null)
    {
        if (items.Count == 0) return "Empty";
        string text = $"{items.Count} {(items.Count == 1 ? "item" : "items")}";
        long total = items.Sum(i => i.ByteCount ?? 0);
        return total > 0 ? $"{text} · {ByteFormat.Format(total, culture)}" : text;
    }

    /// <summary>True when a referenced file was moved or deleted after being added.</summary>
    public bool IsMissing => Kind == ShelfItemKind.File && Path is { } p && !System.IO.File.Exists(p) && !Directory.Exists(p);

    /// <summary>Whether the share sheet can take this item right now. A missing file cannot be shared.</summary>
    public bool IsShareable => !IsMissing;

    /// <summary>The first <paramref name="count"/> user-visible characters (an emoji is one, not two).</summary>
    private static string Prefix(string text, int count)
    {
        var elements = StringInfo.GetTextElementEnumerator(text);
        int taken = 0;
        int end = 0;
        while (taken < count && elements.MoveNext())
        {
            end = elements.ElementIndex + ((string)elements.Current).Length;
            taken++;
        }
        return text[..end];
    }
}
