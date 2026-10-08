using Xunit;

namespace ShelfDrop.Core.Tests;

public class TileGridTests
{
    private static (int Columns, double TileWidth) Layout(double width) => TileGrid.Layout(width, 92, 112, 8);

    [Fact]
    public void ThreeColumnsFitInTheDefaultShelf()
    {
        // The default panel is 360 wide with 14 of padding either side: 332 of room.
        (int columns, double tile) = Layout(332);

        Assert.Equal(3, columns);
        Assert.Equal(105.33, tile, 2);
    }

    [Theory]
    [InlineData(92, 1)]
    [InlineData(191, 1)]
    [InlineData(192, 2)]   // 2 x 92 + 8 of spacing
    [InlineData(291, 2)]
    [InlineData(292, 3)]
    [InlineData(392, 4)]
    public void AColumnIsAddedAsSoonAsTheMinimumWidthFits(double width, int expectedColumns)
    {
        Assert.Equal(expectedColumns, Layout(width).Columns);
    }

    [Fact]
    public void TilesNeverGrowPastTheMaximum()
    {
        (int columns, double tile) = Layout(150);   // one column, with 38 more than a tile can use

        Assert.Equal(1, columns);
        Assert.Equal(112, tile);
    }

    [Fact]
    public void TilesNeverShrinkBelowTheMinimumWhileAColumnFits()
    {
        for (double width = 92; width <= 1000; width += 1)
        {
            (int columns, double tile) = Layout(width);
            Assert.True(tile >= 92 - 1e-9, $"at {width}: {columns} columns of {tile}");
        }
    }

    [Fact]
    public void ColumnsAndSpacingNeverOverflowTheRoom()
    {
        for (double width = 92; width <= 1000; width += 1)
        {
            (int columns, double tile) = Layout(width);
            Assert.True(columns * tile + (columns - 1) * 8 <= width + 1e-9, $"at {width}");
        }
    }

    [Fact]
    public void ARoomNarrowerThanOneTileStillGetsAColumn()
    {
        (int columns, double tile) = Layout(50);

        Assert.Equal(1, columns);
        Assert.Equal(50, tile);
    }

    [Fact]
    public void NoRoomAtAllStillGivesAUsableTile()
    {
        Assert.Equal((1, 1.0), Layout(0));
    }
}
