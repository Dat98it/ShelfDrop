using Xunit;

namespace ShelfDrop.Core.Tests;

public class TextTrimTests
{
    /// <summary>Every character is 10 wide, so a width of 100 holds ten characters.</summary>
    private static double Ten(string text) => text.Length * 10.0;

    [Fact]
    public void TextThatFitsIsLeftAlone()
    {
        Assert.Equal("report.pdf", TextTrim.Middle("report.pdf", Ten, 100));
        Assert.Equal("report.pdf", TextTrim.Middle("report.pdf", Ten, 1000));
    }

    [Fact]
    public void LongTextLosesItsMiddleAndKeepsBothEnds()
    {
        string result = TextTrim.Middle("Holiday photos from the trip.png", Ten, 150);

        Assert.StartsWith("Holiday", result);
        Assert.EndsWith(".png", result);   // the file type survives
        Assert.Equal(1, result.Count(c => c == '…'));
        Assert.True(result.Length * 10 <= 150);
    }

    [Fact]
    public void ItKeepsAsMuchAsFits()
    {
        const string text = "abcdefghijklmnopqrstuvwxyz";

        string result = TextTrim.Middle(text, Ten, 150);   // fifteen characters, one of them the ellipsis

        Assert.Equal(15, result.Length);
        Assert.Equal("abcdefg…tuvwxyz", result);
    }

    [Fact]
    public void ItNeverReturnsSomethingWiderThanTheRoom()
    {
        for (double room = 0; room <= 300; room += 7)
        {
            string result = TextTrim.Middle("A rather long file name.jpeg", Ten, room);
            Assert.True(result.Length * 10 <= room || result == "…", $"{room}: '{result}'");
        }
    }

    [Fact]
    public void NoRoomAtAllGivesJustTheEllipsis()
    {
        Assert.Equal("…", TextTrim.Middle("anything at all", Ten, 5));
        Assert.Equal("…", TextTrim.Middle("anything at all", Ten, 0));
    }

    [Fact]
    public void EmptyTextStaysEmpty()
    {
        Assert.Equal("", TextTrim.Middle("", Ten, 0));
    }

    [Fact]
    public void ACharacterMadeOfSeveralPartsIsNeverSplit()
    {
        string text = string.Concat(Enumerable.Repeat("😀", 20)) + ".png";

        string result = TextTrim.Middle(text, s => new System.Globalization.StringInfo(s).LengthInTextElements * 10.0, 100);

        // Both ends are whole emoji, not half of one.
        Assert.True(char.IsHighSurrogate(result[0]) && char.IsLowSurrogate(result[1]));
        Assert.EndsWith(".png", result);
        Assert.Equal(1, result.Count(c => c == '…'));
    }

    [Fact]
    public void WideLettersTakeMoreRoomThanNarrowOnes()
    {
        // 'W' is twice as wide as 'i': the same room holds fewer of them.
        double Proportional(string s) => s.Sum(c => c == 'W' ? 20.0 : c == '…' ? 10.0 : 10.0);

        string wide = TextTrim.Middle(new string('W', 30), Proportional, 200);
        string narrow = TextTrim.Middle(new string('i', 30), Proportional, 200);

        Assert.True(wide.Length < narrow.Length);
        Assert.True(Proportional(wide) <= 200);
    }
}
