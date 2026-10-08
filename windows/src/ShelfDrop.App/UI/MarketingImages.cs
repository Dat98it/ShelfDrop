using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using ShelfDrop.Core;

namespace ShelfDrop.App.UI
{
    /// <summary>
    /// Draws the pictures for the website and for posts (<c>ShelfDrop.exe --marketing &lt;folder&gt;</c>): the real shelf, drawn again at a
    /// large size so it is sharp, on a backdrop in the style of a Windows 11 wallpaper, light and dark. Nothing here is a mock-up:
    /// the panel is the same window the app shows.
    /// </summary>
    internal static class MarketingImages
    {
        private const int CanvasWidth = 2400;
        private const int CanvasHeight = 1500;

        public static async Task RenderAsync(ShelfWindow window, ShelfController controller, TempStorage temp, string directory)
        {
            Directory.CreateDirectory(directory);
            Directory.CreateDirectory(Path.Combine(directory, "transparent"));

            try
            {
                foreach (bool dark in new[] { true, false })
                {
                    Theme.Forced = dark;
                    await RenderThemeAsync(window, controller, temp, directory, dark);
                }
            }
            finally
            {
                Theme.Forced = null;
            }
        }

        private static async Task RenderThemeAsync(ShelfWindow window, ShelfController controller, TempStorage temp, string directory, bool dark)
        {
            string theme = dark ? "dark" : "light";
            ShelfModel model = controller.Model;
            DpiScale dpi = VisualTreeHelper.GetDpi(window);

            // The default shelf, with six things on it.
            model.Clear();
            model.IsDropTargeted = false;
            await Show(window, ShelfLimits.DefaultSize, dpi);
            model.Add(DemoContent.CreateForPictures(temp, 6));
            await Settle(window, thumbnails: true);

            Save(directory, dark ? "01-hero-dark.png" : "02-hero-light.png", Compose(window.RenderPanel(2.4), 2.4, CanvasWidth, CanvasHeight, dark, backdrop: true));
            Save(directory, dark ? "03-closeup-dark.png" : "04-closeup-light.png", Compose(window.RenderPanel(3.6), 3.6, 1800, 1350, dark, backdrop: true));
            Save(Path.Combine(directory, "transparent"), $"panel-items-{theme}.png", Compose(window.RenderPanel(3), 3, (int)((ShelfLimits.DefaultSize.Width + 140) * 3), (int)((ShelfLimits.DefaultSize.Height + 140) * 3), dark, backdrop: false));

            // The same, with the pointer on the first thing: its share and remove buttons show.
            ItemTile first = window.Tiles[0];
            first.SetHot(true);
            await Settle(window, thumbnails: false);
            Save(directory, dark ? "05-hover-actions-dark.png" : "06-hover-actions-light.png", Compose(window.RenderPanel(2.4), 2.4, CanvasWidth, CanvasHeight, dark, backdrop: true));
            first.SetHot(false);

            // A drag is over the shelf while it already holds things: the whole panel takes the accent colour.
            model.IsDropTargeted = true;
            await Settle(window, thumbnails: false);
            Save(Path.Combine(directory, "transparent"), $"panel-drop-target-{theme}.png", Compose(window.RenderPanel(3), 3, (int)((ShelfLimits.DefaultSize.Width + 140) * 3), (int)((ShelfLimits.DefaultSize.Height + 140) * 3), dark, backdrop: false));
            model.IsDropTargeted = false;

            // Nothing on the shelf, and a drag arriving: "Release to add".
            model.Clear();
            model.IsDropTargeted = true;
            await Settle(window, thumbnails: false);
            Save(directory, dark ? "07-release-to-add-dark.png" : "08-release-to-add-light.png", Compose(window.RenderPanel(2.4), 2.4, CanvasWidth, CanvasHeight, dark, backdrop: true));

            // Nothing on the shelf at rest.
            model.IsDropTargeted = false;
            await Settle(window, thumbnails: false);
            Save(directory, dark ? "09-empty-state-dark.png" : "10-empty-state-light.png", Compose(window.RenderPanel(2.4), 2.4, CanvasWidth, CanvasHeight, dark, backdrop: true));

            // A wider shelf, with nine things on it.
            await Show(window, new SizeDips(560, 420), dpi);
            model.Add(DemoContent.CreateForPictures(temp, 9));
            await Settle(window, thumbnails: true);
            Save(directory, dark ? "11-wide-dark.png" : "12-wide-light.png", Compose(window.RenderPanel(2), 2, CanvasWidth, CanvasHeight, dark, backdrop: true));

            model.Clear();
        }

        private static async Task Show(ShelfWindow window, SizeDips size, DpiScale dpi)
        {
            window.ShowWithoutActivating(new PixelRect(40, 40, (int)Math.Round(size.Width * dpi.DpiScaleX), (int)Math.Round(size.Height * dpi.DpiScaleY)));
            await Settle(window, thumbnails: false);
        }

        /// <summary>Gives layout, rendering and the thumbnails (which load in the background and report back) time to finish.</summary>
        private static async Task Settle(ShelfWindow window, bool thumbnails)
        {
            await Task.Delay(thumbnails ? 3000 : 400);
            window.UpdateLayout();
        }

        private static void Save(string directory, string name, BitmapSource picture)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(picture));
            using (FileStream stream = File.Create(Path.Combine(directory, name))) encoder.Save(stream);
            Log.Info($"picture {name}: {picture.PixelWidth} x {picture.PixelHeight}");
        }

        /// <summary>
        /// Puts the panel (drawn at <paramref name="scale"/>) in the middle of a canvas, with the soft shadow a floating window has
        /// and, unless asked for a transparent picture, on a backdrop. Everything is placed on whole pixels so nothing is blurred.
        /// </summary>
        private static BitmapSource Compose(BitmapSource panel, double scale, int width, int height, bool dark, bool backdrop)
        {
            double canvasWidth = width / scale;
            double canvasHeight = height / scale;
            double panelWidth = panel.PixelWidth / scale;
            double panelHeight = panel.PixelHeight / scale;
            double left = Math.Floor((width - panel.PixelWidth) / 2.0) / scale;
            double top = Math.Floor((height - panel.PixelHeight) / 2.0) / scale;

            var canvas = new Canvas { Width = canvasWidth, Height = canvasHeight, Background = Brushes.Transparent };
            if (backdrop) canvas.Children.Add(Backdrop(dark, canvasWidth, canvasHeight));

            // In the app the window's own shadow sits under the panel, so the panel's colour is under it twice. Same here.
            var caster = new Border
            {
                Width = panelWidth,
                Height = panelHeight,
                CornerRadius = new CornerRadius(20),
                Background = (Brush)Application.Current.Resources[Theme.Keys.Panel],
                Effect = new DropShadowEffect { BlurRadius = 56, ShadowDepth = 18, Direction = 270, Opacity = dark ? 0.55 : 0.30, Color = Colors.Black },
            };
            Canvas.SetLeft(caster, left);
            Canvas.SetTop(caster, top);
            canvas.Children.Add(caster);

            var image = new Image { Source = panel, Width = panelWidth, Height = panelHeight, Stretch = Stretch.Fill };
            Canvas.SetLeft(image, left);
            Canvas.SetTop(image, top);
            canvas.Children.Add(image);

            canvas.Measure(new Size(canvasWidth, canvasHeight));
            canvas.Arrange(new Rect(0, 0, canvasWidth, canvasHeight));
            canvas.UpdateLayout();

            var target = new RenderTargetBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            target.Render(canvas);
            target.Freeze();
            return target;
        }

        /// <summary>A soft, colourful backdrop in the spirit of a Windows 11 wallpaper: a few glowing blooms on a dark or light ground.</summary>
        private static FrameworkElement Backdrop(bool dark, double width, double height)
        {
            var layers = new Grid { Width = width, Height = height };

            layers.Children.Add(Fill(dark ? Gradient(0x0A1022, 0x161E42) : Gradient(0xE8F0FB, 0xF4EEFB)));

            if (dark)
            {
                layers.Children.Add(Fill(Bloom(0x6C47FF, 0.60, 0.16, 0.18, 0.60)));
                layers.Children.Add(Fill(Bloom(0x1E88FF, 0.50, 0.86, 0.22, 0.55)));
                layers.Children.Add(Fill(Bloom(0x00C2A8, 0.30, 0.62, 1.00, 0.60)));
                layers.Children.Add(Fill(Bloom(0xFF5FA2, 0.26, 0.04, 0.95, 0.50)));
            }
            else
            {
                layers.Children.Add(Fill(Bloom(0x7AA8FF, 0.65, 0.14, 0.20, 0.60)));
                layers.Children.Add(Fill(Bloom(0xC6A4FF, 0.55, 0.88, 0.16, 0.55)));
                layers.Children.Add(Fill(Bloom(0x7BDDE8, 0.50, 0.68, 1.00, 0.60)));
                layers.Children.Add(Fill(Bloom(0xFFC4A8, 0.55, 0.04, 0.98, 0.50)));
            }
            return layers;
        }

        private static Brush Gradient(int top, int bottom) =>
            new LinearGradientBrush(Rgb(top), Rgb(bottom), 90);

        /// <summary>A circle of colour that fades out towards its edge.</summary>
        private static Brush Bloom(int rgb, double strength, double centreX, double centreY, double radius)
        {
            Color colour = Rgb(rgb);
            return new RadialGradientBrush(
                Color.FromArgb((byte)(255 * strength), colour.R, colour.G, colour.B),
                Color.FromArgb(0, colour.R, colour.G, colour.B))
            {
                Center = new Point(centreX, centreY),
                GradientOrigin = new Point(centreX, centreY),
                RadiusX = radius,
                RadiusY = radius * 1.4,
            };
        }

        private static Color Rgb(int rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

        /// <summary>A layer that fills whatever it is put in, with the given fill.</summary>
        private static UIElement Fill(Brush fill) => new Border { Background = fill };
    }
}
