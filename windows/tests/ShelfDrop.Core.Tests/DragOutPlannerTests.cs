using Xunit;

namespace ShelfDrop.Core.Tests;

public class DragOutPlannerTests
{
    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Scratch = new TempDir();
            Temp = new TempStorage(System.IO.Path.Combine(Scratch.Path, "ShelfDrop"));
            Planner = new DragOutPlanner(Temp);
        }

        public TempDir Scratch { get; }
        public TempStorage Temp { get; }
        public DragOutPlanner Planner { get; }

        public void Dispose() => Scratch.Dispose();
    }

    [Fact]
    public void FilesGoOutAsTheFilesThemselves()
    {
        using var f = new Fixture();
        string a = f.Scratch.File("a.txt");
        string b = f.Scratch.File("b.txt");

        DragOutPayload payload = f.Planner.Plan(new[] { ShelfItem.ForFile(a), ShelfItem.ForFile(b) });

        Assert.Equal(new[] { a, b }, payload.FilePaths);
        Assert.Null(payload.Text);
        Assert.Null(payload.Url);
        Assert.False(Directory.Exists(f.Temp.Root), "files are handed over in place, not copied");
    }

    [Fact]
    public void ALoneTextGoesOutAsText()
    {
        using var f = new Fixture();

        DragOutPayload payload = f.Planner.Plan(new[] { ShelfItem.ForText("some words") });

        Assert.Equal("some words", payload.Text);
        Assert.Empty(payload.FilePaths);
        Assert.Null(payload.Url);
    }

    [Fact]
    public void ALoneLinkGoesOutAsALinkAndAsItsAddress()
    {
        using var f = new Fixture();
        var url = new Uri("https://example.com/page");

        DragOutPayload payload = f.Planner.Plan(new[] { ShelfItem.ForLink(url) });

        Assert.Equal(url, payload.Url);
        Assert.Equal("https://example.com/page", payload.Text);
        Assert.Empty(payload.FilePaths);
    }

    [Fact]
    public void ALinkAmongOthersBecomesAnInternetShortcutFile()
    {
        using var f = new Fixture();
        string file = f.Scratch.File("a.txt");

        DragOutPayload payload = f.Planner.Plan(new[] { ShelfItem.ForFile(file), ShelfItem.ForLink(new Uri("https://example.com/page")) });

        Assert.Equal(2, payload.FilePaths.Count);
        Assert.Equal(file, payload.FilePaths[0]);
        string shortcut = payload.FilePaths[1];
        Assert.Equal("example.com.url", System.IO.Path.GetFileName(shortcut));
        Assert.Equal("[InternetShortcut]\r\nURL=https://example.com/page\r\n", File.ReadAllText(shortcut));
        Assert.StartsWith(f.Temp.Root, shortcut);
    }

    [Fact]
    public void TextAmongOthersBecomesATextFile()
    {
        using var f = new Fixture();
        string file = f.Scratch.File("a.txt");

        DragOutPayload payload = f.Planner.Plan(new[] { ShelfItem.ForText("Meeting notes\nbuy milk"), ShelfItem.ForFile(file) });

        string note = payload.FilePaths[0];
        Assert.Equal("Meeting notes.txt", System.IO.Path.GetFileName(note));
        Assert.Equal("Meeting notes\nbuy milk", File.ReadAllText(note));
        Assert.Equal(file, payload.FilePaths[1]);
    }

    [Fact]
    public void TwoLinksToTheSameSiteGetDifferentFileNames()
    {
        using var f = new Fixture();

        DragOutPayload payload = f.Planner.Plan(new[]
        {
            ShelfItem.ForLink(new Uri("https://example.com/a")),
            ShelfItem.ForLink(new Uri("https://example.com/b")),
        });

        Assert.Equal(new[] { "example.com.url", "example.com (2).url" }, payload.FilePaths.Select(System.IO.Path.GetFileName));
    }

    [Fact]
    public void ATextTitleThatWindowsForbidsStillMakesAFile()
    {
        using var f = new Fixture();

        DragOutPayload payload = f.Planner.Plan(new[] { ShelfItem.ForText("what? a: b*"), ShelfItem.ForText("second") });

        Assert.Equal(new[] { "what_ a_ b_.txt", "second.txt" }, payload.FilePaths.Select(System.IO.Path.GetFileName));
    }

    [Fact]
    public void MissingFilesAreLeftOut()
    {
        using var f = new Fixture();
        string there = f.Scratch.File("there.txt");
        string gone = f.Scratch.File("gone.txt");
        ShelfItem goneItem = ShelfItem.ForFile(gone);
        File.Delete(gone);

        DragOutPayload payload = f.Planner.Plan(new[] { ShelfItem.ForFile(there), goneItem });

        Assert.Equal(new[] { there }, payload.FilePaths);
    }

    [Fact]
    public void AMissingFileAmongALoneTextStillGoesOutAsText()
    {
        using var f = new Fixture();
        string gone = f.Scratch.File("gone.txt");
        ShelfItem goneItem = ShelfItem.ForFile(gone);
        File.Delete(gone);

        DragOutPayload payload = f.Planner.Plan(new[] { goneItem, ShelfItem.ForText("kept") });

        Assert.Equal("kept", payload.Text);
    }

    [Fact]
    public void NothingUsableGivesAnEmptyPayload()
    {
        using var f = new Fixture();
        Assert.True(f.Planner.Plan(Array.Empty<ShelfItem>()).IsEmpty);
    }

    [Fact]
    public void CleanUpRemovesTheFilesItMade()
    {
        using var f = new Fixture();
        DragOutPayload payload = f.Planner.Plan(new[] { ShelfItem.ForText("a"), ShelfItem.ForText("b") });
        string made = payload.FilePaths[0];
        Assert.True(File.Exists(made));

        f.Planner.CleanUp();

        Assert.False(File.Exists(made));
        Assert.False(Directory.Exists(System.IO.Path.GetDirectoryName(made)));
    }

    [Fact]
    public void CleanUpNeverTouchesTheUsersFiles()
    {
        using var f = new Fixture();
        string theirs = f.Scratch.File("keep.txt");
        f.Planner.Plan(new[] { ShelfItem.ForFile(theirs), ShelfItem.ForText("a") });

        f.Planner.CleanUp();

        Assert.True(File.Exists(theirs));
    }
}
