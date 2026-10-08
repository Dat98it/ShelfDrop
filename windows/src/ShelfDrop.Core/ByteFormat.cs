using System.Globalization;

namespace ShelfDrop.Core;

/// <summary>Formats a size the way Explorer does: 1024-based, labelled KB, MB, GB.</summary>
public static class ByteFormat
{
    private static readonly string[] Units = { "KB", "MB", "GB", "TB" };

    public static string Format(long bytes, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (bytes < 0) bytes = 0;
        if (bytes < 1024) return bytes == 1 ? "1 byte" : $"{bytes.ToString(culture)} bytes";

        double value = bytes / 1024.0;
        int unit = 0;
        // Roll over before rounding would print "1024 KB".
        while (unit < Units.Length - 1 && Math.Round(value, value < 100 ? 1 : 0, MidpointRounding.AwayFromZero) >= 1024)
        {
            value /= 1024;
            unit++;
        }

        // KB is whole numbers: nobody needs to know a file is 23.7 KB.
        string text = unit == 0 || value >= 100
            ? Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", culture)
            : value.ToString("0.#", culture);
        return $"{text} {Units[unit]}";
    }
}
