using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PowerPlanSwitcher
{
    internal sealed class AppSettings
    {
        internal bool HotkeysEnabled;
        internal bool CloseToTray = true;
        private readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal static AppSettings Load(string path)
        {
            var settings = new AppSettings();
            if (!File.Exists(path)) return settings;
            foreach (string line in File.ReadAllLines(path))
            {
                int separator = line.IndexOf('=');
                if (separator <= 0 || line.TrimStart().StartsWith("#")) continue;
                settings.values[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
            }
            string value;
            if (settings.values.TryGetValue("hotkeys", out value)) settings.HotkeysEnabled = IsTrue(value);
            if (settings.values.TryGetValue("closeToTray", out value)) settings.CloseToTray = IsTrue(value);
            return settings;
        }
        private static bool IsTrue(string value) { return value == "1" || String.Equals(value, "true", StringComparison.OrdinalIgnoreCase); }

        internal string GetPlanIcon(Guid id)
        {
            string value;
            return values.TryGetValue("icon." + id.ToString("D"), out value) && IsValidIcon(value) ? value : "";
        }
        internal void SetPlanIcon(Guid id, string icon)
        {
            string key = "icon." + id.ToString("D");
            if (String.IsNullOrEmpty(icon)) { values.Remove(key); return; }
            if (!IsValidIcon(icon)) throw new ArgumentException("请输入一个表情或简短符号，不要包含空格、换行或超过 64 个字符。");
            values[key] = icon;
        }
        internal static bool IsValidIcon(string icon)
        {
            if (String.IsNullOrEmpty(icon) || icon.Length > 64) return false;
            for (int i = 0; i < icon.Length; i++)
            {
                char character = icon[i];
                if (Char.IsControl(character) || Char.IsWhiteSpace(character)) return false;
                if (Char.IsHighSurrogate(character))
                { if (i + 1 >= icon.Length || !Char.IsLowSurrogate(icon[++i])) return false; }
                else if (Char.IsLowSurrogate(character)) return false;
            }
            return true;
        }

        internal void Save(string path)
        {
            values["hotkeys"] = HotkeysEnabled ? "1" : "0";
            values["closeToTray"] = CloseToTray ? "1" : "0";
            var lines = new List<string>();
            foreach (var pair in values) lines.Add(pair.Key + "=" + pair.Value);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllLines(temporary, lines, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
