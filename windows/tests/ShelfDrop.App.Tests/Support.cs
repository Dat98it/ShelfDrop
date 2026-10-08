using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ShelfDrop.App.Services;
using ShelfDrop.App.UI;
using ShelfDrop.Core;
using ShelfDrop.Core.Tests;
using Xunit;

namespace ShelfDrop.App.Tests
{
    /// <summary>
    /// One WPF application and UI thread for all the tests that need windows. WPF allows a single Application per process
    /// and its objects belong to the thread that made them, so every such test runs its body on this thread.
    /// </summary>
    public sealed class WpfFixture : IDisposable
    {
        private readonly Thread _thread;

        public WpfFixture()
        {
            var ready = new ManualResetEventSlim();
            Dispatcher? dispatcher = null;
            _thread = new Thread(() =>
            {
                var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                GC.KeepAlive(application);
                dispatcher = Dispatcher.CurrentDispatcher;
                Theme.Apply(dark: false);
                ready.Set();
                Dispatcher.Run();
            })
            {
                IsBackground = true,
                Name = "WPF test thread",
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            ready.Wait();
            Dispatcher = dispatcher!;
        }

        public Dispatcher Dispatcher { get; }

        public void Run(Action body) => Dispatcher.Invoke(body);

        public T Run<T>(Func<T> body) => Dispatcher.Invoke(body);

        public void Dispose()
        {
            Dispatcher.InvokeShutdown();
            _thread.Join(TimeSpan.FromSeconds(5));
        }
    }

    [CollectionDefinition("wpf")]
    public sealed class WpfCollection : ICollectionFixture<WpfFixture>
    {
    }

    /// <summary>A test that moves the real mouse. It only runs when asked to, so that running the tests on a desk never clicks on it.</summary>
    public sealed class InteractiveFactAttribute : FactAttribute
    {
        public InteractiveFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("SHELFDROP_INTERACTIVE_TESTS") != "1")
                Skip = "Moves the real mouse. Set SHELFDROP_INTERACTIVE_TESTS=1 to run it (the automated build does).";
        }
    }

    internal static class Artifacts
    {
        /// <summary>Where pictures taken by the tests are kept, so the automated build can publish them.</summary>
        public static string PathFor(string name)
        {
            string directory = Environment.GetEnvironmentVariable("SHELFDROP_ARTIFACTS") ?? Path.Combine(AppContext.BaseDirectory, "artifacts");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, name);
        }
    }

    internal static class Pump
    {
        /// <summary>Lets the UI thread work for a while (layout, rendering, background loads that report back to it).</summary>
        public static void For(TimeSpan duration)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = duration };
            timer.Tick += (sender, args) =>
            {
                timer.Stop();
                frame.Continue = false;
            };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }
    }

    /// <summary>A shelf window wired to a controller, with fake screens and a scratch temp folder, and nothing saved anywhere real.</summary>
    internal sealed class ShelfHarness : IDisposable
    {
        public ShelfHarness()
        {
            Scratch = new TempDir();
            Temp = new TempStorage(Path.Combine(Scratch.Path, "ShelfDrop"));
            Settings = new InMemoryKeyValueStore();
            Window = new ShelfWindow(new DragOutService());
            Controller = new ShelfController(
                Window, new FakeScreens(), new FakeShare(), new ManualDelays(),
                new ShelfSizeStore(Settings), new ShelfPositionStore(Settings), Temp);
            Window.Attach(Controller, new PayloadImporter(Temp));
        }

        public TempDir Scratch { get; }
        public TempStorage Temp { get; }
        public InMemoryKeyValueStore Settings { get; }
        public ShelfWindow Window { get; }
        public ShelfController Controller { get; }

        public void Dispose()
        {
            Window.Close();
            Scratch.Dispose();
        }
    }
}
