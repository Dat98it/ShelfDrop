namespace ShelfDrop.Core;

/// <summary>The floating window that hosts the shelf. The real one never takes focus from the program being dragged from.</summary>
public interface IShelfWindow
{
    bool IsVisible { get; }

    /// <summary>Where the window actually is, in physical pixels. Only meaningful while visible.</summary>
    PixelRect Frame { get; }

    /// <summary>Shows the window at <paramref name="frame"/> without activating it: the drag in progress must keep working.</summary>
    void ShowWithoutActivating(PixelRect frame);

    void Hide();
}

public interface IScreenProvider
{
    IReadOnlyList<ScreenInfo> Screens { get; }
    PixelPoint CursorPosition { get; }
}

/// <summary>Opens the system share sheet.</summary>
public interface ISharePresenter
{
    /// <param name="items">Only items that can be shared right now.</param>
    /// <param name="completed">Called with true once the user picked a place to share to, false if they dismissed it.</param>
    void Present(IReadOnlyList<ShelfItem> items, Action<bool> completed);
}

public interface IDelayedActions
{
    IDisposable Schedule(TimeSpan delay, Action action);
}

/// <summary>Wires the pieces together: shake → shelf window ↔ shelf model, and remembers what the user did to the window.</summary>
public sealed class ShelfController
{
    /// <summary>An empty shelf opened for a drag that went elsewhere should not linger.</summary>
    public static readonly TimeSpan EmptyShelfGracePeriod = TimeSpan.FromMilliseconds(500);

    private readonly IShelfWindow _window;
    private readonly IScreenProvider _screens;
    private readonly ISharePresenter _share;
    private readonly IDelayedActions _delays;
    private readonly ShelfSizeStore _sizeStore;
    private readonly ShelfPositionStore _positionStore;
    private readonly TempStorage _temp;
    private readonly DragOutPlanner _dragOut;

    /// <summary>
    /// Items the user picked a share target for. Their temp copies may still be read by that target
    /// (a transfer can outlive the shelf), so closing the shelf must not delete them.
    /// </summary>
    private readonly HashSet<Guid> _sharedItemIds = new();

    private PixelPoint? _rememberedTopLeft;
    private IDisposable? _pendingHide;
    private bool _settingsRetired;

    public ShelfController(
        IShelfWindow window,
        IScreenProvider screens,
        ISharePresenter share,
        IDelayedActions delays,
        ShelfSizeStore sizeStore,
        ShelfPositionStore positionStore,
        TempStorage temp,
        DragOutPlanner? dragOut = null,
        ShelfModel? model = null)
    {
        _window = window;
        _screens = screens;
        _share = share;
        _delays = delays;
        _sizeStore = sizeStore;
        _positionStore = positionStore;
        _temp = temp;
        _dragOut = dragOut ?? new DragOutPlanner(temp);
        Model = model ?? new ShelfModel();

        PreferredSize = sizeStore.Load() ?? ShelfLimits.DefaultSize;
        _rememberedTopLeft = positionStore.Load();
    }

    public ShelfModel Model { get; }

    /// <summary>
    /// The size the user last chose. The window may temporarily be smaller when the current screen cannot fit it,
    /// so this (not the window's frame) is what gets saved.
    /// </summary>
    public SizeDips PreferredSize { get; private set; }

    public bool IsShelfVisible => _window.IsVisible;

    public void Toggle()
    {
        if (_window.IsVisible) Hide();
        else Show(_screens.CursorPosition);
    }

    public void Clear() => Model.Clear();

    public void Hide()
    {
        CancelPendingHide();
        _window.Hide();
    }

    /// <summary>
    /// The shelf's close button: hide the window and forget everything on it. Items are only references to files, so the
    /// originals are never touched; only the temp copies this app made itself are deleted, except those that were just shared:
    /// a share target may still be reading them, so they are left for the next launch's cleanup.
    /// </summary>
    public void Close()
    {
        List<ShelfItem> discarded = Model.Items.Where(i => !_sharedItemIds.Contains(i.Id)).ToList();
        _sharedItemIds.Clear();
        Model.Clear();
        Hide();
        _temp.Discard(discarded);
        // The small files made so that links and text could be dragged out: a share target never sees them.
        _dragOut.CleanUp();
    }

    /// <summary>Opens the share sheet for <paramref name="items"/>. Files that no longer exist are left out; if nothing is left there is nothing to share.</summary>
    public void Share(IReadOnlyList<ShelfItem> items)
    {
        List<ShelfItem> shareable = items.Where(i => i.IsShareable).ToList();
        if (shareable.Count == 0) return;

        _share.Present(shareable, chosen =>
        {
            if (!chosen) return;
            foreach (ShelfItem item in shareable) _sharedItemIds.Add(item.Id);
        });
    }

    /// <summary>What to put on the drag-and-drop board when <paramref name="items"/> are dragged out.</summary>
    public DragOutPayload PlanDragOut(IReadOnlyList<ShelfItem> items) => _dragOut.Plan(items);

    /// <summary>
    /// Shows the shelf where the user last put it, or centred on <paramref name="near"/> if they never moved it
    /// (or that spot is no longer on any screen). Always kept fully inside the usable screen area.
    /// </summary>
    public void Show(PixelPoint near)
    {
        if (_window.IsVisible) return;
        CancelPendingHide();
        _window.ShowWithoutActivating(ShelfPlacement.Compute(PreferredSize, _rememberedTopLeft, near, _screens.Screens));
    }

    /// <summary>The user finished dragging the window somewhere. Remember that spot.</summary>
    public void UserMoved(PixelPoint topLeft)
    {
        _rememberedTopLeft = topLeft;
        if (!_settingsRetired) _positionStore.Save(topLeft);
    }

    /// <summary>The user finished resizing the window with its grip.</summary>
    public void UserResized(SizeDips size)
    {
        SizeDips minimum = ShelfLimits.MinimumSize;
        PreferredSize = new SizeDips(Math.Max(size.Width, minimum.Width), Math.Max(size.Height, minimum.Height));
        if (!_settingsRetired) _sizeStore.Save(PreferredSize);
    }

    /// <summary>
    /// Stops anything further from being written to the settings. Used when uninstalling, so the app does not write
    /// its settings back after deleting them.
    /// </summary>
    public void RetireSettings() => _settingsRetired = true;

    /// <summary>A drag began somewhere on the desktop.</summary>
    public void DragBegan() => CancelPendingHide();

    /// <summary>A drag ended. An empty shelf that was only opened for a drag that went elsewhere should not linger.</summary>
    public void DragEnded()
    {
        CancelPendingHide();
        // Give a drop onto the shelf time to land before judging whether it is empty.
        _pendingHide = _delays.Schedule(EmptyShelfGracePeriod, () =>
        {
            _pendingHide = null;
            if (Model.Items.Count == 0 && !Model.IsDropTargeted) _window.Hide();
        });
    }

    private void CancelPendingHide()
    {
        _pendingHide?.Dispose();
        _pendingHide = null;
    }
}
