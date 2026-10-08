using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ShelfDrop.Core;

namespace ShelfDrop.App.UI
{
    /// <summary>What the tray icon's menu does. Each is a plain callback, so the menu knows nothing about the rest of the app.</summary>
    internal sealed class TrayActions
    {
        public Action ToggleShelf { get; set; } = () => { };
        public Action ClearShelf { get; set; } = () => { };
        public Action ToggleLaunchAtLogin { get; set; } = () => { };
        public Action Uninstall { get; set; } = () => { };
        public Action Quit { get; set; } = () => { };
        public Func<MenuCheckState> LaunchAtLoginState { get; set; } = () => MenuCheckState.Off;
        public Func<UninstallAvailability> UninstallState { get; set; } = () => new UninstallAvailability(false);
    }

    /// <summary>
    /// ShelfDrop's home in the notification area (the system tray): a click shows or hides the shelf, a right click opens the menu.
    /// The only thing here that uses Windows Forms, because WPF has no tray icon of its own.
    /// </summary>
    internal sealed class TrayIcon : IDisposable
    {
        private readonly NotifyIcon _icon;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _launchAtLogin;
        private readonly ToolStripMenuItem _uninstall;
        private readonly TrayActions _actions;

        public TrayIcon(TrayActions actions)
        {
            _actions = actions;

            _launchAtLogin = new ToolStripMenuItem("Launch at Login");
            _launchAtLogin.Click += (sender, args) => actions.ToggleLaunchAtLogin();

            _uninstall = new ToolStripMenuItem("Uninstall ShelfDrop…");
            _uninstall.Click += (sender, args) => actions.Uninstall();

            _menu = new ContextMenuStrip { ShowItemToolTips = true };
            _menu.Items.Add(Item("Show/Hide Shelf", actions.ToggleShelf));
            _menu.Items.Add(Item("Clear Shelf", actions.ClearShelf));
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_launchAtLogin);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_uninstall);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(Item("Quit ShelfDrop", actions.Quit));
            // The check mark and the uninstall item reflect the machine's state, which can change while the app runs.
            _menu.Opening += (sender, args) => RefreshItems();

            _icon = new NotifyIcon
            {
                Text = "ShelfDrop",
                ContextMenuStrip = _menu,
                Icon = LoadIcon(),
                Visible = true,
            };
            _icon.MouseClick += (sender, args) =>
            {
                if (args.Button == MouseButtons.Left) actions.ToggleShelf();
            };
        }

        public void Dispose()
        {
            // Without this the icon stays in the tray, dead, until the pointer is moved over it.
            _icon.Visible = false;
            _icon.Dispose();
            _menu.Dispose();
        }

        private void RefreshItems()
        {
            MenuCheckState state = _actions.LaunchAtLoginState();
            _launchAtLogin.Checked = state == MenuCheckState.On;
            _launchAtLogin.CheckState = state == MenuCheckState.On ? CheckState.Checked
                : state == MenuCheckState.Mixed ? CheckState.Indeterminate
                : CheckState.Unchecked;
            _launchAtLogin.ToolTipText = state == MenuCheckState.Mixed
                ? "Switched off in Settings → Apps → Startup. Click to open that page."
                : string.Empty;

            UninstallAvailability availability = _actions.UninstallState();
            _uninstall.Enabled = availability.IsAvailable;
            _uninstall.ToolTipText = availability.IsAvailable ? string.Empty : availability.Reason ?? string.Empty;
        }

        private static ToolStripMenuItem Item(string text, Action action)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += (sender, args) => action();
            return item;
        }

        private static Icon LoadIcon()
        {
            using (Stream? stream = typeof(TrayIcon).Assembly.GetManifestResourceStream("ShelfDrop.ico"))
            {
                // The size of icon the tray wants at this screen's scaling, taken from the several that the file holds.
                return stream != null ? new Icon(stream, SystemInformation.SmallIconSize) : SystemIcons.Application;
            }
        }
    }
}
