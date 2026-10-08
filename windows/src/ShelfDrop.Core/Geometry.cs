namespace ShelfDrop.Core;

/// <summary>A point on the virtual desktop in physical pixels (y grows downwards).</summary>
public readonly record struct PixelPoint(int X, int Y);

/// <summary>A rectangle on the virtual desktop in physical pixels.</summary>
public readonly record struct PixelRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
    public PixelPoint TopLeft => new(Left, Top);

    /// <summary>Half-open: the left and top edges are inside, the right and bottom edges are not.</summary>
    public bool Contains(PixelPoint point) =>
        point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;
}

/// <summary>A size in device-independent units (1/96 inch at 100% scaling). What the user picks as the shelf size.</summary>
public readonly record struct SizeDips(double Width, double Height);

/// <summary>One monitor: where it is, what part of it windows may use, and how big its pixels are.</summary>
/// <param name="Bounds">Full area of the monitor.</param>
/// <param name="WorkArea">The part left over once the taskbar is taken out.</param>
/// <param name="Scale">DPI scale: 1.0 at 100%, 1.5 at 150%.</param>
public sealed record ScreenInfo(PixelRect Bounds, PixelRect WorkArea, double Scale, bool IsPrimary);
