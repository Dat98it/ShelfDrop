using Xunit;

namespace ShelfDrop.Core.Tests;

public class ShakeDetectorTests
{
    /// <summary>Feeds x positions at a fixed interval and reports whether any sample triggered a shake.</summary>
    private static bool Run(ShakeDetector detector, double[] xs, double interval)
    {
        bool triggered = false;
        for (int i = 0; i < xs.Length; i++)
        {
            if (detector.Feed(xs[i], i * interval)) triggered = true;
        }
        return triggered;
    }

    [Fact]
    public void FastBackAndForthTriggers()
    {
        // 100 px strokes every 0.1 s: right, left, right, left.
        Assert.True(Run(new ShakeDetector(), new double[] { 0, 100, 0, 100, 0 }, 0.1));
    }

    [Fact]
    public void StraightLineDoesNotTrigger()
    {
        double[] xs = Enumerable.Range(0, 101).Select(i => i * 10.0).ToArray();
        Assert.False(Run(new ShakeDetector(), xs, 0.016));
    }

    [Fact]
    public void SlowBackAndForthDoesNotTrigger()
    {
        // Same strokes as the fast case but 2 s apart: the reversals fall outside the window.
        Assert.False(Run(new ShakeDetector(), new double[] { 0, 100, 0, 100, 0 }, 2.0));
    }

    [Fact]
    public void SmallJitterDoesNotTrigger()
    {
        // A 5 px wobble is below the stroke distance, so no stroke is ever established.
        double[] xs = Enumerable.Range(0, 40).Select(i => i % 2 == 0 ? 0.0 : 5.0).ToArray();
        Assert.False(Run(new ShakeDetector(), xs, 0.02));
    }

    [Fact]
    public void TriggersOnlyOncePerShake()
    {
        var detector = new ShakeDetector();
        int count = 0;
        double[] xs = { 0, 100, 0, 100, 0, 100, 0 };
        for (int i = 0; i < xs.Length; i++)
        {
            if (detector.Feed(xs[i], i * 0.1)) count++;
        }
        // Three reversals trigger at index 4; the remaining samples only start a new, incomplete shake.
        Assert.Equal(1, count);
    }

    [Fact]
    public void ResetClearsProgress()
    {
        var detector = new ShakeDetector();
        // Two reversals so far: one more would normally trigger.
        double[] xs = { 0, 100, 0, 100 };
        for (int i = 0; i < xs.Length; i++) Assert.False(detector.Feed(xs[i], i * 0.1));

        detector.Reset();

        // After the reset, a single reversal must not complete the shake.
        Assert.False(Run(detector, new double[] { 0, 100, 0 }, 0.1));
    }

    [Fact]
    public void ShakingWorksWhenStartingByMovingLeft()
    {
        Assert.True(Run(new ShakeDetector(), new double[] { 500, 400, 500, 400, 500 }, 0.1));
    }

    [Fact]
    public void AStrokeJustUnderTheMinimumDoesNotCount()
    {
        Assert.False(Run(new ShakeDetector(), new double[] { 0, 39, 0, 39, 0, 39, 0 }, 0.1));
    }

    [Fact]
    public void AStrokeOfExactlyTheMinimumCounts()
    {
        Assert.True(Run(new ShakeDetector(), new double[] { 0, 40, 0, 40, 0 }, 0.1));
    }

    [Fact]
    public void ReversalsMustFitInTheWindow()
    {
        // Three reversals but 0.5 s apart: 1.5 s in all, more than the 0.8 s window.
        Assert.False(Run(new ShakeDetector(), new double[] { 0, 100, 0, 100, 0 }, 0.5));
    }

    [Fact]
    public void ScalesTheStrokeWithTheScreen()
    {
        ShakeConfiguration at200 = ShakeConfiguration.ForScale(2.0);
        Assert.Equal(80, at200.MinStrokeDistance);
        // A 60 px stroke is a clear shake at 100% but only a wobble at 200%.
        Assert.True(Run(new ShakeDetector(), new double[] { 0, 60, 0, 60, 0 }, 0.1));
        Assert.False(Run(new ShakeDetector(at200), new double[] { 0, 60, 0, 60, 0 }, 0.1));
    }

    [Fact]
    public void NeverScalesTheStrokeBelowTheBaseline()
    {
        Assert.Equal(40, ShakeConfiguration.ForScale(0.5).MinStrokeDistance);
    }
}
