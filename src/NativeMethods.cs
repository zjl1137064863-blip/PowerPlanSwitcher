using System;
using System.Runtime.InteropServices;

namespace PowerPlanSwitcher
{
    internal static class NativeMethods
    {
        internal static readonly uint ShowMessage = RegisterWindowMessage("PowerPlanSwitcher.Show.7D43B3AE");
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll")]
        internal static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll")]
        internal static extern bool DestroyIcon(IntPtr icon);
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        internal static void ApplyWindowTheme(IntPtr window, Theme theme)
        {
            // Unsupported attributes return an HRESULT; the normal system frame is the fallback.
            try
            {
                int dark = theme.Dark && !theme.HighContrast ? 1 : 0;
                DwmSetWindowAttribute(window, 20, ref dark, sizeof(int));
                if (Environment.OSVersion.Version.Build >= 22000)
                {
                    int corners = theme.HighContrast ? 1 : 2;
                    DwmSetWindowAttribute(window, 33, ref corners, sizeof(int));
                }
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
    }
}
