using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace ShelfDrop.App
{
    public static class Program
    {
        /// <summary>Held for as long as the program runs, so that a second copy can tell and leave.</summary>
        public const string SingleInstanceMutexName = "ShelfDrop.SingleInstance";

        [STAThread]
        public static int Main(string[] args)
        {
            using (var mutex = new Mutex(true, SingleInstanceMutexName, out bool isFirst))
            {
                // A second copy would put a second icon in the tray and a second hook on the mouse.
                if (!isFirst)
                {
                    Log.Info("already running; exiting");
                    return 0;
                }

                // The tray menu and the uninstall dialog come from Windows Forms: have them drawn in the current Windows style.
                System.Windows.Forms.Application.EnableVisualStyles();

                var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                application.DispatcherUnhandledException += OnUnhandled;
                AppDomain.CurrentDomain.UnhandledException += (sender, e) => Log.Error("unhandled: " + e.ExceptionObject);

                using (var host = new AppHost(application, args))
                {
                    try
                    {
                        host.Start();
                    }
                    catch (Exception e)
                    {
                        Log.Error("could not start", e);
                        System.Windows.MessageBox.Show(
                            "ShelfDrop could not start: " + e.Message + "\n\nDetails are in " + Log.FilePath,
                            "ShelfDrop", MessageBoxButton.OK, MessageBoxImage.Error);
                        return 1;
                    }
                    return application.Run();
                }
            }
        }

        private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // A tray program should not vanish because one click went wrong: note it and carry on.
            Log.Error("unhandled exception", e.Exception);
            e.Handled = true;
        }
    }
}
