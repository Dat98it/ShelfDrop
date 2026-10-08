namespace ShelfDrop.Core;

/// <summary>Works out where the shelf opens.</summary>
public static class ShelfPlacement
{
    /// <summary>Breathing room between the shelf and the edge of the usable screen, in DIPs.</summary>
    public const double Margin = 8;

    /// <summary>
    /// Opens the shelf where the user last put it, or centred on <paramref name="cursor"/> if they never moved it
    /// (or that spot is no longer on any screen). Always kept fully inside the usable screen area.
    /// </summary>
    /// <param name="preferred">The size the user chose. It is shrunk to fit a small screen, but never changed here.</param>
    /// <param name="remembered">Where the user last dropped the shelf (its top-left corner), if ever.</param>
    public static PixelRect Compute(
        SizeDips preferred, PixelPoint? remembered, PixelPoint cursor, IReadOnlyList<ScreenInfo> screens)
    {
        // The remembered spot only counts while some screen still contains it (a display may have been
        // unplugged since). Probe just inside the corner: the exact edge belongs to the neighbour.
        ScreenInfo? rememberedScreen = remembered is { } r
            ? ScreenContaining(new PixelPoint(r.X + 1, r.Y + 1), screens)
            : null;
        PixelPoint? anchor = rememberedScreen is null ? null : remembered;

        ScreenInfo? screen = rememberedScreen
            ?? ScreenContaining(cursor, screens)
            ?? screens.FirstOrDefault(s => s.IsPrimary)
            ?? screens.FirstOrDefault();

        double scale = screen?.Scale ?? 1.0;
        double width = preferred.Width * scale;
        double height = preferred.Height * scale;
        double margin = Margin * scale;

        if (screen is not null)
        {
            // Shrinking is only for display: the caller keeps the preferred size, so a small screen
            // does not overwrite the user's choice.
            width = Math.Min(width, screen.WorkArea.Width - 2 * margin);
            height = Math.Min(height, screen.WorkArea.Height - 2 * margin);
        }
        int w = Math.Max(1, (int)Math.Round(width));
        int h = Math.Max(1, (int)Math.Round(height));

        int x = anchor?.X ?? cursor.X - w / 2;
        int y = anchor?.Y ?? cursor.Y - h / 2;

        if (screen is not null)
        {
            PixelRect work = screen.WorkArea;
            int m = (int)Math.Round(margin);
            x = Math.Min(Math.Max(x, work.Left + m), work.Right - w - m);
            y = Math.Min(Math.Max(y, work.Top + m), work.Bottom - h - m);
        }
        return new PixelRect(x, y, w, h);
    }

    public static ScreenInfo? ScreenContaining(PixelPoint point, IReadOnlyList<ScreenInfo> screens) =>
        screens.FirstOrDefault(s => s.Bounds.Contains(point));
}
