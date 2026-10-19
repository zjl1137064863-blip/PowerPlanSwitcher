using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PowerPlanSwitcher;

internal static class UiSmoke
{
    private sealed class FakeApi : IPowerApi
    {
        internal readonly Guid[] Ids = { new Guid("381b4222-f694-41f0-9685-ff5bb260df2e"),
            new Guid("402b0f0d-2d95-4be4-bd06-73d366d81c94"), new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"),
            new Guid("a1841308-3541-4fab-bc81-f71556f20b4a") };
        internal Guid Active;
        internal int Writes;
        internal bool Fail, Ignore, ReadFails;
        internal FakeApi() { Active = Ids[0]; }
        public IList<PowerPlan> Enumerate()
        { return Ids.Select((id, i) => new PowerPlan(id, new[] { "平衡", "静音", "高性能", "节能" }[i])).ToList(); }
        public Guid GetActive()
        { if (ReadFails) throw new InvalidOperationException("模拟：读取当前方案失败"); return Active; }
        public void SetActive(Guid id)
        {
            Interlocked.Increment(ref Writes); Thread.Sleep(100);
            if (Fail) throw new InvalidOperationException("模拟：访问被拒绝");
            if (!Ignore) Active = id;
        }
    }
    private static int passed;
    private static readonly string Output = AppDomain.CurrentDomain.BaseDirectory;
    private static T Field<T>(object target, string name)
    { return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target); }
    private static Control Find(Control root, string name) { return root.Controls.Find(name, true).Single(); }
    private static PowerPlanCard[] Cards(MainForm form)
    { return Find(form, "plans").Controls.OfType<PowerPlanCard>().ToArray(); }
    private static bool Idle(MainForm form) { return !form.IsDisposed && Find(form, "refresh").Enabled; }
    private static async Task Until(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition()) { if (watch.ElapsedMilliseconds > 6000) throw new Exception("UI operation timed out"); await Task.Delay(20); }
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Pass(string name) { passed++; Console.WriteLine("PASS " + name); }
    private static void Run(FakeApi api, bool preview, string settingsPath, Theme theme, Func<MainForm, Task> body)
    {
        Exception failure = null;
        using (var form = new MainForm(new PowerPlanService(api), false, preview, settingsPath, theme))
        {
            form.Shown += async delegate
            {
                try { await Until(() => form.InitialLoadComplete && Idle(form)); await body(form); }
                catch (Exception ex) { failure = ex; }
                finally { if (!form.IsDisposed) form.ExitApplication(); }
            };
            Application.Run(form);
        }
        if (failure != null) throw failure;
    }
    private static void CheckLayout(MainForm form)
    {
        var cards = Cards(form);
        Assert(form.FormBorderStyle == FormBorderStyle.Sizable && form.MaximizeBox && form.MinimizeBox, "Native window management lost");
        foreach (var card in cards) Assert(card.Width > 250 && card.Height > 50, "Invalid card bounds");
        for (int i = 1; i < cards.Length; i++) Assert(cards[i].Top >= cards[i - 1].Bottom, "Cards overlap");
        foreach (string name in new[] { "hotkeys", "closeToTray" })
        {
            Control toggle = Find(form, name);
            Assert(toggle.Left >= 0 && toggle.Right <= toggle.Parent.ClientSize.Width && toggle.Top >= 0 && toggle.Bottom <= toggle.Parent.ClientSize.Height, "Toggle outside row");
            Control row = toggle.Parent;
            Assert(form.RectangleToClient(row.RectangleToScreen(row.ClientRectangle)).Bottom < form.ClientSize.Height, "Settings outside window");
        }
        Assert(Find(form, "status").Height > 20, "Status has no room for errors");
    }

    [STAThread]
    private static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        string temporary = Path.Combine(Output, "ui-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        var dark = new Theme(true, false, Color.FromArgb(96, 205, 255));
        try
        {
            string legacyPath = Path.Combine(temporary, "legacy.ini");
            File.WriteAllText(legacyPath, "hotkeys=1\nother=value\n");
            AppSettings legacy = AppSettings.Load(legacyPath);
            Assert(legacy.HotkeysEnabled && legacy.CloseToTray, "Legacy preference migration failed");
            legacy.CloseToTray = false; legacy.Save(legacyPath);
            Assert(AppSettings.Load(legacyPath).HotkeysEnabled && !AppSettings.Load(legacyPath).CloseToTray && File.ReadAllText(legacyPath).Contains("other=value"), "Settings round-trip failed");
            Pass("Legacy settings, both persisted options, unknown keys preserved");

            string settingsPath = Path.Combine(temporary, "settings.ini");
            var api = new FakeApi();
            Run(api, true, settingsPath, dark, async form =>
            {
                Assert(Cards(form).Length == 4 && Cards(form)[0].Plan.IsActive, "Initial plans incorrect");
                CheckLayout(form); form.SavePreview(Path.Combine(Output, "fluent-test-dark.png"));
                Pass("Startup, four cards, native frame and current-display-DPI layout (" + form.DeviceDpi + " DPI)");
                var stateCard = Cards(form)[1];
                Assert(stateCard.VisualState == CardVisualState.Normal && Cards(form)[0].VisualState == CardVisualState.Selected, "Normal/selected state wrong");
                typeof(PowerPlanCard).GetMethod("OnMouseEnter", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stateCard, new object[] { EventArgs.Empty });
                Assert(stateCard.VisualState == CardVisualState.Hover, "Hover state wrong");
                typeof(PowerPlanCard).GetMethod("OnMouseLeave", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(stateCard, new object[] { EventArgs.Empty });
                stateCard.Enabled = false; Assert(stateCard.VisualState == CardVisualState.Disabled, "Disabled state wrong");
                stateCard.Switching = true; Assert(stateCard.VisualState == CardVisualState.Switching, "Switching state wrong");
                stateCard.Switching = false; stateCard.Enabled = true;
                Assert((Cards(form)[0].AccessibilityObject.State & AccessibleStates.Checked) != 0, "Selection not accessible");
                Pass("All five card visual states and accessible checked state");
                string[] icons = { "💻", "🌙", "🚀", "🌿" };
                for (int i = 0; i < 4; i++) Assert(form.SavePlanIcon(api.Ids[i], icons[i]) == null, "Saving custom icon failed");
                Assert(Cards(form).Select(c => c.CustomIcon).SequenceEqual(icons) && api.Writes == 0, "Icon edit changed power state or did not update UI");
                form.SavePreview(Path.Combine(Output, "emoji-plans-preview.png"));
                Assert(form.SavePlanIcon(api.Ids[0], "") == null && Cards(form)[0].CustomIcon == "" && Cards(form)[1].CustomIcon == "🌙", "Reset affected another plan");
                form.SavePlanIcon(api.Ids[0], icons[0]);
                Pass("Main window saves per-GUID emoji, updates cards and resets independently without power writes");
                for (int i = 1; i <= 4; i++)
                {
                    int index = i % 4; var target = Cards(form)[index];
                    int before = api.Writes;
                    target.PerformClick();
                    Assert(target.VisualState == CardVisualState.Switching && Cards(form).All(c => !c.Enabled), "No switching guard");
                    target.PerformClick();
                    await Until(() => Idle(form));
                    Assert(api.Writes == before + 1 && Cards(form)[index].Plan.IsActive && Cards(form).Count(c => c.Plan.IsActive) == 1, "Switch state wrong");
                }
                int writes = api.Writes; Cards(form)[0].PerformClick();
                Assert(api.Writes == writes, "Active-plan click wrote to backend");
                Pass("All four switch paths, busy state, duplicate-click guard and confirmed selection (fake API)");

                api.Fail = true; Cards(form)[1].PerformClick(); await Until(() => Idle(form));
                Assert(Cards(form)[0].Plan.IsActive && Find(form, "status").Text.Contains("访问被拒绝") && Cards(form).All(c => c.Enabled && !c.Switching), "Failure state not restored");
                api.Fail = false; api.Ignore = true; Cards(form)[1].PerformClick(); await Until(() => Idle(form));
                Assert(Cards(form)[0].Plan.IsActive && Find(form, "status").Text.Contains("组织策略"), "Policy override not detected");
                api.Ignore = false; api.ReadFails = true;
                ((Button)Find(form, "refresh")).PerformClick(); await Until(() => Idle(form));
                Assert(Cards(form)[0].Plan.IsActive && Find(form, "status").Text.Contains("读取当前方案失败"), "Read error lost old selection");
                api.ReadFails = false; api.Active = api.Ids[3];
                ((Button)Find(form, "refresh")).PerformClick(); await Until(() => Idle(form));
                Assert(Cards(form)[3].Plan.IsActive && Find(form, "activeName").Text.Contains("节能"), "Refresh did not synchronize backend");
                Pass("Access-denied, policy override, read failure, control recovery and external-state refresh (fake API)");

                form.WindowState = FormWindowState.Minimized; await Task.Delay(80);
                Assert(form.WindowState == FormWindowState.Minimized && !form.IsDisposed, "Native minimize failed");
                form.ShowWindow(); await Until(() => Idle(form));
                Assert(form.WindowState == FormWindowState.Normal && form.Visible, "Restore failed");
                form.Close(); Assert(!form.Visible && !form.IsDisposed, "Close-to-tray failed");
                var menu = Field<NotifyIcon>(form, "tray").ContextMenuStrip;
                menu.Items.OfType<ToolStripMenuItem>().Single(item => item.Text == "打开主窗口").PerformClick();
                await Until(() => Idle(form)); Assert(form.Visible, "Tray reopen handler failed");
                Pass("Native minimize/restore, close-to-tray and tray reopen handler");

                ((ToggleSwitch)Find(form, "hotkeys")).Checked = true;
                ((ToggleSwitch)Find(form, "closeToTray")).Checked = false;
                Assert(AppSettings.Load(settingsPath).HotkeysEnabled && !AppSettings.Load(settingsPath).CloseToTray, "Toggles not persisted");
                Pass("Both settings persist on disk");
            });
            Run(new FakeApi(), true, settingsPath, dark, form =>
            {
                Assert(((ToggleSwitch)Find(form, "hotkeys")).Checked && !((ToggleSwitch)Find(form, "closeToTray")).Checked, "Settings not restored after restart");
                Assert(Cards(form).Select(c => c.CustomIcon).SequenceEqual(new[] { "💻", "🌙", "🚀", "🌿" }), "Custom icons not restored after restart");
                form.Close(); Assert(form.IsDisposed, "Close-to-exit did not dispose form");
                Pass("Restart restores preferences; close-to-exit disposes form"); return Task.FromResult(0);
            });

            Run(new FakeApi(), true, temporary, dark, form =>
            {
                ((ToggleSwitch)Find(form, "hotkeys")).Checked = true;
                Assert(!((ToggleSwitch)Find(form, "hotkeys")).Checked && Find(form, "status").Text.Contains("设置未保存"), "Failed save left fake enabled preference");
                Assert(form.SavePlanIcon(Cards(form)[0].Plan.Id, "😀") != null && Cards(form)[0].CustomIcon == "", "Failed save replaced confirmed icon");
                Pass("Unwritable settings destination rolls toggle back and shows error"); return Task.FromResult(0);
            });

            var hotkeyApi = new FakeApi();
            var acquired = new Dictionary<int, Guid>();
            Run(hotkeyApi, false, Path.Combine(temporary, "hotkeys.ini"), dark, async form =>
            {
                ((ToggleSwitch)Find(form, "hotkeys")).Checked = true;
                foreach (var pair in Field<Dictionary<int, Guid>>(form, "keyPlans")) acquired.Add(pair.Key, pair.Value);
                Assert(acquired.Count + Cards(form).Count(c => c.Shortcut == "快捷键占用" || c.Shortcut == "快捷键不可用") == 4, "Keys neither registered nor reported");
                foreach (var pair in acquired) Assert(hotkeyApi.Ids[pair.Key - 8100] == pair.Value, "Hotkey mapping changed");
                Pass("Native global hotkey registration result/error handling (" + acquired.Count + "/4 available)");
                if (acquired.Count == 0) Console.WriteLine("SKIP actual hotkey dispatch/release: " + Find(form, "status").Text);
                Assert(Field<NotifyIcon>(form, "tray").Visible, "Tray icon not enabled");
                var key = acquired.FirstOrDefault(p => p.Value != hotkeyApi.Active);
                if (key.Key != 0)
                {
                    NativeMethods.PostMessage(form.Handle, 0x0312, (IntPtr)key.Key, IntPtr.Zero);
                    await Until(() => hotkeyApi.Writes > 0 && Idle(form));
                    Assert(Cards(form).Single(c => c.Plan.Id == key.Value).Plan.IsActive, "WM_HOTKEY not dispatched");
                    Pass("WM_HOTKEY message dispatch to fake backend (no physical keystroke injected)");
                }
                if (acquired.Count == 4)
                {
                    typeof(Control).GetMethod("RecreateHandle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, null);
                    Assert(Field<Dictionary<int, Guid>>(form, "keyPlans").Count == 4, "Keys not restored after native handle recreation");
                    Pass("Handle recreation re-registers original hotkey mappings");
                }
                var trayMenu = Field<NotifyIcon>(form, "tray").ContextMenuStrip;
                var item = trayMenu.Items.OfType<ToolStripMenuItem>().First(m => m.Tag is Guid && (Guid)m.Tag != hotkeyApi.Active);
                Guid selected = (Guid)item.Tag; item.PerformClick(); await Until(() => Idle(form));
                Assert(hotkeyApi.Active == selected && Cards(form).Single(c => c.Plan.Id == selected).Plan.IsActive, "Tray selection failed");
                Pass("Tray plan-menu selection handler (fake backend)");
                ((ToggleSwitch)Find(form, "hotkeys")).Checked = false;
                foreach (var pair in acquired)
                {
                    Assert(NativeMethods.RegisterHotKey(IntPtr.Zero, pair.Key + 500, 0x4003, (uint)Keys.D1 + (uint)(pair.Key - 8100)), "Disabling did not release key");
                    NativeMethods.UnregisterHotKey(IntPtr.Zero, pair.Key + 500);
                }
                ((ToggleSwitch)Find(form, "hotkeys")).Checked = true;
                if (acquired.Count > 0) Pass("Disabling toggle releases registered global keys");
            });
            foreach (var pair in acquired)
            {
                Assert(NativeMethods.RegisterHotKey(IntPtr.Zero, pair.Key + 500, 0x4003, (uint)Keys.D1 + (uint)(pair.Key - 8100)), "Exiting did not release key");
                NativeMethods.UnregisterHotKey(IntPtr.Zero, pair.Key + 500);
            }
            if (acquired.Count > 0) Pass("Normal exit releases registered global keys");

            bool reserved = NativeMethods.RegisterHotKey(IntPtr.Zero, 7999, 0x4003, (uint)Keys.D1);
            if (reserved)
            {
                try
                {
                    Run(new FakeApi(), false, Path.Combine(temporary, "conflict.ini"), dark, form =>
                    {
                        ((ToggleSwitch)Find(form, "hotkeys")).Checked = true;
                        Assert(Cards(form)[0].Shortcut == "快捷键占用" && Find(form, "status").Text.Contains("快捷键注册失败"), "Conflict not shown");
                        Pass("Actual global-hotkey conflict reported without changing mappings"); return Task.FromResult(0);
                    });
                }
                finally { NativeMethods.UnregisterHotKey(IntPtr.Zero, 7999); }
            }

            foreach (int dpi in new[] { 96, 120, 144, 192 })
            {
                Run(new FakeApi(), true, Path.Combine(temporary, "dpi.ini"), dark, form =>
                {
                    // Exercise proportional WinForms layout geometry, without changing OS monitor DPI.
                    float factor = dpi / (float)form.DeviceDpi;
                    form.Scale(new SizeF(factor, factor));
                    form.ClientSize = new Size((int)(560 * dpi / 96f), (int)(692 * dpi / 96f));
                    form.PerformLayout(); CheckLayout(form);
                    foreach (var card in Cards(form)) Assert(Math.Abs(card.Height - 80 * dpi / 96f) < 5, "Card height not scaled");
                    Pass("Simulated " + dpi + " DPI layout geometry (not a real monitor-DPI test)"); return Task.FromResult(0);
                });
            }
            foreach (var palette in new[] { new Theme(false, false, Color.FromArgb(0, 95, 184)), new Theme(true, true, Color.Blue) })
            {
                Run(new FakeApi(), true, Path.Combine(temporary, "theme.ini"), palette, form =>
                {
                    CheckLayout(form);
                    Assert(Cards(form).All(c => c.Palette == palette), "Theme not propagated");
                    form.SavePreview(Path.Combine(Output, palette.HighContrast ? "fluent-test-contrast.png" : "fluent-test-light.png"));
                    NativeMethods.PostMessage(form.Handle, 0x031A, IntPtr.Zero, IntPtr.Zero);
                    Pass(palette.HighContrast ? "High-contrast palette render (simulated system palette)" : "Light-theme render");
                    return Task.FromResult(0);
                });
            }
            Run(new FakeApi(), true, Path.Combine(temporary, "system-theme.ini"), null, async form =>
            {
                Theme previous = Field<Theme>(form, "theme");
                NativeMethods.PostMessage(form.Handle, 0x031A, IntPtr.Zero, IntPtr.Zero);
                await Until(() => !Object.ReferenceEquals(previous, Field<Theme>(form, "theme")));
                Theme system = Theme.ReadSystem();
                Assert(Field<Theme>(form, "theme").Dark == system.Dark && Field<Theme>(form, "theme").Accent == system.Accent, "System personalization not read");
                Pass("Theme notification re-reads actual Windows theme and accent");
            });
            Console.WriteLine("All " + passed + " UI/settings checks passed. Real Windows power plans were NOT changed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL " + ex); return 1; }
    }
}
