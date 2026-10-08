using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ShelfDrop.App.Native;
using ShelfDrop.App.Services;
using ShelfDrop.App.UI;
using ShelfDrop.Core;
using ShelfDrop.Core.Tests;
using Xunit;

namespace ShelfDrop.App.Tests
{
    public class RegistryLoginItemServiceTests : IDisposable
    {
        // A private corner of the registry, so that no test can switch the real ShelfDrop on or off at login.
        private readonly string _root = @"Software\ShelfDropTests\" + Guid.NewGuid();
        private const string Exe = @"C:\Tools\ShelfDrop\ShelfDrop.exe";

        private string RunKey => _root + @"\Run";
        private string ApprovedKey => _root + @"\Approved";

        private RegistryLoginItemService Make(string exe = Exe) => new RegistryLoginItemService(exe, RunKey, ApprovedKey, "ShelfDrop");

        public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(_root, throwOnMissingSubKey: false);

        [Fact]
        public void StartsOff()
        {
            Assert.Equal(LoginItemStatus.Disabled, Make().Status);
        }

        [Fact]
        public void RegisteringTurnsItOnAndStoresTheQuotedPath()
        {
            RegistryLoginItemService service = Make();

            service.Register();

            Assert.Equal(LoginItemStatus.Enabled, service.Status);
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey)!)
                Assert.Equal("\"" + Exe + "\"", key.GetValue("ShelfDrop"));
        }

        [Fact]
        public void UnregisteringTurnsItOff()
        {
            RegistryLoginItemService service = Make();
            service.Register();

            service.Unregister();

            Assert.Equal(LoginItemStatus.Disabled, service.Status);
        }

        [Fact]
        public void UnregisteringWhenNothingIsThereIsFine()
        {
            Make().Unregister();
        }

        [Fact]
        public void AnEntrySwitchedOffInSettingsNeedsTheUsersApproval()
        {
            RegistryLoginItemService service = Make();
            service.Register();
            using (RegistryKey approved = Registry.CurrentUser.CreateSubKey(ApprovedKey))
                approved.SetValue("ShelfDrop", new byte[] { 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);

            Assert.Equal(LoginItemStatus.RequiresApproval, service.Status);
        }

        [Fact]
        public void AnOldSwitchedOffVerdictDoesNotSpoilTheNextRegistration()
        {
            RegistryLoginItemService service = Make();
            service.Register();
            using (RegistryKey approved = Registry.CurrentUser.CreateSubKey(ApprovedKey))
                approved.SetValue("ShelfDrop", new byte[] { 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
            service.Unregister();

            service.Register();

            Assert.Equal(LoginItemStatus.Enabled, service.Status);
        }

        [Fact]
        public void AnEntryForAnotherCopyCountsAsOffForThisOne()
        {
            Make(@"D:\Portable\ShelfDrop.exe").Register();

            Assert.Equal(LoginItemStatus.Disabled, Make().Status);
        }

        [Fact]
        public void TheDefaultsPointAtTheRealPerUserRunKey()
        {
            Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Run", RegistryLoginItemService.DefaultRunKey);
            Assert.StartsWith(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved", RegistryLoginItemService.DefaultApprovedKey);
        }
    }

    [Collection("wpf")]
    public class ThemeTests
    {
        private readonly WpfFixture _wpf;

        public ThemeTests(WpfFixture wpf) => _wpf = wpf;

        private static Color ColourOf(string key) => ((SolidColorBrush)Application.Current.Resources[key]).Color;

        [Fact]
        public void DarkAndLightHaveDifferentPanelsAndReadableText() => _wpf.Run(() =>
        {
            Theme.Apply(dark: true);
            Color darkPanel = ColourOf(Theme.Keys.Panel);
            Color darkText = ColourOf(Theme.Keys.Text);
            Assert.True(Theme.IsDark);

            Theme.Apply(dark: false);
            Color lightPanel = ColourOf(Theme.Keys.Panel);
            Color lightText = ColourOf(Theme.Keys.Text);
            Assert.False(Theme.IsDark);

            Assert.True(Luminance(darkPanel) < 0.3, "a dark panel");
            Assert.True(Luminance(lightPanel) > 0.7, "a light panel");
            Assert.True(Luminance(darkText) > 0.7, "light text on the dark panel");
            Assert.True(Luminance(lightText) < 0.3, "dark text on the light panel");
        });

        [Fact]
        public void EveryColourTheShelfUsesIsDefined() => _wpf.Run(() =>
        {
            foreach (bool dark in new[] { false, true })
            {
                Theme.Apply(dark);
                foreach (System.Reflection.FieldInfo field in typeof(Theme.Keys).GetFields())
                {
                    string key = (string)field.GetRawConstantValue()!;
                    Assert.True(Application.Current.Resources[key] is SolidColorBrush, $"{key} is missing in {(dark ? "dark" : "light")}");
                }
            }
            Theme.Apply(dark: false);
        });

        [Fact]
        public void TheAccentIsOpaque() => _wpf.Run(() =>
        {
            Theme.Apply(dark: false);
            Assert.Equal(255, ColourOf(Theme.Keys.Accent).A);
        });

        [Fact]
        public void ReadingTheSystemSettingDoesNotThrow()
        {
            _ = Theme.SystemUsesDarkApps();
        }

        private static double Luminance(Color c) => (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
    }

    [Collection("wpf")]
    public class ThumbnailServiceTests
    {
        private readonly WpfFixture _wpf;

        public ThumbnailServiceTests(WpfFixture wpf) => _wpf = wpf;

        [Fact]
        public async Task APhotoGetsARealThumbnail()
        {
            using (var scratch = new TempDir())
            {
                // The picture is drawn with WPF, which needs the UI thread; the thumbnail is then asked for from outside it.
                string photo = _wpf.Run(() => DemoContent.Create(new TempStorage(Path.Combine(scratch.Path, "ShelfDrop"))).First(i => i.IsImageFile).Path!);

                ImageSource? picture = await ThumbnailService.LoadAsync(photo, 128);

                Assert.NotNull(picture);
                Assert.True(((BitmapSource)picture!).PixelWidth > 16);
                Assert.True(picture.IsFrozen, "made on another thread, so it must be usable from this one");
            }
        }

        [Fact]
        public async Task AnyOtherFileGetsItsIcon()
        {
            using (var scratch = new TempDir())
            {
                ImageSource? picture = await ThumbnailService.LoadAsync(scratch.File("notes.txt"), 128);

                Assert.NotNull(picture);
            }
        }

        [Fact]
        public async Task AFolderGetsItsIcon()
        {
            using (var scratch = new TempDir())
            {
                Assert.NotNull(await ThumbnailService.LoadAsync(scratch.Folder("Photos"), 128));
            }
        }

        [Fact]
        public void APictureIsReadFromItsFileWhereTheShellHasNoThumbnail()
        {
            using (var scratch = new TempDir())
            {
                string photo = _wpf.Run(() => DemoContent.Create(new TempStorage(Path.Combine(scratch.Path, "ShelfDrop"))).First(i => i.IsImageFile).Path!);

                ImageSource? picture = ThumbnailService.FromPictureFile(photo, 256);

                var bitmap = Assert.IsAssignableFrom<BitmapSource>(picture);
                Assert.Equal(256, bitmap.PixelWidth);   // the photo is bigger, so it is decoded down to what was asked for
                Assert.True(bitmap.IsFrozen);
            }
        }

        [Fact]
        public void ASmallPictureIsNotBlownUp()
        {
            using (var scratch = new TempDir())
            {
                string path = Path.Combine(scratch.Path, "tiny.png");
                _wpf.Run(() =>
                {
                    var bitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (FileStream stream = File.Create(path)) encoder.Save(stream);
                });

                Assert.Equal(32, ((BitmapSource)ThumbnailService.FromPictureFile(path, 256)!).PixelWidth);
            }
        }

        [Fact]
        public void OnlyRealPicturesAreReadAsPictures()
        {
            using (var scratch = new TempDir())
            {
                Assert.Null(ThumbnailService.FromPictureFile(scratch.File("report.pdf"), 256));
                Assert.Null(ThumbnailService.FromPictureFile(scratch.File("not really.png", "this is text, not a picture"), 256));
                Assert.Null(ThumbnailService.FromPictureFile(Path.Combine(scratch.Path, "missing.png"), 256));
            }
        }

        [Fact]
        public async Task AFileThatIsNotThereGetsNothingAndNoException()
        {
            string path = Path.Combine(Path.GetTempPath(), "ShelfDrop-missing-" + Guid.NewGuid() + ".png");

            Assert.Null(await ThumbnailService.LoadAsync(path, 128));
        }
    }

    public class NativeLayerTests
    {
        [Fact]
        public void TheScreenListHasAPrimaryScreenWithSaneNumbers()
        {
            var screens = new WindowsScreenProvider().Screens;

            Assert.NotEmpty(screens);
            ScreenInfo primary = Assert.Single(screens, s => s.IsPrimary);
            Assert.True(primary.Bounds.Width > 0 && primary.Bounds.Height > 0);
            Assert.True(primary.Scale >= 1.0 && primary.Scale <= 8.0, "scale " + primary.Scale);
            Assert.True(primary.WorkArea.Width <= primary.Bounds.Width && primary.WorkArea.Height <= primary.Bounds.Height);
        }

        [Fact]
        public void TheCursorCanBeRead()
        {
            PixelPoint point = new WindowsScreenProvider().CursorPosition;

            Assert.InRange(point.X, -100000, 100000);
        }

        [Fact]
        public void TheSystemScaleIsSane()
        {
            Assert.InRange(WindowsScreenProvider.SystemScale(), 1.0, 8.0);
        }

        [Fact]
        public void AMouseHookCanBeInstalledAndRemoved()
        {
            using (var hook = new MouseHook())
            {
                hook.Install();
                hook.Install();   // a second call changes nothing
            }
        }
    }

    [Collection("wpf")]
    public class TrayIconTests
    {
        private readonly WpfFixture _wpf;

        public TrayIconTests(WpfFixture wpf) => _wpf = wpf;

        [Fact]
        public void TheTrayIconCanBeCreatedAndRemoved() => _wpf.Run(() =>
        {
            var tray = new TrayIcon(new TrayActions());
            tray.Dispose();
        });
    }

    [Collection("wpf")]
    public class DemoContentTests
    {
        private readonly WpfFixture _wpf;

        public DemoContentTests(WpfFixture wpf) => _wpf = wpf;

        [Fact]
        public void TheDemoShelfHasAMixOfThings() => _wpf.Run(() =>
        {
            using (var scratch = new TempDir())
            {
                var items = DemoContent.Create(new TempStorage(Path.Combine(scratch.Path, "ShelfDrop")));

                Assert.Equal(6, items.Count);
                Assert.Contains(items, i => i.Kind == ShelfItemKind.Link);
                Assert.Contains(items, i => i.Kind == ShelfItemKind.Text);
                Assert.Contains(items, i => i.IsImageFile);
                Assert.All(items.Where(i => i.Kind == ShelfItemKind.File), i => Assert.False(i.IsMissing));
            }
        });
    }
}
