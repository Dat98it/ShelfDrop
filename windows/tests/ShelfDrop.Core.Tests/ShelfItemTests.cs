using System.Globalization;
using Xunit;

namespace ShelfDrop.Core.Tests;

public class ShelfItemTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Fact]
    public void AFileShowsItsNameKindAndSize()
    {
        using var dir = new TempDir();
        string path = dir.File("Holiday.png", new string('x', 24 * 1024));

        ShelfItem item = ShelfItem.ForFile(path);

        Assert.Equal(ShelfItemKind.File, item.Kind);
        Assert.Equal("Holiday.png", item.Title);
        Assert.Equal("Image", item.KindLabel);
        Assert.True(item.IsImageFile);
        Assert.Equal(24 * 1024, item.ByteCount);
        Assert.Equal("Image · 24 KB", item.Detail(Invariant));
        Assert.Equal(path, item.Path);
    }

    [Fact]
    public void AFolderHasNoSizeAndIsNotAnImage()
    {
        using var dir = new TempDir();
        ShelfItem item = ShelfItem.ForFile(dir.Folder("photos.png"));  // a folder that merely looks like an image

        Assert.Equal("Folder", item.KindLabel);
        Assert.Null(item.ByteCount);
        Assert.False(item.IsImageFile);
        Assert.Equal("Folder", item.Detail(Invariant));
    }

    [Theory]
    [InlineData("a.pdf", "PDF")]
    [InlineData("a.MP4", "Video")]
    [InlineData("a.flac", "Audio")]
    [InlineData("a.zip", "Archive")]
    [InlineData("a.xlsx", "Sheet")]
    [InlineData("a.pptx", "Slides")]
    [InlineData("a.cs", "Code")]
    [InlineData("a.txt", "Text")]
    [InlineData("a.docx", "Document")]
    [InlineData("a.exe", "App")]
    [InlineData("a.msi", "Installer")]
    [InlineData("a.xyz", "XYZ")]
    [InlineData("README", "File")]
    public void KindLabelComesFromTheExtension(string name, string expected)
    {
        Assert.Equal(expected, FileFacts.KindLabel(System.IO.Path.GetExtension(name), isDirectory: false));
    }

    [Fact]
    public void AFileThatIsNotThereStillGetsATileButIsMissing()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ShelfDrop-no-such-" + Guid.NewGuid() + ".pdf");

        ShelfItem item = ShelfItem.ForFile(path);

        Assert.Equal("PDF", item.KindLabel);
        Assert.Null(item.ByteCount);
        Assert.True(item.IsMissing);
        Assert.False(item.IsShareable);
    }

    [Fact]
    public void AFileBecomesMissingWhenItIsDeletedAfterwards()
    {
        using var dir = new TempDir();
        string path = dir.File("a.txt");
        ShelfItem item = ShelfItem.ForFile(path);
        Assert.False(item.IsMissing);

        File.Delete(path);

        Assert.True(item.IsMissing);
    }

    [Fact]
    public void LinksAndTextAreNeverMissing()
    {
        Assert.False(ShelfItem.ForLink(new Uri("https://example.com")).IsMissing);
        Assert.False(ShelfItem.ForText("hi").IsMissing);
        Assert.True(ShelfItem.ForText("hi").IsShareable);
    }

    [Fact]
    public void ALinkIsTitledByItsHost()
    {
        ShelfItem item = ShelfItem.ForLink(new Uri("https://www.example.com/a/b?c=1"));

        Assert.Equal(ShelfItemKind.Link, item.Kind);
        Assert.Equal("www.example.com", item.Title);
        Assert.Equal("Link", item.KindLabel);
        Assert.Equal("Link", item.Detail(Invariant));
        Assert.Null(item.ByteCount);
    }

    [Fact]
    public void ALinkWithoutAHostFallsBackToItsAddress()
    {
        ShelfItem item = ShelfItem.ForLink(new Uri("mailto:someone@example.com"));
        Assert.False(string.IsNullOrEmpty(item.Title));
    }

    [Fact]
    public void TextIsTitledByItsFirstLineCutAtFortyCharacters()
    {
        Assert.Equal("Hello", ShelfItem.ForText("Hello\nsecond line").Title);
        Assert.Equal("Hello", ShelfItem.ForText("\r\nHello\r\nWorld").Title);
        Assert.Equal(new string('a', 40), ShelfItem.ForText(new string('a', 100)).Title);
        Assert.Equal("Text", ShelfItem.ForText("x").KindLabel);
    }

    [Fact]
    public void CuttingTextNeverSplitsAnEmoji()
    {
        string text = string.Concat(Enumerable.Repeat("😀", 50));

        string title = ShelfItem.ForText(text).Title;

        Assert.Equal(string.Concat(Enumerable.Repeat("😀", 40)), title);
    }

    [Fact]
    public void TextWithOnlyLineBreaksIsKeptAsIs()
    {
        Assert.Equal("\n\n", ShelfItem.ForText("\n\n").Title);
    }

    [Fact]
    public void EveryItemHasItsOwnId()
    {
        Assert.NotEqual(ShelfItem.ForText("a").Id, ShelfItem.ForText("a").Id);
    }

    [Fact]
    public void SummaryDescribesTheWholeShelf()
    {
        using var dir = new TempDir();
        var items = new List<ShelfItem>();

        Assert.Equal("Empty", ShelfItem.Summary(items, Invariant));

        items.Add(ShelfItem.ForText("a"));
        Assert.Equal("1 item", ShelfItem.Summary(items, Invariant));

        items.Add(ShelfItem.ForFile(dir.File("a.bin", new string('x', 1024 * 1024))));
        items.Add(ShelfItem.ForFile(dir.File("b.bin", new string('x', 1024 * 1024))));
        Assert.Equal("3 items · 2 MB", ShelfItem.Summary(items, Invariant));
    }
}
