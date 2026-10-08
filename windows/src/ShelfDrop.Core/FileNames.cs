using System.Text;

namespace ShelfDrop.Core;

/// <summary>Turns text from outside (a web page's title, a name a program offers) into something Windows accepts as a file name.</summary>
public static class FileNames
{
    private const int MaxLength = 120;

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// A safe single file name: no folders, no characters Windows forbids, no reserved device names, no trailing dot or space.
    /// A name that comes out empty becomes <paramref name="fallback"/>.
    /// </summary>
    public static string Sanitize(string? name, string fallback = "File")
    {
        // A name can arrive with a folder in front ("..\..\x" or "a/b.txt"): keep only the last part.
        string leaf = (name ?? "").Replace('\\', '/');
        leaf = leaf[(leaf.LastIndexOf('/') + 1)..];

        var builder = new StringBuilder(leaf.Length);
        foreach (char c in leaf)
        {
            builder.Append(c < 32 || "<>:\"/\\|?*".Contains(c) ? '_' : c);
        }

        string cleaned = builder.ToString().Trim().TrimEnd('.', ' ');
        if (cleaned.Length > MaxLength) cleaned = TruncateKeepingExtension(cleaned);
        if (cleaned.Length == 0 || cleaned.All(c => c == '_' || c == '.')) return fallback;

        // "CON.txt" is as reserved as "CON".
        string stem = cleaned.Contains('.') ? cleaned[..cleaned.IndexOf('.')] : cleaned;
        return Reserved.Contains(stem.TrimEnd()) ? "_" + cleaned : cleaned;
    }

    /// <summary>Returns <paramref name="name"/>, or "name (2).ext", "name (3).ext"... if that name is already <paramref name="taken"/>.</summary>
    public static string MakeUnique(string name, Func<string, bool> taken)
    {
        if (!taken(name)) return name;
        string stem = System.IO.Path.GetFileNameWithoutExtension(name);
        string extension = System.IO.Path.GetExtension(name);
        for (int n = 2; ; n++)
        {
            string candidate = $"{stem} ({n}){extension}";
            if (!taken(candidate)) return candidate;
        }
    }

    private static string TruncateKeepingExtension(string name)
    {
        string extension = System.IO.Path.GetExtension(name);
        if (extension.Length is 0 or > 16) return name[..MaxLength];
        return name[..(MaxLength - extension.Length)] + extension;
    }
}
