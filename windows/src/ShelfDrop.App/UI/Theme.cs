using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace ShelfDrop.App.UI
{
    /// <summary>
    /// The colours of the shelf, light or dark to match Windows. They live in the application's resources under the names
    /// in <see cref="Keys"/>, and every element refers to them by name, so switching the theme repaints everything at once.
    /// </summary>
    internal static class Theme
    {
        public static class Keys
        {
            public const string Panel = "ShelfPanel";
            public const string PanelBorder = "ShelfPanelBorder";
            public const string Text = "ShelfText";
            public const string TextSecondary = "ShelfTextSecondary";
            public const string TextTertiary = "ShelfTextTertiary";
            public const string Card = "ShelfCard";
            public const string CardHover = "ShelfCardHover";
            public const string CardBorder = "ShelfCardBorder";
            public const string CardBorderHover = "ShelfCardBorderHover";
            public const string Control = "ShelfControl";
            public const string ControlHover = "ShelfControlHover";
            public const string ControlPressed = "ShelfControlPressed";
            public const string Circle = "ShelfCircle";
            public const string ZoneStroke = "ShelfZoneStroke";
            public const string Accent = "ShelfAccent";
            public const string AccentWash = "ShelfAccentWash";
            public const string AccentWashStrong = "ShelfAccentWashStrong";
            public const string Warning = "ShelfWarning";
            public const string Transparent = "ShelfTransparent";
        }

        public static bool IsDark { get; private set; }

        /// <summary>Raised after the colours changed, for the few things (custom drawing) that cannot follow a resource by themselves.</summary>
        public static event Action? Changed;

        /// <summary>True when Windows is set to dark mode for apps.</summary>
        public static bool SystemUsesDarkApps()
        {
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void Apply() => Apply(SystemUsesDarkApps());

        public static void Apply(bool dark)
        {
            ResourceDictionary resources = Application.Current.Resources;
            Color accent = SystemParameters.WindowGlassColor;
            accent.A = 255;

            if (dark)
            {
                Set(resources, Keys.Panel, Argb(0xF2, 0x20, 0x22, 0x28));
                Set(resources, Keys.PanelBorder, Argb(0x1F, 255, 255, 255));
                Set(resources, Keys.Text, Argb(0xFF, 0xF5, 0xF5, 0xF7));
                Set(resources, Keys.TextSecondary, Argb(0x99, 255, 255, 255));
                Set(resources, Keys.TextTertiary, Argb(0x5C, 255, 255, 255));
                Set(resources, Keys.Card, Argb(0x0F, 255, 255, 255));
                Set(resources, Keys.CardHover, Argb(0x1F, 255, 255, 255));
                Set(resources, Keys.CardBorder, Argb(0x14, 255, 255, 255));
                Set(resources, Keys.CardBorderHover, Argb(0x2E, 255, 255, 255));
                Set(resources, Keys.Control, Argb(0x14, 255, 255, 255));
                Set(resources, Keys.ControlHover, Argb(0x24, 255, 255, 255));
                Set(resources, Keys.ControlPressed, Argb(0x38, 255, 255, 255));
                Set(resources, Keys.Circle, Argb(0xEB, 0x3A, 0x3C, 0x44));
                Set(resources, Keys.ZoneStroke, Argb(0x38, 255, 255, 255));
                Set(resources, Keys.Warning, Argb(0xFF, 0xFF, 0x9F, 0x0A));
            }
            else
            {
                Set(resources, Keys.Panel, Argb(0xF5, 0xF7, 0xF8, 0xFB));
                Set(resources, Keys.PanelBorder, Argb(0x1A, 0, 0, 0));
                Set(resources, Keys.Text, Argb(0xFF, 0x1D, 0x1D, 0x1F));
                Set(resources, Keys.TextSecondary, Argb(0x99, 0, 0, 0));
                Set(resources, Keys.TextTertiary, Argb(0x59, 0, 0, 0));
                Set(resources, Keys.Card, Argb(0x0A, 0, 0, 0));
                Set(resources, Keys.CardHover, Argb(0x14, 0, 0, 0));
                Set(resources, Keys.CardBorder, Argb(0x14, 0, 0, 0));
                Set(resources, Keys.CardBorderHover, Argb(0x26, 0, 0, 0));
                Set(resources, Keys.Control, Argb(0x0F, 0, 0, 0));
                Set(resources, Keys.ControlHover, Argb(0x1F, 0, 0, 0));
                Set(resources, Keys.ControlPressed, Argb(0x33, 0, 0, 0));
                Set(resources, Keys.Circle, Argb(0xF2, 255, 255, 255));
                Set(resources, Keys.ZoneStroke, Argb(0x40, 0, 0, 0));
                Set(resources, Keys.Warning, Argb(0xFF, 0xE0, 0x6A, 0x00));
            }

            Set(resources, Keys.Accent, accent);
            Set(resources, Keys.AccentWash, Color.FromArgb(0x1A, accent.R, accent.G, accent.B));
            Set(resources, Keys.AccentWashStrong, Color.FromArgb(0x29, accent.R, accent.G, accent.B));
            Set(resources, Keys.Transparent, Colors.Transparent);

            bool changed = dark != IsDark;
            IsDark = dark;
            Changed?.Invoke();
            if (changed) Log.Info("theme: " + (dark ? "dark" : "light"));
        }

        private static Color Argb(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);

        private static void Set(ResourceDictionary resources, string key, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            resources[key] = brush;
        }
    }
}
