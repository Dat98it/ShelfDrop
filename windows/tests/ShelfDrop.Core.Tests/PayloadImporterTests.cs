using System.Text;
using Xunit;

namespace ShelfDrop.Core.Tests;

public class PayloadImporterTests
{
    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Scratch = new TempDir();
            Temp = new TempStorage(System.IO.Path.Combine(Scratch.Path, "ShelfDrop"));
            Importer = new PayloadImporter(Temp, () => new DateTime(2026, 10, 8, 14, 3, 22));
        }

        public TempDir Scratch { get; }
        public TempStorage Temp { get; }
        public PayloadImporter Importer { get; }

        public void Dispose() => Scratch.Dispose();
    }

    [Fact]
    public void FilesAreKeptAsReferencesNotCopied()
    {
        using var f = new Fixture();
        string path = f.Scratch.File("Docs/report.pdf");

        IReadOnlyList<ShelfItem> items = f.Importer.Import(new FakePayload { FilePaths = new[] { path } });

        ShelfItem item = Assert.Single(items);
        Assert.Equal(path, item.Path);
        Assert.False(Directory.Exists(f.Temp.Root), "nothing should be copied into the temp folder");
    }

    [Fact]
    public void SeveralFilesBecomeSeveralItemsInOrder()
    {
        using var f = new Fixture();
        string a = f.Scratch.File("a.txt");
        string b = f.Scratch.File("b.txt");

        IReadOnlyList<ShelfItem> items = f.Importer.Import(new FakePayload { FilePaths = new[] { a, b } });

        Assert.Equal(new[] { "a.txt", "b.txt" }, items.Select(i => i.Title));
    }

    [Fact]
    public void FilesWinOverEverythingElseInTheSameDrop()
    {
        using var f = new Fixture();
        string path = f.Scratch.File("a.txt");
        var payload = new FakePayload
        {
            FilePaths = new[] { path },
            Links = new[] { new Uri("https://example.com") },
            Texts = new[] { "also text" },
            ImagePng = new byte[] { 1 },
        };

        IReadOnlyList<ShelfItem> items = f.Importer.Import(payload);

        Assert.Equal(ShelfItemKind.File, Assert.Single(items).Kind);
        Assert.Equal(0, payload.ImageReads);   // never fetched from the sender: it can be expensive
    }

    [Fact]
    public void BlankPathsAreIgnored()
    {
        using var f = new Fixture();
        IReadOnlyList<ShelfItem> items = f.Importer.Import(new FakePayload { FilePaths = new[] { "", "  " }, Texts = new[] { "hello" } });

        Assert.Equal(ShelfItemKind.Text, Assert.Single(items).Kind);
    }

    [Fact]
    public void VirtualFilesAreWrittenToTheTempFolder()
    {
        using var f = new Fixture();
        var payload = new FakePayload
        {
            VirtualFiles = new[] { new VirtualFile("invoice.pdf", () => Encoding.UTF8.GetBytes("PDF-DATA")) },
        };

        IReadOnlyList<ShelfItem> items = f.Importer.Import(payload);

        ShelfItem item = Assert.Single(items);
        Assert.Equal("invoice.pdf", item.Title);
        Assert.StartsWith(f.Temp.Root, item.Path);
        Assert.Equal("PDF-DATA", File.ReadAllText(item.Path!));
    }

    [Fact]
    public void VirtualFilesWithTheSameNameAreNumbered()
    {
        using var f = new Fixture();
        var payload = new FakePayload
        {
            VirtualFiles = new[]
            {
                new VirtualFile("photo.jpg", () => new byte[] { 1 }),
                new VirtualFile("photo.jpg", () => new byte[] { 2 }),
            },
        };

        IReadOnlyList<ShelfItem> items = f.Importer.Import(payload);

        Assert.Equal(new[] { "photo.jpg", "photo (2).jpg" }, items.Select(i => i.Title));
    }

    [Fact]
    public void AHostileVirtualFileNameCannotEscapeTheTempFolder()
    {
        using var f = new Fixture();
        var payload = new FakePayload
        {
            VirtualFiles = new[] { new VirtualFile("..\\..\\..\\evil.dll", () => new byte[] { 1 }) },
        };

        IReadOnlyList<ShelfItem> items = f.Importer.Import(payload);

        ShelfItem item = Assert.Single(items);
        Assert.Equal("evil.dll", item.Title);
        Assert.StartsWith(f.Temp.Root + System.IO.Path.DirectorySeparatorChar, System.IO.Path.GetFullPath(item.Path!));
    }

    [Fact]
    public void AVirtualFileThatCannotBeReadIsSkippedAndTheRestStillArrive()
    {
        using var f = new Fixture();
        var payload = new FakePayload
        {
            VirtualFiles = new[]
            {
                new VirtualFile("broken.bin", () => throw new IOException("sender went away")),
                new VirtualFile("empty-handed.bin", () => null),
                new VirtualFile("good.txt", () => new byte[] { 7 }),
            },
        };

        IReadOnlyList<ShelfItem> items = f.Importer.Import(payload);

        Assert.Equal(new[] { "good.txt" }, items.Select(i => i.Title));
    }

    [Fact]
    public void WhenEveryVirtualFileFailsNoEmptyFolderIsLeftAndTheNextKindIsTried()
    {
        using var f = new Fixture();
        var payload = new FakePayload
        {
            VirtualFiles = new[] { new VirtualFile("broken.bin", () => null) },
            Texts = new[] { "fallback" },
        };

        IReadOnlyList<ShelfItem> items = f.Importer.Import(payload);

        Assert.Equal(ShelfItemKind.Text, Assert.Single(items).Kind);
        Assert.False(Directory.Exists(f.Temp.Root) && Directory.EnumerateFileSystemEntries(f.Temp.Root).Any());
    }

    [Fact]
    public void AnImageIsSavedAsAPngNamedAfterTheTime()
    {
        using var f = new Fixture();
        var payload = new FakePayload { ImagePng = new byte[] { 0x89, 0x50, 0x4E, 0x47 } };

        IReadOnlyList<ShelfItem> items = f.Importer.Import(payload);

        ShelfItem item = Assert.Single(items);
        Assert.Equal("Image 2026-10-08 at 14.03.22.png", item.Title);
        Assert.Equal("Image", item.KindLabel);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, File.ReadAllBytes(item.Path!));
        Assert.StartsWith(f.Temp.Root, item.Path);
    }

    [Fact]
    public void AnEmptyImageIsNotAnImage()
    {
        using var f = new Fixture();
        IReadOnlyList<ShelfItem> items = f.Importer.Import(new FakePayload { ImagePng = Array.Empty<byte>(), Texts = new[] { "t" } });

        Assert.Equal(ShelfItemKind.Text, Assert.Single(items).Kind);
    }

    [Fact]
    public void LinksComeBeforeText()
    {
        using var f = new Fixture();
        var payload = new FakePayload
        {
            Links = new[] { new Uri("https://example.com/a"), new Uri("https://example.org/b") },
            Texts = new[] { "https://example.com/a" },
        };

        IReadOnlyList<ShelfItem> items = f.Importer.Import(payload);

        Assert.All(items, i => Assert.Equal(ShelfItemKind.Link, i.Kind));
        Assert.Equal(new[] { "example.com", "example.org" }, items.Select(i => i.Title));
    }

    [Fact]
    public void AnImageBeatsALinkAsInADragFromABrowser()
    {
        using var f = new Fixture();
        var payload = new FakePayload { ImagePng = new byte[] { 1, 2, 3 }, Links = new[] { new Uri("https://example.com/cat.png") } };

        IReadOnlyList<ShelfItem> items = f.Importer.Import(payload);

        Assert.Equal(ShelfItemKind.File, Assert.Single(items).Kind);
    }

    [Fact]
    public void TextIsKeptWhole()
    {
        using var f = new Fixture();

        IReadOnlyList<ShelfItem> items = f.Importer.Import(new FakePayload { Texts = new[] { "first line\nsecond line", "" } });

        ShelfItem item = Assert.Single(items);
        Assert.Equal("first line\nsecond line", item.Text);
        Assert.Equal("first line", item.Title);
    }

    [Fact]
    public void AnEmptyDropGivesNothing()
    {
        using var f = new Fixture();
        Assert.Empty(f.Importer.Import(new FakePayload()));
    }
}
