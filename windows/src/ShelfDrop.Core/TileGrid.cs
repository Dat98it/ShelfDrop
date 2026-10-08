namespace ShelfDrop.Core;

/// <summary>How tiles are arranged: as many columns as fit, growing a little to use up the room.</summary>
public static class TileGrid
{
    /// <summary>
    /// As many columns as fit with the minimum tile width, and tiles that grow (up to the maximum) to use up the room,
    /// so the grid fills the shelf at any width.
    /// </summary>
    public static (int Columns, double TileWidth) Layout(double width, double minTile, double maxTile, double spacing)
    {
        int columns = Math.Max(1, (int)Math.Floor((width + spacing) / (minTile + spacing)));
        double tile = Math.Min(maxTile, (width - (columns - 1) * spacing) / columns);
        // A single column in a room narrower than the minimum still gets what room there is.
        return (columns, Math.Max(1, tile));
    }
}
