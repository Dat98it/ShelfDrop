using Xunit;

namespace ShelfDrop.Core.Tests;

public class ShelfControllerTests
{
    private sealed class Fixture : IDisposable
    {
        public Fixture(IKeyValueStore? settings = null)
        {
            Scratch = new TempDir();
            Temp = new TempStorage(System.IO.Path.Combine(Scratch.Path, "ShelfDrop"));
            Settings = settings ?? new InMemoryKeyValueStore();
            Controller = Build();
        }

        public TempDir Scratch { get; }
        public TempStorage Temp { get; }
        public IKeyValueStore Settings { get; }
        public FakeWindow Window { get; } = new();
        public FakeScreens Screens { get; } = new();
        public FakeShare Share { get; } = new();
        public ManualDelays Delays { get; } = new();
        public ShelfController Controller { get; private set; }

        public ShelfController Build() => Controller = new ShelfController(
            Window, Screens, Share, Delays, new ShelfSizeStore(Settings), new ShelfPositionStore(Settings), Temp);

        public ShelfItem TempItem(string name = "pasted.png")
        {
            string path = System.IO.Path.Combine(Temp.MakeDirectory(), name);
            File.WriteAllText(path, "x");
            return ShelfItem.ForFile(path);
        }

        public void Dispose() => Scratch.Dispose();
    }

    /// <summary>Where a default-sized shelf sits when opened centred on a point, at 100% scaling.</summary>
    private static PixelRect CentredOn(int x, int y)
    {
        int width = (int)ShelfLimits.DefaultSize.Width;
        int height = (int)ShelfLimits.DefaultSize.Height;
        return new PixelRect(x - width / 2, y - height / 2, width, height);
    }

    // Showing

    [Fact]
    public void ShowsCentredOnTheCursorTheFirstTime()
    {
        using var f = new Fixture();

        f.Controller.Show(new PixelPoint(960, 540));

        Assert.True(f.Controller.IsShelfVisible);
        Assert.Equal(CentredOn(960, 540), f.Window.Frame);
    }

    [Fact]
    public void ShowingAnAlreadyVisibleShelfDoesNotMoveIt()
    {
        using var f = new Fixture();
        f.Controller.Show(new PixelPoint(960, 540));

        f.Controller.Show(new PixelPoint(100, 100));

        Assert.Single(f.Window.Shown);
    }

    [Fact]
    public void ToggleShowsAtTheCursorThenHides()
    {
        using var f = new Fixture();
        f.Screens.CursorPosition = new PixelPoint(500, 500);

        f.Controller.Toggle();
        Assert.True(f.Window.IsVisible);
        Assert.Equal(CentredOn(500, 500), f.Window.Frame);

        f.Controller.Toggle();
        Assert.False(f.Window.IsVisible);
    }

    // Remembering

    [Fact]
    public void OpensAtTheRememberedSizeAndPosition()
    {
        using var f = new Fixture();
        f.Controller.UserResized(new SizeDips(500, 420));
        f.Controller.UserMoved(new PixelPoint(1000, 200));

        ShelfController reopened = f.Build();
        reopened.Show(new PixelPoint(0, 0));

        Assert.Equal(new PixelRect(1000, 200, 500, 420), f.Window.Frame);
    }

    [Fact]
    public void UsesTheDefaultSizeUntilTheUserResizes()
    {
        using var f = new Fixture();
        Assert.Equal(ShelfLimits.DefaultSize, f.Controller.PreferredSize);
    }

    [Fact]
    public void AResizeBelowTheMinimumIsRaisedToIt()
    {
        using var f = new Fixture();

        f.Controller.UserResized(new SizeDips(10, 10));

        Assert.Equal(ShelfLimits.MinimumSize, f.Controller.PreferredSize);
        Assert.Equal(ShelfLimits.MinimumSize, new ShelfSizeStore(f.Settings).Load());
    }

    [Fact]
    public void AShrunkenWindowDoesNotOverwriteTheChosenSize()
    {
        using var f = new Fixture();
        f.Controller.UserResized(new SizeDips(1200, 900));
        f.Screens.Screens = new[] { FakeScreens.Screen(0, 0, 800, 640) };

        f.Controller.Show(new PixelPoint(400, 300));

        Assert.True(f.Window.Frame.Width < 1200);
        Assert.Equal(new SizeDips(1200, 900), f.Controller.PreferredSize);
        Assert.Equal(new SizeDips(1200, 900), new ShelfSizeStore(f.Settings).Load());
    }

    [Fact]
    public void MovingWritesTheSettingsOnce()
    {
        var settings = new InMemoryKeyValueStore();
        using var f = new Fixture(settings);

        f.Controller.UserMoved(new PixelPoint(10, 20));

        Assert.Equal(1, settings.WriteCount);
        Assert.Equal(new PixelPoint(10, 20), new ShelfPositionStore(settings).Load());
    }

    [Fact]
    public void ARememberedSpotIsUsedInTheSameRunToo()
    {
        using var f = new Fixture();
        f.Controller.Show(new PixelPoint(960, 540));
        f.Controller.UserMoved(new PixelPoint(50, 60));
        f.Controller.Hide();

        f.Controller.Show(new PixelPoint(960, 540));

        Assert.Equal(new PixelPoint(50, 60), f.Window.Frame.TopLeft);
    }

    [Fact]
    public void AfterRetiringTheSettingsNothingIsWrittenAnyMore()
    {
        var settings = new InMemoryKeyValueStore();
        using var f = new Fixture(settings);
        f.Controller.RetireSettings();

        f.Controller.UserMoved(new PixelPoint(10, 20));
        f.Controller.UserResized(new SizeDips(500, 500));

        Assert.Equal(0, settings.WriteCount);
        // The running app still behaves: it just does not persist.
        Assert.Equal(new SizeDips(500, 500), f.Controller.PreferredSize);
    }

    // Closing

    [Fact]
    public void CloseForgetsEverythingAndHides()
    {
        using var f = new Fixture();
        f.Controller.Show(new PixelPoint(960, 540));
        f.Controller.Model.Add(new[] { ShelfItem.ForText("a"), ShelfItem.ForText("b") });

        f.Controller.Close();

        Assert.Empty(f.Controller.Model.Items);
        Assert.False(f.Window.IsVisible);
    }

    [Fact]
    public void CloseDeletesTheTempCopiesButNeverTheUsersFiles()
    {
        using var f = new Fixture();
        ShelfItem temp = f.TempItem();
        string theirs = f.Scratch.File("Documents/report.docx");
        f.Controller.Model.Add(new[] { temp, ShelfItem.ForFile(theirs) });

        f.Controller.Close();

        Assert.False(File.Exists(temp.Path));
        Assert.True(File.Exists(theirs));
    }

    [Fact]
    public void ClearOnlyEmptiesTheShelf()
    {
        using var f = new Fixture();
        f.Controller.Show(new PixelPoint(960, 540));
        ShelfItem temp = f.TempItem();
        f.Controller.Model.Add(new[] { temp });

        f.Controller.Clear();

        Assert.Empty(f.Controller.Model.Items);
        Assert.True(f.Window.IsVisible);
        Assert.True(File.Exists(temp.Path), "Clear is not Close: the temp copies are cleaned up on Close");
    }

    [Fact]
    public void CloseDeletesTheSmallFilesMadeForDraggingLinksAndTextOut()
    {
        using var f = new Fixture();
        f.Controller.Model.Add(new[] { ShelfItem.ForText("a"), ShelfItem.ForText("b") });
        DragOutPayload payload = f.Controller.PlanDragOut(f.Controller.Model.Items.ToList());
        string made = payload.FilePaths[0];
        Assert.True(File.Exists(made));

        f.Controller.Close();

        Assert.False(File.Exists(made));
    }

    // Sharing

    [Fact]
    public void SharePresentsTheItems()
    {
        using var f = new Fixture();
        ShelfItem item = ShelfItem.ForText("hello");

        f.Controller.Share(new[] { item });

        Assert.Equal(new[] { item }, Assert.Single(f.Share.Presented));
    }

    [Fact]
    public void ShareLeavesOutFilesThatAreGoneAndDoesNothingIfNothingIsLeft()
    {
        using var f = new Fixture();
        string gone = f.Scratch.File("gone.txt");
        ShelfItem missing = ShelfItem.ForFile(gone);
        File.Delete(gone);

        f.Controller.Share(new[] { missing });
        Assert.Empty(f.Share.Presented);

        ShelfItem text = ShelfItem.ForText("kept");
        f.Controller.Share(new[] { missing, text });
        Assert.Equal(new[] { text }, Assert.Single(f.Share.Presented));
    }

    [Fact]
    public void SharedTempCopiesSurviveCloseBecauseTheTargetMayStillBeReadingThem()
    {
        using var f = new Fixture();
        ShelfItem shared = f.TempItem("shared.png");
        ShelfItem notShared = f.TempItem("other.png");
        f.Controller.Model.Add(new[] { shared, notShared });

        f.Controller.Share(new[] { shared });
        f.Share.Completed!(true);
        f.Controller.Close();

        Assert.True(File.Exists(shared.Path), "an AirDrop-style transfer can outlive the shelf");
        Assert.False(File.Exists(notShared.Path));
    }

    [Fact]
    public void ADismissedShareSheetProtectsNothing()
    {
        using var f = new Fixture();
        ShelfItem item = f.TempItem();
        f.Controller.Model.Add(new[] { item });

        f.Controller.Share(new[] { item });
        f.Share.Completed!(false);
        f.Controller.Close();

        Assert.False(File.Exists(item.Path));
    }

    [Fact]
    public void TheProtectionOnlyLastsUntilTheNextClose()
    {
        using var f = new Fixture();
        ShelfItem item = f.TempItem();
        f.Controller.Model.Add(new[] { item });
        f.Controller.Share(new[] { item });
        f.Share.Completed!(true);
        f.Controller.Close();
        Assert.True(File.Exists(item.Path));

        // The same file put on the shelf again and closed again is no longer protected.
        f.Controller.Model.Add(new[] { item });
        f.Controller.Close();

        Assert.False(File.Exists(item.Path));
    }

    // Drags

    [Fact]
    public void AnEmptyShelfOpenedForADragThatWentElsewhereHidesAfterAMoment()
    {
        using var f = new Fixture();
        f.Controller.Show(new PixelPoint(960, 540));

        f.Controller.DragEnded();
        Assert.True(f.Window.IsVisible, "it waits a moment so a drop onto the shelf can land first");

        f.Delays.RunAll();
        Assert.False(f.Window.IsVisible);
    }

    [Fact]
    public void AShelfWithItemsStaysAfterTheDragEnds()
    {
        using var f = new Fixture();
        f.Controller.Show(new PixelPoint(960, 540));
        f.Controller.Model.Add(new[] { ShelfItem.ForText("dropped") });

        f.Controller.DragEnded();
        f.Delays.RunAll();

        Assert.True(f.Window.IsVisible);
    }

    [Fact]
    public void AShelfThatIsStillBeingDroppedOnStays()
    {
        using var f = new Fixture();
        f.Controller.Show(new PixelPoint(960, 540));
        f.Controller.Model.IsDropTargeted = true;

        f.Controller.DragEnded();
        f.Delays.RunAll();

        Assert.True(f.Window.IsVisible);
    }

    [Fact]
    public void ANewDragCancelsThePendingHide()
    {
        using var f = new Fixture();
        f.Controller.Show(new PixelPoint(960, 540));
        f.Controller.DragEnded();

        f.Controller.DragBegan();
        f.Delays.RunAll();

        Assert.True(f.Window.IsVisible);
    }

    [Fact]
    public void ShowingAgainCancelsAPendingHideToo()
    {
        using var f = new Fixture();
        f.Controller.Show(new PixelPoint(960, 540));
        f.Controller.DragEnded();
        f.Controller.Hide();

        f.Controller.Show(new PixelPoint(960, 540));
        f.Delays.RunAll();

        Assert.True(f.Window.IsVisible);
    }

    [Fact]
    public void TwoDragEndsSchedulesOnlyOneHide()
    {
        using var f = new Fixture();
        f.Controller.Show(new PixelPoint(960, 540));

        f.Controller.DragEnded();
        f.Controller.DragEnded();

        Assert.Equal(1, f.Delays.PendingCount);
    }
}

public class ShelfModelTests
{
    [Fact]
    public void AddsAndRemovesById()
    {
        var model = new ShelfModel();
        ShelfItem a = ShelfItem.ForText("a");
        ShelfItem b = ShelfItem.ForText("b");
        model.Add(new[] { a, b });

        model.Remove(a.Id);

        Assert.Equal(new[] { b }, model.Items);
    }

    [Fact]
    public void RemovingAnUnknownIdChangesNothing()
    {
        var model = new ShelfModel();
        model.Add(new[] { ShelfItem.ForText("a") });

        model.Remove(Guid.NewGuid());

        Assert.Single(model.Items);
    }

    [Fact]
    public void ClearEmptiesIt()
    {
        var model = new ShelfModel();
        model.Add(new[] { ShelfItem.ForText("a"), ShelfItem.ForText("b") });

        model.Clear();

        Assert.Empty(model.Items);
    }

    [Fact]
    public void TellsTheViewWhenTheItemsChange()
    {
        var model = new ShelfModel();
        var changes = new List<System.Collections.Specialized.NotifyCollectionChangedAction>();
        ((System.Collections.Specialized.INotifyCollectionChanged)model.Items).CollectionChanged += (_, e) => changes.Add(e.Action);

        ShelfItem item = ShelfItem.ForText("a");
        model.Add(new[] { item });
        model.Remove(item.Id);

        Assert.Equal(
            new[] { System.Collections.Specialized.NotifyCollectionChangedAction.Add, System.Collections.Specialized.NotifyCollectionChangedAction.Remove },
            changes);
    }

    [Fact]
    public void TellsTheViewWhenADragEntersOrLeaves()
    {
        var model = new ShelfModel();
        var names = new List<string?>();
        model.PropertyChanged += (_, e) => names.Add(e.PropertyName);

        model.IsDropTargeted = true;
        model.IsDropTargeted = true;   // no change, no notification
        model.IsDropTargeted = false;

        Assert.Equal(new[] { "IsDropTargeted", "IsDropTargeted" }, names);
    }

    [Fact]
    public void KeepsDuplicatesBecauseTheUserMayWantTwoOfTheSame()
    {
        var model = new ShelfModel();
        model.Add(new[] { ShelfItem.ForText("same"), ShelfItem.ForText("same") });
        Assert.Equal(2, model.Items.Count);
    }
}
