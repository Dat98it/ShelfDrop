using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShelfDrop.App.Native;
using ShelfDrop.App.UI;
using ShelfDrop.Core;
using ShelfDrop.Core.Tests;
using Xunit;

namespace ShelfDrop.App.Tests
{
    [Collection("wpf")]
    public class ShelfWindowTests
    {
        private readonly WpfFixture _wpf;

        public ShelfWindowTests(WpfFixture wpf) => _wpf = wpf;

        [Fact]
        public void ItIsNeverTheActiveWindowAndHasNoTaskbarButton() => _wpf.Run(() =>
        {
            using (var h = new ShelfHarness())
            {
                long style = NativeMethods.GetWindowLongPtr(h.Window.Handle, NativeMethods.GWL_EXSTYLE).ToInt64();

                Assert.NotEqual(0, style & NativeMethods.WS_EX_NOACTIVATE);
                Assert.NotEqual(0, style & NativeMethods.WS_EX_TOOLWINDOW);
            }
        });

        [Fact]
        public void ItAppearsWhereAskedWithoutTakingTheForeground() => _wpf.Run(() =>
        {
            using (var h = new ShelfHarness())
            {
                IntPtr before = NativeMethods.GetForegroundWindow();

                h.Window.ShowWithoutActivating(new PixelRect(220, 180, 420, 340));
                Pump.For(TimeSpan.FromMilliseconds(300));

                Assert.True(h.Window.IsVisible);
                PixelRect actual = h.Window.Frame;
                Assert.InRange(actual.Left, 218, 222);
                Assert.InRange(actual.Top, 178, 182);
                Assert.InRange(actual.Width, 418, 422);
                Assert.InRange(actual.Height, 338, 342);
                Assert.Equal(before, NativeMethods.GetForegroundWindow());
            }
        });

        [Fact]
        public void HidingWorksAndItCanComeBack() => _wpf.Run(() =>
        {
            using (var h = new ShelfHarness())
            {
                h.Controller.Show(new PixelPoint(600, 400));
                Assert.True(h.Controller.IsShelfVisible);

                h.Controller.Hide();
                Assert.False(h.Window.IsVisible);

                h.Controller.Show(new PixelPoint(600, 400));
                Assert.True(h.Window.IsVisible);
            }
        });

        [Fact]
        public void PrewarmingLeavesItHidden() => _wpf.Run(() =>
        {
            using (var h = new ShelfHarness())
            {
                h.Window.Prewarm();

                Assert.False(h.Window.IsVisible);
            }
        });

        [Fact]
        public void AnEmptyShelfShowsTheDropZoneAndNoItemControls() => _wpf.Run(() =>
        {
            using (var h = new ShelfHarness())
            {
                Assert.True(h.Window.IsDropZoneShown);
                Assert.False(h.Window.AreItemControlsShown);
                Assert.Equal("Empty", h.Window.SubtitleText);
            }
        });

        [Fact]
        public void ItemsReplaceTheDropZoneAndAreSummarised() => _wpf.Run(() =>
        {
            using (var h = new ShelfHarness())
            {
                h.Controller.Model.Add(new[] { ShelfItem.ForText("one"), ShelfItem.ForLink(new Uri("https://example.com")) });

                Assert.False(h.Window.IsDropZoneShown);
                Assert.True(h.Window.AreItemControlsShown);
                Assert.Equal(2, h.Window.Tiles.Count);
                Assert.Equal("2 items", h.Window.SubtitleText);
            }
        });

        [Fact]
        public void RemovingTheLastItemBringsTheDropZoneBack() => _wpf.Run(() =>
        {
            using (var h = new ShelfHarness())
            {
                var item = ShelfItem.ForText("one");
                h.Controller.Model.Add(new[] { item });

                h.Window.Tiles.Single().RemoveButton.PerformClick();

                Assert.Empty(h.Window.Tiles);
                Assert.True(h.Window.IsDropZoneShown);
            }
        });

        [Fact]
        public void ClearEmptiesTheShelfButLeavesItOpen() => _wpf.Run(() =>
        {
            using (var h = new ShelfHarness())
            {
                h.Controller.Show(new PixelPoint(600, 400));
                h.Controller.Model.Add(new[] { ShelfItem.ForText("one"), ShelfItem.ForText("two") });

                h.Window.ClearButton.PerformClick();

                Assert.Empty(h.Window.Tiles);
                Assert.True(h.Window.IsVisible);
            }
        });

        [Fact]
        public void CloseEmptiesTheShelfAndHidesIt() => _wpf.Run(() =>
        {
            using (var h = new ShelfHarness())
            {
                h.Controller.Show(new PixelPoint(600, 400));
                h.Controller.Model.Add(new[] { ShelfItem.ForText("one") });

                h.Window.CloseButton.PerformClick();

                Assert.Empty(h.Window.Tiles);
                Assert.False(h.Window.IsVisible);
            }
        });

        [Fact]
        public void ADragOverTheShelfHighlightsItOnlyWhenThereAreItems() => _wpf.Run(() =>
        {
            using (var h = new ShelfHarness())
            {
                h.Controller.Model.IsDropTargeted = true;
                Assert.False(h.Window.IsHighlightShown, "an empty shelf shows its drop zone instead");

                h.Controller.Model.Add(new[] { ShelfItem.ForText("one") });
                Assert.True(h.Window.IsHighlightShown);

                h.Controller.Model.IsDropTargeted = false;
                Assert.False(h.Window.IsHighlightShown);
            }
        });

        [Fact]
        public void AMissingFileIsMarkedAndCannotBeShared() => _wpf.Run(() =>
        {
            using (var h = new ShelfHarness())
            {
                string path = h.Scratch.File("gone.txt");
                h.Controller.Model.Add(new[] { ShelfItem.ForFile(path) });
                File.Delete(path);

                h.Controller.Show(new PixelPoint(600, 400));   // opening the shelf re-checks every file

                ItemTile tile = h.Window.Tiles.Single();
                Assert.Equal("File not found", tile.DetailText);
                Assert.False(tile.ShareButton.IsActive);
            }
        });

        [Fact]
        public void APhotoGetsItsThumbnailFromWindows() => _wpf.Run(() =>
        {
            using (var h = new ShelfHarness())
            {
                string photo = DemoContent.Create(h.Temp).First(i => i.IsImageFile).Path!;
                h.Controller.Model.Add(new[] { ShelfItem.ForFile(photo) });

                Pump.For(TimeSpan.FromSeconds(3));   // thumbnails load in the background and report back

                Assert.True(h.Window.Tiles.Single().HasPicture);
            }
        });

        [Fact]
        public void ThePanelCanBeDrawnAtAnySize() => _wpf.Run(() =>
        {
            using (var h = new ShelfHarness())
            {
                h.Window.ShowWithoutActivating(new PixelRect(60, 60, (int)ShelfLimits.DefaultSize.Width, (int)ShelfLimits.DefaultSize.Height));
                Pump.For(TimeSpan.FromMilliseconds(300));

                BitmapSource once = h.Window.RenderPanel(1);
                BitmapSource triple = h.Window.RenderPanel(3);

                Assert.Equal(360, once.PixelWidth);
                Assert.Equal(300, once.PixelHeight);
                Assert.Equal(1080, triple.PixelWidth);
                Assert.Equal(900, triple.PixelHeight);
            }
        });

        [Fact]
        public void ThePanelPictureStartsAtThePanelsOwnEdge() => _wpf.Run(() =>
        {
            using (var h = new ShelfHarness())
            {
                Theme.Apply(dark: false);
                h.Window.ShowWithoutActivating(new PixelRect(60, 60, (int)ShelfLimits.DefaultSize.Width, (int)ShelfLimits.DefaultSize.Height));
                Pump.For(TimeSpan.FromMilliseconds(300));

                BitmapSource picture = h.Window.RenderPanel(2);

                // Halfway down, 3 pixels in from each edge, is panel (nearly opaque). Shifted by the window's margin it would be
                // empty on the left and the picture would be cut off on the right.
                Assert.True(AlphaAt(picture, 3, picture.PixelHeight / 2) > 200, "left edge is empty");
                Assert.True(AlphaAt(picture, picture.PixelWidth - 4, picture.PixelHeight / 2) > 200, "right edge is cut off");
                Assert.True(AlphaAt(picture, picture.PixelWidth / 2, picture.PixelHeight - 4) > 200, "bottom edge is cut off");
            }
        });

        [Fact]
        public void ALongNameIsCutInTheMiddleSoTheFileTypeStillShows() => _wpf.Run(() =>
        {
            using (var h = new ShelfHarness())
            {
                string name = "Holiday photos from the long weekend by the lake.png";
                h.Controller.Model.Add(new[] { ShelfItem.ForFile(h.Scratch.File(name)) });
                h.Window.ShowWithoutActivating(new PixelRect(60, 60, (int)ShelfLimits.DefaultSize.Width, (int)ShelfLimits.DefaultSize.Height));
                Pump.For(TimeSpan.FromMilliseconds(600));

                string shown = h.Window.Tiles.Single().TitleText;

                Assert.Contains("…", shown);
                Assert.EndsWith(".png", shown);
                Assert.StartsWith("Holiday", shown);
                Assert.True(shown.Length < name.Length);
            }
        });

        [Fact]
        public void AShortNameIsLeftWhole() => _wpf.Run(() =>
        {
            using (var h = new ShelfHarness())
            {
                h.Controller.Model.Add(new[] { ShelfItem.ForFile(h.Scratch.File("notes.txt")) });
                h.Window.ShowWithoutActivating(new PixelRect(60, 60, (int)ShelfLimits.DefaultSize.Width, (int)ShelfLimits.DefaultSize.Height));
                Pump.For(TimeSpan.FromMilliseconds(400));

                Assert.Equal("notes.txt", h.Window.Tiles.Single().TitleText);
            }
        });

        private static int AlphaAt(BitmapSource picture, int x, int y)
        {
            var pixel = new byte[4];
            new FormatConvertedBitmap(new CroppedBitmap(picture, new System.Windows.Int32Rect(x, y, 1, 1)), PixelFormats.Bgra32, null, 0).CopyPixels(pixel, 4, 0);
            return pixel[3];
        }

        // Pictures. Kept as artifacts of the automated build, where they are the only way to see what the window looks like.

        [Fact]
        public void ThePicturesOfTheShelfAreDrawn() => _wpf.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            {
                string theme = dark ? "dark" : "light";
                using (var h = new ShelfHarness())
                {
                    Theme.Apply(dark);
                    h.Window.ShowWithoutActivating(new PixelRect(60, 60, 384, 324));
                    Pump.For(TimeSpan.FromMilliseconds(500));
                    Theme.Apply(dark);   // showing re-reads the system setting; this test decides

                    Assert.True(Picture(h, $"shelf-empty-{theme}.png") > 5, "even the empty shelf has a panel, a border and a drop zone");

                    h.Controller.Model.IsDropTargeted = true;
                    Pump.For(TimeSpan.FromMilliseconds(200));
                    Picture(h, $"shelf-empty-targeted-{theme}.png");
                    h.Controller.Model.IsDropTargeted = false;

                    h.Controller.Model.Add(DemoContent.Create(h.Temp));
                    Pump.For(TimeSpan.FromSeconds(3));
                    Theme.Apply(dark);
                    Assert.True(Picture(h, $"shelf-items-{theme}.png") > 40, "the shelf with items should show many colours");

                    h.Controller.Model.IsDropTargeted = true;
                    Pump.For(TimeSpan.FromMilliseconds(200));
                    Picture(h, $"shelf-items-targeted-{theme}.png");
                    h.Controller.Model.IsDropTargeted = false;
                }
            }
        });

        [Fact]
        public void ThePicturesOfTheShelfAtItsSmallestAndWidestAreDrawn() => _wpf.Run(() =>
        {
            using (var h = new ShelfHarness())
            {
                Theme.Apply(dark: false);
                h.Controller.Model.Add(DemoContent.Create(h.Temp));

                h.Window.ShowWithoutActivating(new PixelRect(60, 60, (int)ShelfLimits.MinimumSize.Width, (int)ShelfLimits.MinimumSize.Height));
                Pump.For(TimeSpan.FromSeconds(3));
                Picture(h, "shelf-smallest.png");

                h.Window.ShowWithoutActivating(new PixelRect(60, 60, 760, 520));
                Pump.For(TimeSpan.FromMilliseconds(800));
                Picture(h, "shelf-widest.png");
            }
        });

        /// <summary>Saves the shelf as a picture and returns how many distinct colours it holds (a blank picture holds one).</summary>
        private static int Picture(ShelfHarness h, string name)
        {
            string path = Artifacts.PathFor(name);
            h.Window.SaveScreenshot(path);

            BitmapSource bitmap = new PngBitmapDecoder(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Assert.True(bitmap.PixelWidth > 200 && bitmap.PixelHeight > 150, $"{name} is only {bitmap.PixelWidth} x {bitmap.PixelHeight}");

            BitmapSource converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4];
            converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
            var colours = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < pixels.Length; i += 4) colours.Add(pixels[i] | (pixels[i + 1] << 8) | (pixels[i + 2] << 16) | (pixels[i + 3] << 24));
            return colours.Count;
        }
    }
}
