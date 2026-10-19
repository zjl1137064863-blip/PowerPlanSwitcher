using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PowerPlanSwitcher
{
    internal sealed class Theme
    {
        internal readonly bool Dark, HighContrast;
        internal readonly Color Background, Surface, Hover, Selected, Border, Text, Muted, Accent, OnAccent, Error;
        private static readonly string FontFamily = FindFont();
        private static readonly string ChineseFontFamily = FindChineseFont();

        internal Theme(bool dark, bool highContrast, Color accent)
        {
            Dark = dark; HighContrast = highContrast;
            if (highContrast)
            {
                Background = SystemColors.Window; Surface = SystemColors.Window;
                Hover = Selected = SystemColors.Highlight; Border = SystemColors.WindowText;
                Text = Muted = SystemColors.WindowText; Accent = SystemColors.Highlight;
                OnAccent = SystemColors.HighlightText; Error = SystemColors.WindowText;
                return;
            }
            Background = ColorTranslator.FromHtml(dark ? "#202020" : "#F3F3F3");
            Surface = ColorTranslator.FromHtml(dark ? "#2B2B2B" : "#FBFBFB");
            Hover = ColorTranslator.FromHtml(dark ? "#323232" : "#F0F0F0");
            Selected = ColorTranslator.FromHtml(dark ? "#343434" : "#EAEAEA");
            Border = ColorTranslator.FromHtml(dark ? "#3D3D3D" : "#D6D6D6");
            Text = ColorTranslator.FromHtml(dark ? "#F5F5F5" : "#1B1B1B");
            Muted = ColorTranslator.FromHtml(dark ? "#B5B5B5" : "#606060");
            Accent = accent;
            OnAccent = accent.R * .299 + accent.G * .587 + accent.B * .114 > 145 ? Color.FromArgb(20, 20, 20) : Color.White;
            Error = ColorTranslator.FromHtml(dark ? "#FFB4AB" : "#B3261E");
        }

        internal static Theme ReadSystem()
        {
            bool dark = true;
            Color accent = ColorTranslator.FromHtml("#60CDFF");
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    if (key != null) dark = Convert.ToInt32(key.GetValue("AppsUseLightTheme", 0)) == 0;
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent"))
                {
                    object value = key == null ? null : key.GetValue("AccentColorMenu");
                    if (value != null)
                    {
                        uint abgr = unchecked((uint)Convert.ToInt32(value));
                        accent = Color.FromArgb((int)(abgr & 255), (int)((abgr >> 8) & 255), (int)((abgr >> 16) & 255));
                    }
                    else
                    {
                        uint argb; bool opaque;
                        if (DwmGetColorizationColor(out argb, out opaque) == 0)
                            accent = Color.FromArgb((int)((argb >> 16) & 255), (int)((argb >> 8) & 255), (int)(argb & 255));
                    }
                }
            }
            catch { /* Defaults also work when personalization is unavailable. */ }
            return new Theme(dark, SystemInformation.HighContrast, accent);
        }

        private static string FindFont()
        {
            using (var fonts = new InstalledFontCollection())
                foreach (string candidate in new[] { "Segoe UI Variable Text", "Segoe UI Variable", "Segoe UI", "Microsoft YaHei UI" })
                    foreach (FontFamily family in fonts.Families)
                        if (String.Equals(family.Name, candidate, StringComparison.OrdinalIgnoreCase)) return candidate;
            return SystemFonts.MessageBoxFont.FontFamily.Name;
        }
        private static string FindChineseFont()
        {
            using (var fonts = new InstalledFontCollection())
                foreach (string candidate in new[] { "Microsoft YaHei UI", "Microsoft YaHei" })
                    foreach (FontFamily family in fonts.Families)
                        if (String.Equals(family.Name, candidate, StringComparison.OrdinalIgnoreCase)) return candidate;
            return FontFamily;
        }
        internal static Font Font(float size, FontStyle style = FontStyle.Regular, string text = null)
        {
            bool chinese = false;
            if (text != null) foreach (char character in text) if (character >= '\u2e80' && character <= '\u9fff') { chinese = true; break; }
            return new Font(chinese ? ChineseFontFamily : FontFamily, size, style, GraphicsUnit.Point);
        }

        internal static GraphicsPath Round(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            if (d <= 0) { path.AddRectangle(rect); return path; }
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure(); return path;
        }
        [DllImport("dwmapi.dll")]
        private static extern int DwmGetColorizationColor(out uint color, out bool opaque);
    }
}
