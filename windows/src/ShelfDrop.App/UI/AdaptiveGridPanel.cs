using System;
using System.Windows;
using System.Windows.Controls;
using ShelfDrop.Core;

namespace ShelfDrop.App.UI
{
    /// <summary>
    /// Lays tiles out in rows. As many columns as fit with the minimum tile width, and the tiles grow a little (up to the
    /// maximum) to use up the room, so the grid fills the shelf at any width.
    /// </summary>
    internal sealed class AdaptiveGridPanel : Panel
    {
        public double MinTileWidth { get; set; } = 92;
        public double MaxTileWidth { get; set; } = 112;
        public double Spacing { get; set; } = 8;

        protected override Size MeasureOverride(Size availableSize)
        {
            double width = double.IsInfinity(availableSize.Width) ? MinTileWidth * 3 + Spacing * 2 : availableSize.Width;
            (int columns, double tileWidth) = TileGrid.Layout(width, MinTileWidth, MaxTileWidth, Spacing);

            double y = 0;
            double rowHeight = 0;
            int column = 0;
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(tileWidth, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
                if (++column == columns)
                {
                    y += rowHeight + Spacing;
                    rowHeight = 0;
                    column = 0;
                }
            }
            if (column > 0) y += rowHeight;
            else if (y > 0) y -= Spacing;
            return new Size(width, y);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            (int columns, double tileWidth) = TileGrid.Layout(finalSize.Width, MinTileWidth, MaxTileWidth, Spacing);

            // Rows are as tall as their tallest tile, so measure the rows first.
            var children = new UIElement[InternalChildren.Count];
            InternalChildren.CopyTo(children, 0);

            double y = 0;
            for (int start = 0; start < children.Length; start += columns)
            {
                int end = Math.Min(start + columns, children.Length);
                double rowHeight = 0;
                for (int i = start; i < end; i++) rowHeight = Math.Max(rowHeight, children[i].DesiredSize.Height);
                for (int i = start; i < end; i++)
                {
                    double x = (i - start) * (tileWidth + Spacing);
                    children[i].Arrange(new Rect(x, y, tileWidth, rowHeight));
                }
                y += rowHeight + Spacing;
            }
            return finalSize;
        }
    }
}
