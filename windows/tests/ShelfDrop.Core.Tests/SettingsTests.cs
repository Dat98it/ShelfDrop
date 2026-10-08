using Xunit;

namespace ShelfDrop.Core.Tests;

public class JsonFileKeyValueStoreTests
{
    [Fact]
    public void RemembersValuesAcrossInstances()
    {
        using var dir = new TempDir();
        string path = System.IO.Path.Combine(dir.Path, "ShelfDrop", "settings.json");

        new JsonFileKeyValueStore(path).SetDoubles(("a", 1.5), ("b", -3));

        var reopened = new JsonFileKeyValueStore(path);
        Assert.Equal(1.5, reopened.GetDouble("a"));
        Assert.Equal(-3, reopened.GetDouble("b"));
        Assert.Null(reopened.GetDouble("missing"));
    }

    [Fact]
    public void ANewValueDoesNotLoseTheOthers()
    {
        using var dir = new TempDir();
        string path = System.IO.Path.Combine(dir.Path, "settings.json");
        var store = new JsonFileKeyValueStore(path);

        store.SetDoubles(("a", 1));
        store.SetDoubles(("b", 2));

        var reopened = new JsonFileKeyValueStore(path);
        Assert.Equal(1, reopened.GetDouble("a"));
        Assert.Equal(2, reopened.GetDouble("b"));
    }

    [Fact]
    public void ACorruptFileMeansNothingIsSavedYet()
    {
        using var dir = new TempDir();
        string path = dir.File("settings.json", "{ this is not json");
        var store = new JsonFileKeyValueStore(path);

        Assert.Null(store.GetDouble("a"));

        // And saving still works, replacing the broken file.
        store.SetDoubles(("a", 7));
        Assert.Equal(7, new JsonFileKeyValueStore(path).GetDouble("a"));
    }

    [Fact]
    public void AFileOfTheWrongShapeMeansNothingIsSavedYet()
    {
        using var dir = new TempDir();
        string path = dir.File("settings.json", "[1, 2, 3]");
        Assert.Null(new JsonFileKeyValueStore(path).GetDouble("a"));
    }

    [Fact]
    public void ANonFiniteNumberIsNotWritten()
    {
        using var dir = new TempDir();
        string path = System.IO.Path.Combine(dir.Path, "settings.json");
        var store = new JsonFileKeyValueStore(path);

        store.SetDoubles(("a", double.NaN), ("b", double.PositiveInfinity), ("c", 4));

        var reopened = new JsonFileKeyValueStore(path);
        Assert.Null(reopened.GetDouble("a"));
        Assert.Null(reopened.GetDouble("b"));
        Assert.Equal(4, reopened.GetDouble("c"));
    }

    [Fact]
    public void LeavesNoTemporaryFileBehind()
    {
        using var dir = new TempDir();
        string path = System.IO.Path.Combine(dir.Path, "settings.json");

        new JsonFileKeyValueStore(path).SetDoubles(("a", 1));

        Assert.Equal(new[] { path }, Directory.GetFileSystemEntries(dir.Path));
    }

    [Fact]
    public void EraseRemovesTheFileAndItsEmptyFolder()
    {
        using var dir = new TempDir();
        string folder = System.IO.Path.Combine(dir.Path, "ShelfDrop");
        string path = System.IO.Path.Combine(folder, "settings.json");
        var store = new JsonFileKeyValueStore(path);
        store.SetDoubles(("a", 1));

        store.Erase();

        Assert.False(File.Exists(path));
        Assert.False(Directory.Exists(folder));
        Assert.Null(store.GetDouble("a"));
    }

    [Fact]
    public void EraseKeepsAFolderThatHoldsSomethingElse()
    {
        using var dir = new TempDir();
        string folder = System.IO.Path.Combine(dir.Path, "ShelfDrop");
        string path = System.IO.Path.Combine(folder, "settings.json");
        var store = new JsonFileKeyValueStore(path);
        store.SetDoubles(("a", 1));
        string other = System.IO.Path.Combine(folder, "notes.txt");
        File.WriteAllText(other, "mine");

        store.Erase();

        Assert.False(File.Exists(path));
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void AfterEraseNothingIsWrittenBack()
    {
        using var dir = new TempDir();
        string path = System.IO.Path.Combine(dir.Path, "ShelfDrop", "settings.json");
        var store = new JsonFileKeyValueStore(path);
        store.SetDoubles(("a", 1));
        store.Erase();

        store.SetDoubles(("a", 2));

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void EraseWithNothingSavedIsFine()
    {
        using var dir = new TempDir();
        new JsonFileKeyValueStore(System.IO.Path.Combine(dir.Path, "none", "settings.json")).Erase();
    }

    [Fact]
    public void TheDefaultFileIsUnderTheUsersAppData()
    {
        string path = JsonFileKeyValueStore.DefaultPath;
        Assert.EndsWith(System.IO.Path.Combine("ShelfDrop", "settings.json"), path);
    }
}

public class ShelfSizeStoreTests
{
    [Fact]
    public void ReturnsNullUntilTheUserResizes()
    {
        Assert.Null(new ShelfSizeStore(new InMemoryKeyValueStore()).Load());
    }

    [Fact]
    public void RemembersTheSize()
    {
        var backing = new InMemoryKeyValueStore();
        new ShelfSizeStore(backing).Save(new SizeDips(500, 420));

        Assert.Equal(new SizeDips(500, 420), new ShelfSizeStore(backing).Load());
    }

    [Fact]
    public void WritesWidthAndHeightTogether()
    {
        var backing = new InMemoryKeyValueStore();
        new ShelfSizeStore(backing).Save(new SizeDips(500, 420));
        Assert.Equal(1, backing.WriteCount);
    }

    [Fact]
    public void ASizeBelowTheMinimumIsRaisedToIt()
    {
        var backing = new InMemoryKeyValueStore();
        backing.SetDoubles(("shelfWidth", 10), ("shelfHeight", 10));

        Assert.Equal(ShelfLimits.MinimumSize, new ShelfSizeStore(backing).Load());
    }

    [Fact]
    public void AnAbsurdSizeIsCapped()
    {
        var backing = new InMemoryKeyValueStore();
        backing.SetDoubles(("shelfWidth", 1e9), ("shelfHeight", 5e5));

        Assert.Equal(new SizeDips(ShelfSizeStore.MaximumStoredLength, ShelfSizeStore.MaximumStoredLength), new ShelfSizeStore(backing).Load());
    }

    [Fact]
    public void HalfASizeIsNoSize()
    {
        var backing = new InMemoryKeyValueStore();
        backing.SetDoubles(("shelfWidth", 400));
        Assert.Null(new ShelfSizeStore(backing).Load());
    }
}

public class ShelfPositionStoreTests
{
    [Fact]
    public void ReturnsNullUntilTheUserMovesTheShelf()
    {
        Assert.Null(new ShelfPositionStore(new InMemoryKeyValueStore()).Load());
    }

    [Fact]
    public void RemembersThePosition()
    {
        var backing = new InMemoryKeyValueStore();
        new ShelfPositionStore(backing).Save(new PixelPoint(-1500, 220));

        Assert.Equal(new PixelPoint(-1500, 220), new ShelfPositionStore(backing).Load());
    }

    [Fact]
    public void AnAbsurdPositionIsIgnored()
    {
        var backing = new InMemoryKeyValueStore();
        backing.SetDoubles(("shelfTopLeftX", 5e7), ("shelfTopLeftY", 0));
        Assert.Null(new ShelfPositionStore(backing).Load());
    }

    [Fact]
    public void HalfAPositionIsNoPosition()
    {
        var backing = new InMemoryKeyValueStore();
        backing.SetDoubles(("shelfTopLeftX", 10));
        Assert.Null(new ShelfPositionStore(backing).Load());
    }

    [Fact]
    public void AMonitorLeftOfOrAboveThePrimaryHasNegativeCoordinatesAndThatIsFine()
    {
        var backing = new InMemoryKeyValueStore();
        new ShelfPositionStore(backing).Save(new PixelPoint(-1920, -300));
        Assert.Equal(new PixelPoint(-1920, -300), new ShelfPositionStore(backing).Load());
    }
}
