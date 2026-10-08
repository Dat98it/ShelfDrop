using Xunit;

namespace ShelfDrop.Core.Tests;

public class ShelfPlacementTests
{
    private static readonly SizeDips Size = new(360, 300);
    private static readonly ScreenInfo Main = FakeScreens.Screen(0, 0, 1920, 1080);

    private static PixelRect Place(PixelPoint cursor, PixelPoint? remembered = null, SizeDips? size = null, params ScreenInfo[] screens) =>
        ShelfPlacement.Compute(size ?? Size, remembered, cursor, screens.Length == 0 ? new[] { Main } : screens);

    [Fact]
    public void OpensCentredOnTheCursorWhenNothingIsRemembered()
    {
        PixelRect frame = Place(new PixelPoint(960, 540));

        Assert.Equal(new PixelRect(960 - 180, 540 - 150, 360, 300), frame);
    }

    [Fact]
    public void OpensWhereTheUserLastPutItEvenIfTheCursorIsElsewhere()
    {
        PixelRect frame = Place(new PixelPoint(100, 100), remembered: new PixelPoint(1200, 400));

        Assert.Equal(new PixelRect(1200, 400, 360, 300), frame);
    }

    [Fact]
    public void AnEdgeOfTheScreenPushesItBackInside()
    {
        PixelRect frame = Place(new PixelPoint(0, 0));

        Assert.Equal(8, frame.Left);
        Assert.Equal(8, frame.Top);
    }

    [Fact]
    public void TheTaskbarIsKeptClear()
    {
        // The work area ends at y = 1040 (a 40 px taskbar), so the bottom edge must stay 8 px above it.
        PixelRect frame = Place(new PixelPoint(960, 1079));

        Assert.Equal(1040 - 8, frame.Bottom);
    }

    [Fact]
    public void TheRightEdgeIsKeptInsideToo()
    {
        PixelRect frame = Place(new PixelPoint(1919, 540));
        Assert.Equal(1920 - 8, frame.Right);
    }

    [Fact]
    public void ARememberedSpotThatSticksOutIsPulledBackInside()
    {
        PixelRect frame = Place(new PixelPoint(0, 0), remembered: new PixelPoint(1800, 900));

        Assert.True(frame.Right <= 1920 - 8);
        Assert.True(frame.Bottom <= 1040 - 8);
    }

    [Fact]
    public void ARememberedSpotOnAMonitorThatIsGoneFallsBackToTheCursor()
    {
        // The user left the shelf on a second monitor that has since been unplugged.
        PixelRect frame = Place(new PixelPoint(960, 540), remembered: new PixelPoint(2500, 300));

        Assert.Equal(new PixelRect(780, 390, 360, 300), frame);
    }

    [Fact]
    public void ARememberedSpotOnASecondMonitorIsUsedWhileItIsThere()
    {
        ScreenInfo second = FakeScreens.Screen(1920, 0, 1920, 1080, primary: false);

        PixelRect frame = Place(new PixelPoint(100, 100), remembered: new PixelPoint(2500, 300), screens: new[] { Main, second });

        Assert.Equal(new PixelRect(2500, 300, 360, 300), frame);
    }

    [Fact]
    public void ARememberedSpotOnAMonitorLeftOfThePrimaryHasNegativeCoordinates()
    {
        ScreenInfo left = FakeScreens.Screen(-1920, 0, 1920, 1080, primary: false);

        PixelRect frame = Place(new PixelPoint(100, 100), remembered: new PixelPoint(-1500, 200), screens: new[] { left, Main });

        Assert.Equal(new PixelRect(-1500, 200, 360, 300), frame);
    }

    [Fact]
    public void OpensOnTheMonitorTheCursorIsOn()
    {
        ScreenInfo second = FakeScreens.Screen(1920, 0, 1920, 1080, primary: false);

        PixelRect frame = Place(new PixelPoint(2880, 540), screens: new[] { Main, second });

        Assert.Equal(new PixelRect(2880 - 180, 540 - 150, 360, 300), frame);
    }

    [Fact]
    public void AHighDpiMonitorGetsAProportionallyBiggerWindow()
    {
        ScreenInfo hiDpi = FakeScreens.Screen(0, 0, 3840, 2160, scale: 2.0);

        PixelRect frame = Place(new PixelPoint(1920, 1080), screens: new[] { hiDpi });

        Assert.Equal(720, frame.Width);
        Assert.Equal(600, frame.Height);
        // The margin scales as well.
        PixelRect cornered = Place(new PixelPoint(0, 0), screens: new[] { hiDpi });
        Assert.Equal(16, cornered.Left);
    }

    [Fact]
    public void AScreenTooSmallForTheShelfShrinksItButTheChoiceIsKept()
    {
        var tiny = FakeScreens.Screen(0, 0, 300, 240);   // work area 300 x 200

        PixelRect frame = ShelfPlacement.Compute(Size, null, new PixelPoint(150, 100), new[] { tiny });

        Assert.Equal(300 - 16, frame.Width);
        Assert.Equal(200 - 16, frame.Height);
        Assert.Equal(8, frame.Left);
        Assert.Equal(8, frame.Top);
    }

    [Fact]
    public void WorksWithoutAnyScreenInformation()
    {
        PixelRect frame = ShelfPlacement.Compute(Size, null, new PixelPoint(500, 500), Array.Empty<ScreenInfo>());

        Assert.Equal(new PixelRect(320, 350, 360, 300), frame);
    }

    [Fact]
    public void ACursorOutsideEveryScreenUsesThePrimaryOne()
    {
        ScreenInfo second = FakeScreens.Screen(1920, 0, 1920, 1080, primary: false);

        PixelRect frame = Place(new PixelPoint(-5000, -5000), screens: new[] { second, Main });

        Assert.Equal(8, frame.Left);
        Assert.Equal(8, frame.Top);
    }

    [Fact]
    public void ARememberedSpotExactlyOnAnEdgeBelongsToTheScreenThatOwnsThatPixel()
    {
        ScreenInfo second = FakeScreens.Screen(1920, 0, 1920, 1080, primary: false);

        // x = 1919 is the last pixel of the first monitor; probing just inside the corner lands on the second one.
        PixelRect frame = Place(new PixelPoint(0, 0), remembered: new PixelPoint(1919, 100), screens: new[] { Main, second });

        Assert.True(frame.Left >= 1920, $"expected the second monitor, got {frame}");
    }

    [Fact]
    public void ContainsIsHalfOpen()
    {
        var rect = new PixelRect(10, 10, 100, 50);
        Assert.True(rect.Contains(new PixelPoint(10, 10)));
        Assert.True(rect.Contains(new PixelPoint(109, 59)));
        Assert.False(rect.Contains(new PixelPoint(110, 30)));
        Assert.False(rect.Contains(new PixelPoint(50, 60)));
    }
}
