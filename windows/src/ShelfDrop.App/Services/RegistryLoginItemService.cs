using System;
using System.Diagnostics;
using Microsoft.Win32;
using ShelfDrop.Core;

namespace ShelfDrop.App.Services
{
    /// <summary>
    /// "Launch at login" through the per-user Run key, the way almost every tray program does it. It needs no
    /// administrator rights and shows up in Settings → Apps → Startup, where the user can switch it off.
    /// </summary>
    internal sealed class RegistryLoginItemService : ILoginItemService
    {
        public const string DefaultRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string DefaultApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        public const string DefaultValueName = "ShelfDrop";

        private readonly string _command;
        private readonly string _runKey;
        private readonly string _approvedKey;
        private readonly string _valueName;

        /// <param name="executablePath">The program to start at login. Defaults to the one that is running.</param>
        /// <param name="runKey">Where the entry is kept, under HKEY_CURRENT_USER. Only tests use anything but the default.</param>
        /// <param name="approvedKey">Where Windows keeps the user's on/off switch for it.</param>
        /// <param name="valueName">The name of the entry.</param>
        public RegistryLoginItemService(
            string? executablePath = null, string runKey = DefaultRunKey, string approvedKey = DefaultApprovedKey, string valueName = DefaultValueName)
        {
            _command = "\"" + (executablePath ?? Environment.ProcessPath ?? "") + "\"";
            _runKey = runKey;
            _approvedKey = approvedKey;
            _valueName = valueName;
        }

        public LoginItemStatus Status
        {
            get
            {
                try
                {
                    string? run;
                    byte[]? approved;
                    using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(_runKey))
                        run = key?.GetValue(_valueName) as string;
                    using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(_approvedKey))
                        approved = key?.GetValue(_valueName) as byte[];
                    return LoginItemStatusResolver.Resolve(run, _command, approved);
                }
                catch (Exception e)
                {
                    Log.Error("could not read the startup entry", e);
                    return LoginItemStatus.Unavailable;
                }
            }
        }

        public void Register()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(_runKey))
                key.SetValue(_valueName, _command, RegistryValueKind.String);
            ForgetApproval();
        }

        public void Unregister()
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(_runKey, writable: true))
                key?.DeleteValue(_valueName, throwOnMissingValue: false);
            ForgetApproval();
        }

        public void OpenSystemSettings()
        {
            Process.Start(new ProcessStartInfo("ms-settings:startupapps") { UseShellExecute = true });
        }

        /// <summary>
        /// Windows remembers a switch the user flipped in Settings in a separate value. Once our own entry is created or removed
        /// that old verdict no longer applies, and keeping it would make a fresh entry look "switched off" at once.
        /// </summary>
        private void ForgetApproval()
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(_approvedKey, writable: true))
                key?.DeleteValue(_valueName, throwOnMissingValue: false);
        }
    }
}
