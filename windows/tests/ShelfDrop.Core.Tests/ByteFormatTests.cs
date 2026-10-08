using System.Globalization;
using Xunit;

namespace ShelfDrop.Core.Tests;

public class ByteFormatTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(1, "1 byte")]
    [InlineData(2, "2 bytes")]
    [InlineData(1023, "1023 bytes")]
    [InlineData(1024, "1 KB")]
    [InlineData(2560, "3 KB")]
    [InlineData(24 * 1024, "24 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(1258291, "1.2 MB")]
    [InlineData(10 * 1048576 + 524288, "10.5 MB")]
    [InlineData(150L * 1048576, "150 MB")]
    [InlineData(3L * 1073741824, "3 GB")]
    public void FormatsLikeExplorer(long bytes, string expected)
    {
        Assert.Equal(expected, ByteFormat.Format(bytes, Invariant));
    }

    [Fact]
    public void RollsOverInsteadOfPrintingAThousandAndTwentyFourKilobytes()
    {
        // 1023.6 KB rounds to 1024: that is "1 MB", not "1024 KB".
        Assert.Equal("1 MB", ByteFormat.Format(1048166, Invariant));
    }

    [Fact]
    public void NegativeSizesAreTreatedAsZero()
    {
        Assert.Equal("0 bytes", ByteFormat.Format(-5, Invariant));
    }

    [Fact]
    public void UsesTheDecimalSeparatorOfTheCulture()
    {
        Assert.Equal("1,2 MB", ByteFormat.Format(1258291, new CultureInfo("vi-VN")));
    }
}
