namespace ShelfDrop.Core;

/// <summary>
/// Detects a "shake": quick back-and-forth movement along the X axis.
///
/// Feed it mouse positions while a drag is in progress. A stroke only counts once it travels at least
/// <see cref="ShakeConfiguration.MinStrokeDistance"/>, which filters out hand tremor. A shake is reported
/// when <see cref="ShakeConfiguration.RequiredReversals"/> direction changes happen within
/// <see cref="ShakeConfiguration.Window"/> seconds.
/// </summary>
public sealed class ShakeDetector
{
    private readonly ShakeConfiguration _configuration;

    /// <summary>-1 = moving left, +1 = moving right, 0 = no stroke established yet.</summary>
    private int _direction;

    /// <summary>Anchor before a stroke exists, then the furthest x reached in the current stroke.</summary>
    private double _extremeX;

    private bool _hasAnchor;
    private readonly List<double> _reversalTimes = new();

    public ShakeDetector(ShakeConfiguration? configuration = null)
    {
        _configuration = configuration ?? new ShakeConfiguration();
    }

    public void Reset()
    {
        _direction = 0;
        _extremeX = 0;
        _hasAnchor = false;
        _reversalTimes.Clear();
    }

    /// <summary>
    /// Returns true when this sample completes a shake. State is reset afterwards so one shake triggers once.
    /// </summary>
    /// <param name="x">Horizontal position in pixels.</param>
    /// <param name="timeSeconds">When the sample was taken, in seconds on any steady clock.</param>
    public bool Feed(double x, double timeSeconds)
    {
        if (!_hasAnchor)
        {
            _extremeX = x;
            _hasAnchor = true;
            return false;
        }

        if (_direction == 0)
        {
            double delta = x - _extremeX;
            if (Math.Abs(delta) >= _configuration.MinStrokeDistance)
            {
                _direction = delta > 0 ? 1 : -1;
                _extremeX = x;
            }
            return false;
        }

        if (x * _direction > _extremeX * _direction)
        {
            // Still travelling the same way: extend the stroke.
            _extremeX = x;
            return false;
        }

        // Moving against the stroke: it is a reversal once it has gone far enough back.
        if ((_extremeX - x) * _direction >= _configuration.MinStrokeDistance)
        {
            _direction = -_direction;
            _extremeX = x;
            _reversalTimes.Add(timeSeconds);
            _reversalTimes.RemoveAll(t => timeSeconds - t > _configuration.Window);

            if (_reversalTimes.Count >= _configuration.RequiredReversals)
            {
                Reset();
                return true;
            }
        }
        return false;
    }
}

public sealed record ShakeConfiguration
{
    /// <summary>How far one stroke must travel to count, in pixels.</summary>
    public double MinStrokeDistance { get; init; } = 40;

    public int RequiredReversals { get; init; } = 3;

    /// <summary>The time all reversals must fit in, in seconds.</summary>
    public double Window { get; init; } = 0.8;

    /// <summary>
    /// The default shake, with the stroke length scaled to the screen so that it is the same physical distance
    /// on a high-DPI monitor (40 px is half as far on a 200% screen).
    /// </summary>
    public static ShakeConfiguration ForScale(double scale) =>
        new() { MinStrokeDistance = 40 * Math.Max(1.0, scale) };
}
