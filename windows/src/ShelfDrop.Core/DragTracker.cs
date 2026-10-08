namespace ShelfDrop.Core;

/// <summary>
/// Decides, from a stream of mouse events, when a drag is under way and when it is shaken.
///
/// Windows has no system-wide "what is being dragged" board to look at (the pasteboard on macOS), so a drag is
/// recognised the way every drag starts: the left button goes down and the pointer travels past the drag
/// threshold before it comes up. A shake is only looked for while that is the case.
/// </summary>
public sealed class DragTracker
{
    private readonly ShakeDetector _shake;
    private readonly double _dragThreshold;
    private bool _buttonDown;
    private PixelPoint _downAt;

    /// <param name="shake">The detector fed with the pointer's x position during a drag.</param>
    /// <param name="dragThreshold">How far (pixels) the pointer must travel with the button down to count as a drag.</param>
    public DragTracker(ShakeDetector shake, double dragThreshold = 8)
    {
        _shake = shake;
        _dragThreshold = dragThreshold;
    }

    public bool IsDragging { get; private set; }

    /// <summary>
    /// Decides, for the spot where the button went down, whether to leave this press alone. A press on the shelf itself
    /// (moving it, resizing it, dragging an item out) is not a drag on its way to the shelf.
    /// </summary>
    public Func<PixelPoint, bool>? ShouldIgnorePress { get; set; }

    public event Action? DragBegan;
    public event Action? DragEnded;

    /// <summary>The pointer's position when the shake was completed.</summary>
    public event Action<PixelPoint>? Shaken;

    public void OnLeftDown(PixelPoint at)
    {
        if (IsDragging) End();
        if (ShouldIgnorePress?.Invoke(at) == true)
        {
            _buttonDown = false;
            return;
        }
        _buttonDown = true;
        _downAt = at;
    }

    public void OnMove(PixelPoint at, double timeSeconds)
    {
        if (!_buttonDown) return;

        if (!IsDragging)
        {
            double distance = Math.Sqrt(Math.Pow(at.X - _downAt.X, 2) + Math.Pow(at.Y - _downAt.Y, 2));
            if (distance < _dragThreshold) return;
            Begin();
        }

        if (_shake.Feed(at.X, timeSeconds)) Shaken?.Invoke(at);
    }

    public void OnLeftUp()
    {
        _buttonDown = false;
        End();
    }

    /// <summary>
    /// A safety net for a button release that the mouse hook never saw (the hook can be switched off by the system
    /// when it is too slow, and a secure desktop such as the UAC prompt hides input from it).
    /// </summary>
    public void ObserveButton(bool leftIsDown)
    {
        if (leftIsDown) return;
        _buttonDown = false;
        End();
    }

    private void Begin()
    {
        IsDragging = true;
        _shake.Reset();
        DragBegan?.Invoke();
    }

    private void End()
    {
        if (!IsDragging) return;
        IsDragging = false;
        _shake.Reset();
        DragEnded?.Invoke();
    }
}
