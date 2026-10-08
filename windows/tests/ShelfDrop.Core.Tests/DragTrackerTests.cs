using Xunit;

namespace ShelfDrop.Core.Tests;

public class DragTrackerTests
{
    private sealed class Recorder
    {
        public Recorder(DragTracker tracker)
        {
            tracker.DragBegan += () => Events.Add("began");
            tracker.DragEnded += () => Events.Add("ended");
            tracker.Shaken += p => Events.Add($"shake@{p.X},{p.Y}");
        }

        public List<string> Events { get; } = new();
    }

    private static (DragTracker Tracker, Recorder Log) Make(double threshold = 8)
    {
        var tracker = new DragTracker(new ShakeDetector(), threshold);
        return (tracker, new Recorder(tracker));
    }

    /// <summary>Moves right and left 100 px, four times, 0.1 s apart (a hard shake).</summary>
    private static void ShakeIt(DragTracker tracker, int y = 50, double startTime = 0)
    {
        double[] xs = { 100, 200, 100, 200, 100 };
        for (int i = 0; i < xs.Length; i++) tracker.OnMove(new PixelPoint((int)xs[i], y), startTime + i * 0.1);
    }

    [Fact]
    public void ShakingWithoutTheButtonDownIsNotADrag()
    {
        var (tracker, log) = Make();

        ShakeIt(tracker);

        Assert.Empty(log.Events);
        Assert.False(tracker.IsDragging);
    }

    [Fact]
    public void AClickWithoutMovingIsNotADrag()
    {
        var (tracker, log) = Make();

        tracker.OnLeftDown(new PixelPoint(100, 50));
        tracker.OnMove(new PixelPoint(103, 52), 0);   // within the threshold
        tracker.OnLeftUp();

        Assert.Empty(log.Events);
    }

    [Fact]
    public void MovingPastTheThresholdWithTheButtonDownBeginsADrag()
    {
        var (tracker, log) = Make();

        tracker.OnLeftDown(new PixelPoint(100, 50));
        tracker.OnMove(new PixelPoint(120, 50), 0);

        Assert.Equal(new[] { "began" }, log.Events);
        Assert.True(tracker.IsDragging);
    }

    [Fact]
    public void ReleasingTheButtonEndsTheDragOnce()
    {
        var (tracker, log) = Make();
        tracker.OnLeftDown(new PixelPoint(100, 50));
        tracker.OnMove(new PixelPoint(120, 50), 0);

        tracker.OnLeftUp();
        tracker.OnLeftUp();

        Assert.Equal(new[] { "began", "ended" }, log.Events);
        Assert.False(tracker.IsDragging);
    }

    [Fact]
    public void AShakeDuringADragIsReportedWithTheSpotWhereItHappened()
    {
        var (tracker, log) = Make();
        tracker.OnLeftDown(new PixelPoint(100, 50));

        ShakeIt(tracker, y: 77);

        Assert.Contains("shake@100,77", log.Events);
        Assert.Single(log.Events, e => e.StartsWith("shake", StringComparison.Ordinal));
    }

    [Fact]
    public void ShakingAfterTheButtonIsReleasedDoesNothing()
    {
        var (tracker, log) = Make();
        tracker.OnLeftDown(new PixelPoint(100, 50));
        tracker.OnMove(new PixelPoint(120, 50), 0);
        tracker.OnLeftUp();
        log.Events.Clear();

        ShakeIt(tracker, startTime: 1);

        Assert.Empty(log.Events);
    }

    [Fact]
    public void ProgressFromBeforeTheDragDoesNotCountTowardsTheShake()
    {
        var (tracker, log) = Make();
        tracker.OnLeftDown(new PixelPoint(100, 50));
        // The first moves are still short strokes of an ordinary drag: they must not be mistaken for a shake.
        tracker.OnMove(new PixelPoint(120, 50), 0);
        tracker.OnMove(new PixelPoint(140, 50), 0.1);
        tracker.OnMove(new PixelPoint(120, 50), 0.2);

        Assert.DoesNotContain(log.Events, e => e.StartsWith("shake", StringComparison.Ordinal));
    }

    [Fact]
    public void AMissedButtonReleaseIsCaughtByPollingTheButton()
    {
        var (tracker, log) = Make();
        tracker.OnLeftDown(new PixelPoint(100, 50));
        tracker.OnMove(new PixelPoint(120, 50), 0);

        tracker.ObserveButton(leftIsDown: true);
        Assert.True(tracker.IsDragging);

        tracker.ObserveButton(leftIsDown: false);

        Assert.Equal(new[] { "began", "ended" }, log.Events);
    }

    [Fact]
    public void AfterAMissedReleaseTheNextClickStartsFresh()
    {
        var (tracker, log) = Make();
        tracker.OnLeftDown(new PixelPoint(100, 50));
        tracker.OnMove(new PixelPoint(120, 50), 0);
        tracker.ObserveButton(leftIsDown: false);
        log.Events.Clear();

        // No new button press: moving must not restart a drag.
        tracker.OnMove(new PixelPoint(400, 50), 1);
        Assert.Empty(log.Events);

        tracker.OnLeftDown(new PixelPoint(400, 50));
        tracker.OnMove(new PixelPoint(430, 50), 2);
        Assert.Equal(new[] { "began" }, log.Events);
    }

    [Fact]
    public void ANewButtonPressEndsADragThatNeverSawItsRelease()
    {
        var (tracker, log) = Make();
        tracker.OnLeftDown(new PixelPoint(100, 50));
        tracker.OnMove(new PixelPoint(120, 50), 0);

        tracker.OnLeftDown(new PixelPoint(500, 500));

        Assert.Equal(new[] { "began", "ended" }, log.Events);
    }

    [Fact]
    public void APressThatTheCallerWantsIgnoredNeverBecomesADrag()
    {
        var (tracker, log) = Make();
        tracker.ShouldIgnorePress = p => p.X < 50;   // say, the shelf's own area

        tracker.OnLeftDown(new PixelPoint(10, 50));
        tracker.OnMove(new PixelPoint(120, 50), 0);
        ShakeIt(tracker);
        tracker.OnLeftUp();

        Assert.Empty(log.Events);
    }

    [Fact]
    public void IgnoringOnePressDoesNotSpoilTheNext()
    {
        var (tracker, log) = Make();
        tracker.ShouldIgnorePress = p => p.X < 50;
        tracker.OnLeftDown(new PixelPoint(10, 50));
        tracker.OnLeftUp();

        tracker.OnLeftDown(new PixelPoint(300, 50));
        tracker.OnMove(new PixelPoint(330, 50), 1);

        Assert.Equal(new[] { "began" }, log.Events);
    }

    [Fact]
    public void IgnoringAPressEndsADragThatNeverSawItsRelease()
    {
        var (tracker, log) = Make();
        tracker.ShouldIgnorePress = p => p.X < 50;
        tracker.OnLeftDown(new PixelPoint(300, 50));
        tracker.OnMove(new PixelPoint(330, 50), 0);

        tracker.OnLeftDown(new PixelPoint(10, 50));

        Assert.Equal(new[] { "began", "ended" }, log.Events);
        Assert.False(tracker.IsDragging);
    }

    [Fact]
    public void TheThresholdIsMeasuredInAnyDirection()
    {
        var (tracker, log) = Make(threshold: 10);
        tracker.OnLeftDown(new PixelPoint(100, 100));

        tracker.OnMove(new PixelPoint(100, 108), 0);
        Assert.Empty(log.Events);

        tracker.OnMove(new PixelPoint(100, 111), 0.1);
        Assert.Equal(new[] { "began" }, log.Events);
    }
}
