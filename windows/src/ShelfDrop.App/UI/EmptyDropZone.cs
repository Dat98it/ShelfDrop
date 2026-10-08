using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ShelfDrop.App.UI
{
    /// <summary>
    /// What the shelf shows while it has nothing on it: a dashed outline that announces it is a place to drop things,
    /// which turns solid and takes the accent colour while a drag is over it.
    /// </summary>
    internal sealed class EmptyDropZone : Grid
    {
        private readonly Rectangle _outline;
        private readonly Border _circle;
        private readonly TextBlock _icon;
        private readonly TextBlock _title;
        private readonly TextBlock _tip;
        private bool _isTargeted;

        public EmptyDropZone()
        {
            _outline = new Rectangle { RadiusX = 14, RadiusY = 14, StrokeThickness = 1.2, StrokeDashArray = new DoubleCollection { 5, 4 } };

            _icon = Ui.Icon(Glyphs.Download, 18, Theme.Keys.TextSecondary);
            _circle = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(22),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = _icon,
            };
            _circle.SetResourceReference(Border.BackgroundProperty, Theme.Keys.Card);

            _title = Ui.Label("Drop anything here", 13, Theme.Keys.Text, FontWeights.SemiBold);
            _title.TextAlignment = TextAlignment.Center;
            TextBlock subtitle = Ui.Label("Files, images, links or text", 11, Theme.Keys.TextSecondary);
            subtitle.TextAlignment = TextAlignment.Center;
            subtitle.Margin = new Thickness(0, 2, 0, 0);

            _tip = Ui.Label("Tip: shake your mouse while dragging to open the shelf", 10, Theme.Keys.TextTertiary);
            _tip.TextAlignment = TextAlignment.Center;
            _tip.TextWrapping = TextWrapping.Wrap;
            _tip.Margin = new Thickness(0, 10, 0, 0);

            var column = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 290,
                Margin = new Thickness(10),
            };
            _title.Margin = new Thickness(0, 10, 0, 0);
            column.Children.Add(_circle);
            column.Children.Add(_title);
            column.Children.Add(subtitle);
            column.Children.Add(_tip);

            Children.Add(_outline);
            Children.Add(column);

            // Drop the tip first when the shelf is short.
            SizeChanged += (sender, args) => _tip.Visibility = args.NewSize.Height < 175 ? Visibility.Collapsed : Visibility.Visible;
            ApplyState();
        }

        public bool IsTargeted
        {
            get => _isTargeted;
            set
            {
                if (_isTargeted == value) return;
                _isTargeted = value;
                ApplyState();
            }
        }

        private void ApplyState()
        {
            _outline.SetResourceReference(Shape.StrokeProperty, _isTargeted ? Theme.Keys.Accent : Theme.Keys.ZoneStroke);
            _outline.StrokeThickness = _isTargeted ? 2 : 1.2;
            _outline.StrokeDashArray = _isTargeted ? new DoubleCollection() : new DoubleCollection { 5, 4 };
            _outline.SetResourceReference(Shape.FillProperty, _isTargeted ? Theme.Keys.AccentWash : Theme.Keys.Transparent);
            _icon.SetResourceReference(TextBlock.ForegroundProperty, _isTargeted ? Theme.Keys.Accent : Theme.Keys.TextSecondary);
            _title.Text = _isTargeted ? "Release to add" : "Drop anything here";
        }
    }
}
