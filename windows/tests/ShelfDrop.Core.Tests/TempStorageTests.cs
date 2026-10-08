using Xunit;

namespace ShelfDrop.Core.Tests;

public class TempStorageTests
{
    /// <summary>The temp folder is a subfolder of the scratch folder, so "outside" can be tested without leaving it.</summary>
    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Scratch = new TempDir();
            Root = System.IO.Path.Combine(Scratch.Path, "ShelfDrop");
            Storage = new TempStorage(Root);
        }

        public TempDir Scratch { get; }
        public string Root { get; }
        public TempStorage Storage { get; }

        public string MakeTempFile(string name = "a.txt")
        {
            string path = System.IO.Path.Combine(Storage.MakeDirectory(), name);
            File.WriteAllText(path, "x");
            return path;
        }

        public void Dispose() => Scratch.Dispose();
    }

    [Fact]
    public void EachDropGetsItsOwnFolderUnderTheRoot()
    {
        using var f = new Fixture();

        string a = f.Storage.MakeDirectory();
        string b = f.Storage.MakeDirectory();

        Assert.NotEqual(a, b);
        Assert.True(Directory.Exists(a));
        Assert.Equal(f.Root, System.IO.Path.GetDirectoryName(a));
    }

    [Fact]
    public void DiscardDeletesTheTempCopyAndItsEmptyFolder()
    {
        using var f = new Fixture();
        string path = f.MakeTempFile();
        string folder = System.IO.Path.GetDirectoryName(path)!;

        f.Storage.Discard(new[] { ShelfItem.ForFile(path) });

        Assert.False(File.Exists(path));
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void DiscardKeepsAFolderThatStillHoldsAnotherFile()
    {
        using var f = new Fixture();
        string folder = f.Storage.MakeDirectory();
        string one = System.IO.Path.Combine(folder, "one.txt");
        string two = System.IO.Path.Combine(folder, "two.txt");
        File.WriteAllText(one, "1");
        File.WriteAllText(two, "2");

        f.Storage.Discard(new[] { ShelfItem.ForFile(one) });

        Assert.False(File.Exists(one));
        Assert.True(File.Exists(two));
    }

    [Fact]
    public void DiscardNeverTouchesAFileTheUserOwns()
    {
        using var f = new Fixture();
        string theirs = f.Scratch.File("Documents/report.docx");

        f.Storage.Discard(new[] { ShelfItem.ForFile(theirs) });

        Assert.True(File.Exists(theirs));
    }

    [Fact]
    public void DiscardIgnoresLinksAndText()
    {
        using var f = new Fixture();
        f.Storage.Discard(new[] { ShelfItem.ForLink(new Uri("https://example.com")), ShelfItem.ForText("hi") });
    }

    [Fact]
    public void ASiblingFolderWithTheSamePrefixIsNotInsideTheRoot()
    {
        using var f = new Fixture();
        // ".../ShelfDropOther/x.txt" starts with ".../ShelfDrop" but is not inside it.
        string theirs = f.Scratch.File("ShelfDropOther/x.txt");

        f.Storage.Discard(new[] { ShelfItem.ForFile(theirs) });

        Assert.True(File.Exists(theirs));
    }

    [Fact]
    public void DiscardRefusesThePathsThatClimbOutOfTheRoot()
    {
        using var f = new Fixture();
        string theirs = f.Scratch.File("keep.txt");
        string sneaky = System.IO.Path.Combine(f.Storage.MakeDirectory(), "..", "..", "keep.txt");

        f.Storage.Discard(new[] { ShelfItem.ForFile(sneaky) });

        Assert.True(File.Exists(theirs));
    }

    [Fact]
    public void DiscardNeverDeletesTheRootItself()
    {
        using var f = new Fixture();
        f.Storage.MakeDirectory();

        f.Storage.Discard(new[] { ShelfItem.ForFile(f.Root) });
        f.Storage.DiscardDirectory(f.Root);

        Assert.True(Directory.Exists(f.Root));
    }

    [Fact]
    public void DiscardDoesNotFollowALinkOutOfTheRoot()
    {
        using var f = new Fixture();
        string theirs = f.Scratch.File("Documents/precious.txt");
        string folder = f.Storage.MakeDirectory();
        string link = System.IO.Path.Combine(folder, "shortcut.txt");
        if (!TryCreateFileLink(link, theirs)) return;   // creating links can need a privilege this machine does not grant

        f.Storage.Discard(new[] { ShelfItem.ForFile(link) });

        Assert.True(File.Exists(theirs), "the file behind the link must survive");
    }

    [Fact]
    public void DiscardDoesNotWalkThroughAFolderLink()
    {
        using var f = new Fixture();
        string theirs = f.Scratch.File("Documents/precious.txt");
        string folder = f.Storage.MakeDirectory();
        string link = System.IO.Path.Combine(folder, "docs");
        if (!TryCreateDirectoryLink(link, System.IO.Path.GetDirectoryName(theirs)!)) return;

        f.Storage.Discard(new[] { ShelfItem.ForFile(System.IO.Path.Combine(link, "precious.txt")) });

        Assert.True(File.Exists(theirs), "the file behind the folder link must survive");
    }

    [Fact]
    public void DiscardDirectoryDoesNotDeleteWhatAFolderLinkPointsAt()
    {
        using var f = new Fixture();
        string theirs = f.Scratch.File("Documents/precious.txt");
        string folder = f.Storage.MakeDirectory();
        string link = System.IO.Path.Combine(folder, "docs");
        if (!TryCreateDirectoryLink(link, System.IO.Path.GetDirectoryName(theirs)!)) return;

        f.Storage.DiscardDirectory(folder);
        f.Storage.DiscardDirectory(link);

        Assert.True(File.Exists(theirs), "the folder behind the link must survive");
    }

    [Fact]
    public void CleanUpRemovesEverythingUnderTheRoot()
    {
        using var f = new Fixture();
        f.MakeTempFile();
        f.MakeTempFile("b.txt");

        f.Storage.CleanUp();

        Assert.False(Directory.Exists(f.Root));
    }

    [Fact]
    public void CleanUpWithNothingThereIsFine()
    {
        using var f = new Fixture();
        f.Storage.CleanUp();
    }

    [Fact]
    public void DiscardDirectoryRemovesAWholeDropFolder()
    {
        using var f = new Fixture();
        string path = f.MakeTempFile();
        string folder = System.IO.Path.GetDirectoryName(path)!;

        f.Storage.DiscardDirectory(folder);

        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void DiscardDirectoryRefusesAFolderOutsideTheRoot()
    {
        using var f = new Fixture();
        string theirs = f.Scratch.Folder("Photos");

        f.Storage.DiscardDirectory(theirs);

        Assert.True(Directory.Exists(theirs));
    }

    [Fact]
    public void TheDefaultRootIsInTheSystemTempFolder()
    {
        var storage = new TempStorage();
        Assert.Equal(System.IO.Path.Combine(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()), "ShelfDrop"), storage.Root);
    }

    private static bool TryCreateFileLink(string link, string target)
    {
        try
        {
            File.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    private static bool TryCreateDirectoryLink(string link, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }
}
