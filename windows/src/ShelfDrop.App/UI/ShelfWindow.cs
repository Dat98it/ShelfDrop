using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using ShelfDrop.App.Native;
using ShelfDrop.App.Services;
using ShelfDrop.Core;

namespace ShelfDrop.App.UI
{
    /// <summary>
    /// The floating shelf. It is shown without being activated, so that opening it in the middle of a drag does not take focus from
    /// the program the user is dragging out of, and it hides from the taskbar and from Alt+Tab.
    /// </summary>
    internal sealed class ShelfWindow : Window, IShelfWindow
    {
        private readonly DragOutService _dragOut;
        private readonly Border _panel;
        private readonly Border _highlight;
        private readonly TextBlock _subtitle;
        private readonly DragHandle _dragAll;
        private readonly ShelfButton _shareAll;
        private readonly ShelfButton _clear;
        private readonly ShelfButton _close;
        private readonly ScrollViewer _scroll;
        private readonly AdaptiveGridPanel _grid;
        private readonly EmptyDropZone _emptyZone;
        private readonly ResizeGrip _grip;
        private readonly Grid _root;

        private ShelfController? _controller;
        private PayloadImporter? _importer;
        private PixelPoint _moveStartedAt;

        public ShelfWindow(DragOutService dragOut)
        {
            _dragOut = dragOut;

            Title = "ShelfDrop";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            AllowDrop = true;
            Width = ShelfLimits.DefaultSize.Width;
            Height = ShelfLimits.DefaultSize.Height;
            MinWidth = ShelfLimits.MinimumSize.Width;
            MinHeight = ShelfLimits.MinimumSize.Height;
            WindowStartupLocation = WindowStartupLocation.Manual;

            // The shadow is cast by a plain rounded rectangle behind the panel, so the soft-edge effect is not
            // applied to everything on the shelf each time something changes.
            var shadow = new Border { CornerRadius = new CornerRadius(20), Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 3, Direction = 270, Opacity = 0.4 } };
            shadow.SetResourceReference(Border.BackgroundProperty, Theme.Keys.Panel);

            _panel = new Border { CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1) };
            _panel.SetResourceReference(Border.BackgroundProperty, Theme.Keys.Panel);
            _panel.SetResourceReference(Border.BorderBrushProperty, Theme.Keys.PanelBorder);
            _panel.MouseLeftButtonDown += OnPanelMouseDown;

            _highlight = new Border
            {
                CornerRadius = new CornerRadius(20),
                BorderThickness = new Thickness(2),
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
            };
            _highlight.SetResourceReference(Border.BorderBrushProperty, Theme.Keys.Accent);
            _highlight.SetResourceReference(Border.BackgroundProperty, Theme.Keys.AccentWash);

            // Header
            TextBlock title = Ui.Label("Shelf", 14, Theme.Keys.Text, FontWeights.SemiBold);
            _subtitle = Ui.Label("Empty", 11, Theme.Keys.TextSecondary);
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(title);
            titles.Children.Add(_subtitle);

            _dragAll = new DragHandle();
            _dragAll.DragRequested += () => BeginDrag(_controller!.Model.Items.ToList());

            _shareAll = ShelfButton.WithIcon(Glyphs.Share);
            _shareAll.ToolTip = "Share all items…";
            _shareAll.Clicked += () => _controller?.Share(_controller.Model.Items.ToList());

            _clear = ShelfButton.WithText("Clear");
            _clear.ToolTip = "Remove every item from the shelf (your files are not touched)";
            _clear.Clicked += () => _controller?.Clear();

            _close = ShelfButton.WithIcon(Glyphs.Close);
            _close.ToolTip = "Close shelf and remove its items";
            _close.Clicked += () => _controller?.Close();

            var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            foreach (FrameworkElement control in new FrameworkElement[] { _dragAll, _shareAll, _clear, _close })
            {
                control.Margin = new Thickness(6, 0, 0, 0);
                controls.Children.Add(control);
            }

            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 0 });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(titles);
            header.Children.Add(controls);
            Grid.SetColumn(controls, 1);

            // Items, or the drop zone while there are none
            _grid = new AdaptiveGridPanel { Margin = new Thickness(0, 1, 0, 1) };
            _scroll = new ScrollViewer
            {
                Content = _grid,
                // Hidden on purpose, as on the Mac: scrolling still works with the wheel and a half-visible row hints at it.
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false,
                Background = Brushes.Transparent,
            };
            _emptyZone = new EmptyDropZone();

            var body = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            body.Children.Add(_scroll);
            body.Children.Add(_emptyZone);

            var inner = new Grid { Margin = new Thickness(14) };
            inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            inner.Children.Add(header);
            inner.Children.Add(body);
            Grid.SetRow(body, 1);
            _panel.Child = inner;

            _grip = new ResizeGrip { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 4, 4) };
            _grip.MouseLeftButtonDown += OnGripDown;
            _grip.MouseMove += OnGripMove;
            _grip.MouseLeftButtonUp += OnGripUp;

            _root = new Grid { Margin = new Thickness(ShelfLimits.ShadowMargin) };
            _root.Children.Add(shadow);
            _root.Children.Add(_panel);
            _root.Children.Add(_highlight);
            _root.Children.Add(_grip);
            Content = _root;

            Ui.AllowDropEverywhere(_root);
            UpdateChrome();
        }

        /// <summary>Connects the window to the controller, which needs the window to exist first.</summary>
        public void Attach(ShelfController controller, PayloadImporter importer)
        {
            _controller = controller;
            _importer = importer;
            ((INotifyCollectionChanged)controller.Model.Items).CollectionChanged += OnItemsChanged;
            controller.Model.PropertyChanged += OnModelPropertyChanged;
            foreach (ShelfItem item in controller.Model.Items) AddTile(item);
            UpdateChrome();
        }

        // IShelfWindow

        bool IShelfWindow.IsVisible => IsVisible;

        public PixelRect Frame
        {
            get
            {
                IntPtr handle = new WindowInteropHelper(this).Handle;
                return handle != IntPtr.Zero && NativeMethods.GetWindowRect(handle, out NativeMethods.RECT rect)
                    ? new PixelRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top)
                    : new PixelRect(0, 0, 0, 0);
            }
        }

        public IntPtr Handle => new WindowInteropHelper(this).EnsureHandle();

        // What automated tests look at.
        internal IReadOnlyList<ItemTile> Tiles => _grid.Children.OfType<ItemTile>().ToList();
        internal string SubtitleText => _subtitle.Text;
        internal bool IsDropZoneShown => _emptyZone.Visibility == Visibility.Visible;
        internal bool IsHighlightShown => _highlight.Visibility == Visibility.Visible;
        internal bool AreItemControlsShown => _clear.Visibility == Visibility.Visible && _dragAll.Visibility == Visibility.Visible;
        internal HoverButton CloseButton => _close;
        internal HoverButton ClearButton => _clear;
        internal HoverButton ShareAllButton => _shareAll;

        public void ShowWithoutActivating(PixelRect frame)
        {
            Theme.Apply();   // Windows may have switched between light and dark since the last time
            RefreshTiles();

            IntPtr handle = Handle;
            Place(handle, frame);
            if (!IsVisible) Show();   // ShowActivated is false, so this does not take focus
            // A different DPI at the destination can rescale the window as it appears: put it exactly where it was asked to be.
            Place(handle, frame);
            Log.Info("shelf shown at " + frame);
        }

        void IShelfWindow.Hide() => Hide();

        /// <summary>
        /// Shows and hides the window once, far off the screen, so the first real opening (in the middle of a shake, when speed
        /// matters) does not have to load WPF's drawing code and lay everything out from scratch.
        /// </summary>
        public void Prewarm()
        {
            IntPtr handle = Handle;
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            Place(handle, new PixelRect(-32000, -32000, (int)(Width * dpi.DpiScaleX), (int)(Height * dpi.DpiScaleY)));
            Show();
            UpdateLayout();
            Hide();
        }

        /// <summary>Draws the shelf to a PNG, for the screenshots that the automated checks keep.</summary>
        public void SaveScreenshot(string path)
        {
            UpdateLayout();
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            int width = (int)Math.Ceiling(_root.ActualWidth * dpi.DpiScaleX);
            int height = (int)Math.Ceiling(_root.ActualHeight * dpi.DpiScaleY);
            var bitmap = new RenderTargetBitmap(width, height, 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
            bitmap.Render(_root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }

        // Window plumbing

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr handle = new WindowInteropHelper(this).Handle;

            // Never the active window, and not a "real" window in the eyes of the taskbar and Alt+Tab.
            long style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE).ToInt64();
            NativeMethods.SetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE,
                new IntPtr(style | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW));

            HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (message)
            {
                case NativeMethods.WM_MOUSEACTIVATE:
                    // A click on the shelf works without making it the active window.
                    handled = true;
                    return new IntPtr(NativeMethods.MA_NOACTIVATE);

                case NativeMethods.WM_ENTERSIZEMOVE:
                    _moveStartedAt = Frame.TopLeft;
                    break;

                case NativeMethods.WM_EXITSIZEMOVE:
                    // The user let go of the window. Only a move they made counts, not one that this program made.
                    PixelPoint now = Frame.TopLeft;
                    if (now != _moveStartedAt) _controller?.UserMoved(now);
                    break;
            }
            return IntPtr.Zero;
        }

        private static void Place(IntPtr handle, PixelRect frame)
        {
            NativeMethods.SetWindowPos(handle, NativeMethods.HWND_TOPMOST, frame.Left, frame.Top, frame.Width, frame.Height,
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOOWNERZORDER);
        }

        // Moving and resizing

        private void OnPanelMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Handled || e.ChangedButton != MouseButton.Left) return;
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // The button was released before the move could start.
            }
        }

        private Point _gripStart;
        private Size _gripStartSize;

        private void OnGripDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _gripStart = CursorInDips();
            _gripStartSize = new Size(ActualWidth, ActualHeight);
            _grip.CaptureMouse();
        }

        private void OnGripMove(object sender, MouseEventArgs e)
        {
            if (!_grip.IsMouseCaptured) return;
            Point now = CursorInDips();
            Width = Math.Max(MinWidth, _gripStartSize.Width + now.X - _gripStart.X);
            Height = Math.Max(MinHeight, _gripStartSize.Height + now.Y - _gripStart.Y);
        }

        private void OnGripUp(object sender, MouseButtonEventArgs e)
        {
            if (!_grip.IsMouseCaptured) return;
            e.Handled = true;
            _grip.ReleaseMouseCapture();
            _controller?.UserResized(new SizeDips(ActualWidth, ActualHeight));
        }

        /// <summary>The pointer in device-independent units. Measured on the screen, not in the window, which moves as it is resized.</summary>
        private Point CursorInDips()
        {
            NativeMethods.GetCursorPos(out NativeMethods.POINT point);
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            return new Point(point.X / dpi.DpiScaleX, point.Y / dpi.DpiScaleY);
        }

        // Dragging out

        private void OnTileDragRequested(ItemTile tile)
        {
            BeginDrag(new List<ShelfItem> { tile.Item });
        }

        private void BeginDrag(IReadOnlyList<ShelfItem> items)
        {
            if (_controller == null || items.Count == 0) return;
            try
            {
                DragOutPayload payload = _controller.PlanDragOut(items);
                Log.Info("dragging out " + items.Count + " item(s)");
                _dragOut.Begin(this, payload);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is System.Runtime.InteropServices.COMException)
            {
                Log.Error("could not start the drag", e);
            }
        }

        // Dropping in

        protected override void OnDragEnter(DragEventArgs e)
        {
            base.OnDragEnter(e);
            // What a drag offers is the first thing to look at when something does not arrive, so it goes in the log.
            Log.Info("drag entered the shelf; formats: " + string.Join(", ", SafeFormats(e.Data)));
            AcceptOrRefuse(e);
        }

        private static string[] SafeFormats(System.Windows.IDataObject data)
        {
            try
            {
                return data.GetFormats();
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                return new[] { "(could not be read)" };
            }
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);
            AcceptOrRefuse(e);
        }

        protected override void OnDragLeave(DragEventArgs e)
        {
            base.OnDragLeave(e);
            // Crossing from one element to another inside the shelf also "leaves" one of them: only leaving the window counts.
            if (_controller != null && !CursorIsInsideWindow()) _controller.Model.IsDropTargeted = false;
        }

        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);
            if (_controller == null || _importer == null) return;
            _controller.Model.IsDropTargeted = false;
            Log.Info("drop on the shelf; formats: " + string.Join(", ", SafeFormats(e.Data)));
            if (_dragOut.IsDragging) return;

            try
            {
                IReadOnlyList<ShelfItem> items = _importer.Import(new WpfDropPayload(e.Data));
                _controller.Model.Add(items);
                e.Handled = true;
                Log.Info("dropped: " + items.Count + " item(s)");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Runtime.InteropServices.COMException || ex is InvalidOperationException)
            {
                Log.Error("the drop could not be read", ex);
            }
        }

        private void AcceptOrRefuse(DragEventArgs e)
        {
            // Our own items dragged back onto the shelf would only be added a second time.
            bool accept = _controller != null && !_dragOut.IsDragging && AcceptsAnything(e.Data);
            e.Effects = accept ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
            if (_controller != null) _controller.Model.IsDropTargeted = accept;
        }

        private static bool AcceptsAnything(System.Windows.IDataObject data) =>
            data.GetDataPresent(DataFormats.FileDrop)
            || data.GetDataPresent(WpfDropPayload.FileGroupDescriptorFormat)
            || data.GetDataPresent(WpfDropPayload.PngFormat)
            || data.GetDataPresent(DataFormats.Bitmap)
            || data.GetDataPresent(WpfDropPayload.UnicodeUrlFormat)
            || data.GetDataPresent(WpfDropPayload.AnsiUrlFormat)
            || data.GetDataPresent(DataFormats.UnicodeText);

        private bool CursorIsInsideWindow()
        {
            if (!NativeMethods.GetCursorPos(out NativeMethods.POINT point)) return false;
            return Frame.Contains(new PixelPoint(point.X, point.Y));
        }

        // Keeping the view in step with the model

        private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (ShelfItem item in e.NewItems!.Cast<ShelfItem>()) AddTile(item);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (ShelfItem item in e.OldItems!.Cast<ShelfItem>()) RemoveTile(item);
                    break;
                default:
                    _grid.Children.Clear();
                    foreach (ShelfItem item in _controller!.Model.Items) AddTile(item);
                    break;
            }
            UpdateChrome();
        }

        private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ShelfModel.IsDropTargeted)) UpdateChrome();
        }

        private void AddTile(ShelfItem item)
        {
            var tile = new ItemTile(item);
            tile.RemoveRequested += t => _controller?.Model.Remove(t.Item.Id);
            tile.ShareRequested += t => _controller?.Share(new List<ShelfItem> { t.Item });
            tile.DragRequested += OnTileDragRequested;
            Ui.AllowDropEverywhere(tile);
            _grid.Children.Add(tile);
        }

        private void RemoveTile(ShelfItem item)
        {
            ItemTile? tile = _grid.Children.OfType<ItemTile>().FirstOrDefault(t => ReferenceEquals(t.Item, item));
            if (tile != null) _grid.Children.Remove(tile);
        }

        private void RefreshTiles()
        {
            foreach (ItemTile tile in _grid.Children.OfType<ItemTile>()) tile.Refresh();
            UpdateChrome();
        }

        private void UpdateChrome()
        {
            IReadOnlyCollection<ShelfItem> items = _controller != null ? (IReadOnlyCollection<ShelfItem>)_controller.Model.Items : Array.Empty<ShelfItem>();
            bool hasItems = items.Count > 0;
            bool targeted = _controller?.Model.IsDropTargeted ?? false;

            _subtitle.Text = ShelfItem.Summary(items);
            _scroll.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
            _emptyZone.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
            _emptyZone.IsTargeted = targeted;
            _highlight.Visibility = targeted && hasItems ? Visibility.Visible : Visibility.Collapsed;

            // With nothing on the shelf there is nothing to drag, share or clear.
            Visibility controls = hasItems ? Visibility.Visible : Visibility.Collapsed;
            _dragAll.Visibility = controls;
            _shareAll.Visibility = controls;
            _clear.Visibility = controls;
            _shareAll.IsActive = items.Any(i => i.IsShareable);
        }
    }

    /// <summary>The "Drag all" pill. Not a button: grab it and drag to take every item out at once.</summary>
    internal sealed class DragHandle : Border
    {
        private Point _pressedAt;
        private bool _isPressed;

        public DragHandle()
        {
            Height = 24;
            CornerRadius = new CornerRadius(12);
            Padding = new Thickness(10, 0, 10, 0);
            Cursor = Cursors.Hand;
            ToolTip = "Drag every item out at once";
            SetResourceReference(BackgroundProperty, Theme.Keys.AccentWashStrong);

            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            TextBlock icon = Ui.Icon(Glyphs.Copy, 12, Theme.Keys.Accent);
            icon.Margin = new Thickness(0, 0, 6, 0);
            TextBlock label = Ui.Label("Drag all", 12, Theme.Keys.Accent, FontWeights.SemiBold);
            label.TextTrimming = TextTrimming.None;
            row.Children.Add(icon);
            row.Children.Add(label);
            Child = row;
        }

        public event Action? DragRequested;

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            e.Handled = true;   // not a press on the shelf's background, which would move the window
            _pressedAt = e.GetPosition(this);
            _isPressed = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            _isPressed = false;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _isPressed = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_isPressed || e.LeftButton != MouseButtonState.Pressed) return;
            if (!Ui.MovedFarEnoughToDrag(_pressedAt, e.GetPosition(this))) return;

            _isPressed = false;
            DragRequested?.Invoke();
        }
    }
}
