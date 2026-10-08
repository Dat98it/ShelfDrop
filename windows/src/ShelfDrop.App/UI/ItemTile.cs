using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using ShelfDrop.App.Services;
using ShelfDrop.Core;

namespace ShelfDrop.App.UI
{
    /// <summary>One thing on the shelf: a picture, its name and what it is, with share and remove buttons that appear under the pointer.</summary>
    internal sealed class ItemTile : Border
    {
        private readonly Grid _thumbnailArea;
        private readonly Image _image = new Image();
        private readonly TextBlock _detail;
        private readonly CircleButton _share;
        private readonly CircleButton _remove;
        private readonly Border? _imageFrame;
        private Point _pressedAt;
        private bool _isPressed;
        private bool _isHot;

        public ItemTile(ShelfItem item)
        {
            Item = item;

            CornerRadius = new CornerRadius(12);
            BorderThickness = new Thickness(1);
            Padding = new Thickness(6, 8, 6, 8);
            Background = Brushes.Transparent;
            ContextMenu = BuildContextMenu();
            ToolTip = ToolTipFor(item);

            _thumbnailArea = new Grid { Height = 48 };
            if (item.Kind == ShelfItemKind.File)
            {
                _image.Stretch = Stretch.Uniform;
                _image.MaxWidth = item.IsImageFile ? 76 : 48;
                _image.MaxHeight = 48;
                _image.HorizontalAlignment = HorizontalAlignment.Center;
                RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.HighQuality);

                if (item.IsImageFile)
                {
                    _imageFrame = new Border
                    {
                        CornerRadius = new CornerRadius(4),
                        BorderThickness = new Thickness(1),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = _image,
                        Effect = new DropShadowEffect { BlurRadius = 3, ShadowDepth = 1, Direction = 270, Opacity = 0.3 },
                    };
                    _imageFrame.SetResourceReference(BorderBrushProperty, Theme.Keys.CardBorderHover);
                    _thumbnailArea.Children.Add(_imageFrame);
                    _imageFrame.Visibility = Visibility.Hidden;   // until the picture is there: an empty frame looks broken
                }
                else
                {
                    _thumbnailArea.Children.Add(_image);
                }
            }
            else
            {
                _thumbnailArea.Children.Add(GlyphTile(item.Kind));
            }

            TextBlock title = Ui.Label(item.Title, 11, Theme.Keys.Text, FontWeights.Medium);
            title.TextAlignment = TextAlignment.Center;
            title.Margin = new Thickness(0, 6, 0, 0);

            _detail = Ui.Label(item.Detail(), 10, Theme.Keys.TextSecondary);
            _detail.TextAlignment = TextAlignment.Center;
            _detail.Margin = new Thickness(0, 1, 0, 0);

            var column = new StackPanel();
            column.Children.Add(_thumbnailArea);
            column.Children.Add(title);
            column.Children.Add(_detail);

            _share = new CircleButton(Glyphs.Share, "Share…")
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(-2, -4, 0, 0),
                Visibility = Visibility.Hidden,
            };
            _share.Clicked += () => ShareRequested?.Invoke(this);

            _remove = new CircleButton(Glyphs.Close, "Remove from shelf")
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -4, -2, 0),
                Visibility = Visibility.Hidden,
            };
            _remove.Clicked += () => RemoveRequested?.Invoke(this);

            var content = new Grid();
            content.Children.Add(column);
            content.Children.Add(_share);
            content.Children.Add(_remove);
            Child = content;

            ApplyColors();
            Refresh();
            if (item.Kind == ShelfItemKind.File && item.Path != null) _ = LoadThumbnailAsync(item.Path);
        }

        public ShelfItem Item { get; }

        internal HoverButton ShareButton => _share;
        internal HoverButton RemoveButton => _remove;
        internal string DetailText => _detail.Text;
        internal bool HasPicture => _image.Source != null;

        public event Action<ItemTile>? RemoveRequested;
        public event Action<ItemTile>? ShareRequested;

        /// <summary>The user started dragging this tile away from the shelf.</summary>
        public event Action<ItemTile>? DragRequested;

        /// <summary>Re-checks whether the file is still there. Called when the pointer arrives and when the shelf opens.</summary>
        public void Refresh()
        {
            bool missing = Item.IsMissing;
            _detail.Text = missing ? "File not found" : Item.Detail();
            _detail.SetResourceReference(TextBlock.ForegroundProperty, missing ? Theme.Keys.Warning : Theme.Keys.TextSecondary);
            _thumbnailArea.Opacity = missing ? 0.35 : 1;
            _share.IsActive = !missing;
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            _isHot = true;
            _share.Visibility = Visibility.Visible;
            _remove.Visibility = Visibility.Visible;
            Refresh();
            ApplyColors();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _isHot = false;
            _isPressed = false;
            _share.Visibility = Visibility.Hidden;
            _remove.Visibility = Visibility.Hidden;
            ApplyColors();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            // Handled: a press on a tile must not be taken for a press on the shelf's background, which moves the window.
            e.Handled = true;
            _pressedAt = e.GetPosition(this);
            _isPressed = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            _isPressed = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_isPressed || e.LeftButton != MouseButtonState.Pressed) return;
            if (!Ui.MovedFarEnoughToDrag(_pressedAt, e.GetPosition(this))) return;

            _isPressed = false;
            DragRequested?.Invoke(this);
        }

        public void SetThumbnail(ImageSource? picture)
        {
            if (picture == null) return;
            _image.Source = picture;
            if (_imageFrame != null) _imageFrame.Visibility = Visibility.Visible;
        }

        private async Task LoadThumbnailAsync(string path)
        {
            // Twice the on-screen size, so it stays sharp on a high-DPI screen.
            ImageSource? picture = await ThumbnailService.LoadAsync(path, 128);
            SetThumbnail(picture);
        }

        private void ApplyColors()
        {
            SetResourceReference(BackgroundProperty, _isHot ? Theme.Keys.CardHover : Theme.Keys.Card);
            SetResourceReference(BorderBrushProperty, _isHot ? Theme.Keys.CardBorderHover : Theme.Keys.CardBorder);
        }

        private ContextMenu BuildContextMenu()
        {
            var share = new MenuItem { Header = "Share…" };
            share.Click += (sender, args) => ShareRequested?.Invoke(this);
            var remove = new MenuItem { Header = "Remove from Shelf" };
            remove.Click += (sender, args) => RemoveRequested?.Invoke(this);

            var menu = new ContextMenu();
            menu.Items.Add(share);
            menu.Items.Add(remove);
            // Re-check the file each time the menu opens, so "Share…" is greyed out for a file that has gone.
            menu.Opened += (sender, args) => share.IsEnabled = !Item.IsMissing;
            return menu;
        }

        private static string ToolTipFor(ShelfItem item)
        {
            switch (item.Kind)
            {
                case ShelfItemKind.File: return item.Path ?? item.Title;
                case ShelfItemKind.Link: return item.Url?.OriginalString ?? item.Title;
                default:
                    string text = item.Text ?? item.Title;
                    return text.Length > 300 ? text.Substring(0, 300) + "…" : text;
            }
        }

        /// <summary>A rounded, tinted square with a white symbol: the picture for items that have none of their own.</summary>
        private static FrameworkElement GlyphTile(ShelfItemKind kind)
        {
            bool isLink = kind == ShelfItemKind.Link;
            Color from = isLink ? Color.FromRgb(0x5C, 0xAB, 0xFF) : Color.FromRgb(0xFF, 0xB8, 0x4D);
            Color to = isLink ? Color.FromRgb(0x1F, 0x6B, 0xFA) : Color.FromRgb(0xF7, 0x80, 0x1F);

            var tile = new Border
            {
                Width = 46,
                Height = 46,
                CornerRadius = new CornerRadius(11),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new LinearGradientBrush(from, to, 90),
                Child = new TextBlock
                {
                    Text = isLink ? Glyphs.Link : Glyphs.Text,
                    FontFamily = Ui.IconFont,
                    FontSize = 20,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                Effect = new DropShadowEffect { BlurRadius = 5, ShadowDepth = 1, Direction = 270, Opacity = 0.3, Color = to },
            };
            return tile;
        }
    }
}
