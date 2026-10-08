using Xunit;

namespace ShelfDrop.Core.Tests;

public class FileNamesTests
{
    [Theory]
    [InlineData("photo.jpg", "photo.jpg")]
    [InlineData("  spaced name .txt  ", "spaced name .txt")]
    [InlineData("a<b>c:d\"e|f?g*h.txt", "a_b_c_d_e_f_g_h.txt")]
    [InlineData("report.", "report")]
    [InlineData("tab\there.txt", "tab_here.txt")]
    public void ReplacesWhatWindowsForbids(string input, string expected)
    {
        Assert.Equal(expected, FileNames.Sanitize(input));
    }

    [Theory]
    [InlineData("..\\..\\Windows\\evil.dll", "evil.dll")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("folder/sub\\file.txt", "file.txt")]
    [InlineData("C:\\Users\\me\\x.txt", "x.txt")]
    public void KeepsOnlyTheLastPartSoNothingCanEscapeTheFolder(string input, string expected)
    {
        Assert.Equal(expected, FileNames.Sanitize(input));
    }

    [Theory]
    [InlineData("CON", "_CON")]
    [InlineData("con.txt", "_con.txt")]
    [InlineData("NUL", "_NUL")]
    [InlineData("com1.log", "_com1.log")]
    [InlineData("LPT9", "_LPT9")]
    [InlineData("console.txt", "console.txt")]
    public void ReservedDeviceNamesAreDefused(string input, string expected)
    {
        Assert.Equal(expected, FileNames.Sanitize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData("???")]
    [InlineData("\\")]
    public void NothingUsableBecomesTheFallback(string input)
    {
        Assert.Equal("File", FileNames.Sanitize(input));
        Assert.Equal("Link", FileNames.Sanitize(input, "Link"));
    }

    [Fact]
    public void NullBecomesTheFallback()
    {
        Assert.Equal("File", FileNames.Sanitize(null));
    }

    [Fact]
    public void ALongNameIsShortenedButKeepsItsExtension()
    {
        string result = FileNames.Sanitize(new string('a', 300) + ".pdf");

        Assert.Equal(120, result.Length);
        Assert.EndsWith(".pdf", result);
    }

    [Fact]
    public void MakeUniqueLeavesAFreeNameAlone()
    {
        Assert.Equal("a.txt", FileNames.MakeUnique("a.txt", _ => false));
    }

    [Fact]
    public void MakeUniqueNumbersTheClashes()
    {
        var taken = new HashSet<string> { "a.txt", "a (2).txt" };

        Assert.Equal("a (3).txt", FileNames.MakeUnique("a.txt", taken.Contains));
    }

    [Fact]
    public void MakeUniqueWorksWithoutAnExtension()
    {
        Assert.Equal("README (2)", FileNames.MakeUnique("README", n => n == "README"));
    }
}
