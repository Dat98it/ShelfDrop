using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using ShelfDrop.App.Native;
using ShelfDrop.App.Services;
using ShelfDrop.App.UI;
using ShelfDrop.Core;

namespace ShelfDrop.App
{
    /// <summary>Builds every piece of the app and connects them. Nothing here decides anything: that is all in ShelfDrop.Core.</summary>
    internal sealed class AppHost : IDisposable
    {
        private readonly Application _application;
        private readonly string[] _arguments;
        private readonly TempStorage _temp = new TempStorage();
        private readonly JsonFileKeyValueStore _settings = new JsonFileKeyValueStore(JsonFileKeyValueStore.DefaultPath);

        private ShelfWindow? _window;
        private ShelfController? _controller;
        private WindowsScreenProvider? _screens;
        private RegistryLoginItemService? _loginItem;
        private LaunchAtLogin? _launchAtLogin;
        private Uninstaller? _uninstaller;
        private MouseHook? _hook;
        private DragTracker? _tracker;
        private DispatcherTimer? _buttonPoll;
        private TrayIcon? _tray;
        private bool _disposed;

        public AppHost(Application application, string[] arguments)
        {
            _application = application;
            _arguments = arguments;
        }

        public void Start()
        {
            Log.Info("starting " + string.Join(" ", _arguments));
            Theme.Apply();
            _temp.CleanUp();   // the shelf is not kept between runs, so whatever is left is from one that did not exit cleanly

            var dragOut = new DragOutService();
            _window = new ShelfWindow(dragOut);
            _screens = new WindowsScreenProvider();
            var share = new ShareSheetPresenter(() => _window.Handle, (title, message) => Dialogs.Tell(title, message, isWarning: false));
            _controller = new ShelfController(
                _window, _screens, share, new DispatcherDelayedActions(),
                new ShelfSizeStore(_settings), new ShelfPositionStore(_settings), _temp);
            _window.Attach(_controller, new PayloadImporter(_temp));

            _loginItem = new RegistryLoginItemService();
            _launchAtLogin = new LaunchAtLogin(_loginItem);
            _uninstaller = new Uninstaller(
                Environment.ProcessPath ?? string.Empty,
                _loginItem,
                new UninstallEffects
                {
                    LaunchUninstaller = StartUninstaller,
                    RemoveTemporaryFiles = _temp.CleanUp,
                    RemoveSettings = _settings.Erase,
                });

            _tray = new TrayIcon(new TrayActions
            {
                ToggleShelf = () => _controller.Toggle(),
                ClearShelf = () => _controller.Clear(),
                ToggleLaunchAtLogin = ToggleLaunchAtLogin,
                Uninstall = UninstallApp,
                Quit = () => Quit(),
                LaunchAtLoginState = () => _launchAtLogin.MenuState,
                UninstallState = () => _uninstaller.Availability,
            });

            StartListeningForDrags();
            HandleArguments();
            if (!HasArgument("--demo") && !HasArgument("--show-shelf") && !HasArgument("--marketing")) _window.Prewarm();
        }

        public void Quit(int exitCode = 0)
        {
            Log.Info("quitting");
            Dispose();
            _application.Shutdown(exitCode);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _buttonPoll?.Stop();
            _hook?.Dispose();
            _tray?.Dispose();
            _window?.Close();
        }

        // Shaking

        private void StartListeningForDrags()
        {
            double scale = WindowsScreenProvider.SystemScale();
            _tracker = new DragTracker(new ShakeDetector(ShakeConfiguration.ForScale(scale)), dragThreshold: 8 * scale)
            {
                // Pressing on the shelf itself (to move it, resize it, or drag an item out) is not a drag on its way to the shelf.
                ShouldIgnorePress = point => _window!.IsVisible && _window.Frame.Contains(point),
            };

            // Windows takes a mouse hook away from a program that is slow to answer it, and opening the window for the first time
            // is slow. So the hook only feeds the tracker, which is quick, and what the tracker decides is done a moment later.
            Dispatcher ui = _application.Dispatcher;
            _tracker.DragBegan += () => ui.BeginInvoke(new Action(() =>
            {
                Log.Info("drag began");
                _controller!.DragBegan();
                _buttonPoll?.Start();
            }));
            _tracker.DragEnded += () => ui.BeginInvoke(new Action(() =>
            {
                Log.Info("drag ended");
                _buttonPoll?.Stop();
                _controller!.DragEnded();
            }));
            _tracker.Shaken += point => ui.BeginInvoke(new Action(() =>
            {
                Log.Info("shake at " + point);
                _controller!.Show(point);
            }));

            // The hook should see every button release, but if one is ever missed the drag would never end: look at the button too.
            int button = NativeMethods.GetSystemMetrics(NativeMethods.SM_SWAPBUTTON) != 0 ? NativeMethods.VK_RBUTTON : NativeMethods.VK_LBUTTON;
            _buttonPoll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _buttonPoll.Tick += (sender, args) => _tracker!.ObserveButton((NativeMethods.GetAsyncKeyState(button) & 0x8000) != 0);

            _hook = new MouseHook();
            _hook.LeftDown += point => _tracker.OnLeftDown(point);
            _hook.Moved += (point, seconds) => _tracker.OnMove(point, seconds);
            _hook.LeftUp += () => _tracker.OnLeftUp();
            try
            {
                _hook.Install();
                Log.Info("mouse hook installed");
            }
            catch (Exception e)
            {
                // The tray icon still works, so the shelf can be opened from its menu.
                Log.Error("the mouse hook could not be installed: shaking will not open the shelf", e);
            }
        }

        // Menu actions

        private void ToggleLaunchAtLogin()
        {
            LaunchAtLoginOutcome outcome = _launchAtLogin!.Toggle();
            Log.Info("launch at login: " + outcome.Kind);
            if (outcome.Kind == LaunchAtLoginOutcomeKind.Failed)
                Dialogs.Tell("Couldn't change Launch at Login", outcome.Error ?? "Unknown error.");
        }

        private void UninstallApp()
        {
            if (!_uninstaller!.Availability.IsAvailable) return;
            if (!Dialogs.ConfirmUninstall(_uninstaller.Confirmation())) return;

            UninstallOutcome outcome = _uninstaller.Uninstall();
            if (outcome.Succeeded)
            {
                // The settings are gone now: make sure quitting does not write them back.
                _controller!.RetireSettings();
                Log.Info("uninstalling");
                if (outcome.Warnings.Count > 0)
                    Dialogs.Tell("ShelfDrop is being uninstalled", string.Join("\n\n", outcome.Warnings));
                Quit();
            }
            else
            {
                Log.Error("uninstall failed: " + outcome.Error);
                Dialogs.Tell("Couldn't uninstall ShelfDrop", outcome.Error ?? "Unknown error.");
            }
        }

        private static void StartUninstaller(string path)
        {
            // /SILENT: our own dialog has already asked, so the uninstaller only shows its progress.
            Process? process = Process.Start(new ProcessStartInfo(path, "/SILENT /NORESTART") { UseShellExecute = false });
            if (process == null) throw new InvalidOperationException("Windows did not start it.");
        }

        // Command line, used for pictures and checks

        private async void RenderMarketingImages(string directory)
        {
            int exitCode = 0;
            try
            {
                await MarketingImages.RenderAsync(_window!, _controller!, _temp, directory);
                Log.Info("marketing pictures written to " + directory);
            }
            catch (Exception e)
            {
                Log.Error("could not draw the marketing pictures", e);
                exitCode = 1;
            }
            Quit(exitCode);
        }

        private bool HasArgument(string name) => _arguments.Contains(name, StringComparer.OrdinalIgnoreCase);

        private void HandleArguments()
        {
            bool demo = HasArgument("--demo");
            if (demo) _controller!.Model.Add(DemoContent.Create(_temp));

            if (demo || HasArgument("--show-shelf"))
            {
                ScreenInfo? primary = _screens!.Screens.FirstOrDefault(s => s.IsPrimary);
                PixelPoint centre = primary == null
                    ? new PixelPoint(600, 400)
                    : new PixelPoint(primary.WorkArea.Left + primary.WorkArea.Width / 2, primary.WorkArea.Top + primary.WorkArea.Height / 2);
                _controller!.Show(centre);
            }

            int marketing = Array.FindIndex(_arguments, a => string.Equals(a, "--marketing", StringComparison.OrdinalIgnoreCase));
            if (marketing >= 0 && marketing + 1 < _arguments.Length) RenderMarketingImages(_arguments[marketing + 1]);

            int index = Array.FindIndex(_arguments, a => string.Equals(a, "--screenshot", StringComparison.OrdinalIgnoreCase));
            if (index >= 0 && index + 1 < _arguments.Length)
            {
                string path = _arguments[index + 1];
                // Long enough for the thumbnails, which load in the background, to arrive.
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (sender, args) =>
                {
                    timer.Stop();
                    try
                    {
                        _window!.SaveScreenshot(path);
                        Log.Info("screenshot saved to " + path);
                    }
                    catch (Exception e)
                    {
                        Log.Error("could not save the screenshot", e);
                    }
                    Quit();
                };
                timer.Start();
            }
        }
    }
}
