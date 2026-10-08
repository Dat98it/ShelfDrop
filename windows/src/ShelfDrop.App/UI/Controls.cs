using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ShelfDrop.App.UI
{
    internal static class Glyphs
    {
        // Segoe MDL2 Assets / Segoe Fluent Icons code points.
        public const string Share = "\uE72D";
        public const string Close = "\uE711";
        public const string Link = "\uE71B";
        public const string Text = "\uE8E4";
        public const string Download = "\uE896";
        public const string Copy = "\uE8C8";
        public const string Document = "\uE8A5";
    }

    internal static class Ui
    {
        // Windows 11 has the Fluent icon font, Windows 10 the older MDL2 one with the same code points.
        public static readonly FontFamily IconFont = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");

        public static TextBlock Label(string text, double size, string foreground, FontWeight? weight = null)
        {
            var label = new TextBlock
            {
                Text = text,
                FontSize = size,
                FontWeight = weight ?? FontWeights.Normal,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, foreground);
            return label;
        }

        public static TextBlock Icon(string glyph, double size, string foreground)
        {
            var icon = new TextBlock
            {
                Text = glyph,
                FontFamily = IconFont,
                FontSize = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            icon.SetResourceReference(TextBlock.ForegroundProperty, foreground);
            return icon;
        }

        /// <summary>
        /// Lets every element of a tree accept drops. Whether a drop lands on an element depends on it being switched on there,
        /// so it is simplest to switch it on everywhere and decide what to do with a drop in one place, the window.
        /// </summary>
        public static void AllowDropEverywhere(DependencyObject root)
        {
            if (root is UIElement element) element.AllowDrop = true;
            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is DependencyObject node) AllowDropEverywhere(node);
            }
        }

        public static bool MovedFarEnoughToDrag(Point from, Point to) =>
            Math.Abs(to.X - from.X) > SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(to.Y - from.Y) > SystemParameters.MinimumVerticalDragDistance;
    }

    /// <summary>
    /// A flat button that lightens when the pointer is over it and darkens while pressed. It handles every mouse press itself,
    /// including when it is disabled, so a click on it never falls through and moves the window.
    /// </summary>
    internal class HoverButton : Border
    {
        private readonly string _normal;
        private readonly string _hot;
        private readonly string _pressed;
        private bool _isHot;
        private bool _isPressed;
        private bool _isActive = true;

        public HoverButton(string normal, string hot, string pressed)
        {
            _normal = normal;
            _hot = hot;
            _pressed = pressed;
            Cursor = Cursors.Hand;
            Refresh();
        }

        public event Action? Clicked;

        /// <summary>False greys the button out and stops it from clicking.</summary>
        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                Opacity = value ? 1 : 0.4;
                Cursor = value ? Cursors.Hand : Cursors.Arrow;
                Refresh();
            }
        }

        /// <summary>Presses the button as a click would, for automated tests that have no mouse.</summary>
        internal void PerformClick()
        {
            if (_isActive) Clicked?.Invoke();
        }

        protected virtual void OnStateChanged(bool hot)
        {
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            _isHot = true;
            Refresh();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _isHot = false;
            _isPressed = false;
            Refresh();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            e.Handled = true;
            if (!_isActive) return;
            _isPressed = true;
            CaptureMouse();
            Refresh();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            e.Handled = true;
            if (!_isPressed) return;
            _isPressed = false;
            ReleaseMouseCapture();
            Refresh();
            if (_isActive && IsMouseOver) Clicked?.Invoke();
        }

        private void Refresh()
        {
            SetResourceReference(BackgroundProperty, _isActive && _isPressed ? _pressed : _isActive && _isHot ? _hot : _normal);
            OnStateChanged(_isActive && _isHot);
        }
    }

    /// <summary>The small rounded button of the shelf's header: an icon or a short word.</summary>
    internal sealed class ShelfButton : HoverButton
    {
        public const double Height24 = 24;

        private readonly TextBlock _content;

        public ShelfButton(TextBlock content, double width)
            : base(Theme.Keys.Control, Theme.Keys.ControlHover, Theme.Keys.ControlPressed)
        {
            _content = content;
            Height = Height24;
            if (width > 0) Width = width;
            CornerRadius = new CornerRadius(7);
            Child = content;
            content.HorizontalAlignment = HorizontalAlignment.Center;
            content.VerticalAlignment = VerticalAlignment.Center;
            OnStateChanged(false);
        }

        public static ShelfButton WithIcon(string glyph) => new ShelfButton(Ui.Icon(glyph, 12, Theme.Keys.TextSecondary), Height24);

        public static ShelfButton WithText(string text)
        {
            TextBlock label = Ui.Label(text, 12, Theme.Keys.TextSecondary, FontWeights.SemiBold);
            label.TextTrimming = TextTrimming.None;
            return new ShelfButton(label, 0) { Padding = new Thickness(9, 0, 9, 0) };
        }

        protected override void OnStateChanged(bool hot)
        {
            base.OnStateChanged(hot);
            _content?.SetResourceReference(TextBlock.ForegroundProperty, hot ? Theme.Keys.Text : Theme.Keys.TextSecondary);
        }
    }

    /// <summary>The small round button in the corner of a tile (share, remove).</summary>
    internal sealed class CircleButton : HoverButton
    {
        public CircleButton(string glyph, string toolTip)
            : base(Theme.Keys.Circle, Theme.Keys.ControlHover, Theme.Keys.ControlPressed)
        {
            Width = 20;
            Height = 20;
            CornerRadius = new CornerRadius(10);
            BorderThickness = new Thickness(1);
            SetResourceReference(BorderBrushProperty, Theme.Keys.CardBorderHover);
            Child = Ui.Icon(glyph, 9, Theme.Keys.Text);
            ToolTip = toolTip;
        }
    }

    /// <summary>The grip in the bottom-right corner: drag it to resize the shelf.</summary>
    internal sealed class ResizeGrip : FrameworkElement
    {
        public ResizeGrip()
        {
            Cursor = Cursors.SizeNWSE;
            Width = 18;
            Height = 18;
            ToolTip = "Drag to resize";
            Theme.Changed += InvalidateVisual;
            Unloaded += (sender, args) => Theme.Changed -= InvalidateVisual;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            // A transparent fill gives the whole square to the mouse; the three lines are what is seen.
            drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
            var brush = TryFindResource(Theme.Keys.TextTertiary) as Brush ?? Brushes.Gray;
            var pen = new Pen(brush, 1.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            double w = ActualWidth;
            double h = ActualHeight;
            for (int i = 0; i < 3; i++)
            {
                double inset = 4 + i * 4;
                drawingContext.DrawLine(pen, new Point(w - 2, h - inset - 2), new Point(w - inset - 2, h - 2));
            }
        }
    }
}
