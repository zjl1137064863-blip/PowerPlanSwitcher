using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PowerPlanSwitcher
{
    internal static class NativeMethods
    {
        internal static readonly uint ShowMessage = RegisterWindowMessage("PowerPlanSwitcher.Show.7D43B3AE");
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll")]
        internal static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
        [DllImport("user32.dll")]
        internal static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        internal static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wparam, IntPtr lparam);
        [DllImport("user32.dll")]
        internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")]
        internal static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll")]
        internal static extern bool DestroyIcon(IntPtr icon);
    }

    internal static class Theme
    {
        internal static readonly Color Background = Color.FromArgb(18, 23, 30);
        internal static readonly Color Surface = Color.FromArgb(27, 34, 43);
        internal static readonly Color Muted = Color.FromArgb(144, 158, 173);
        internal static readonly Color Text = Color.FromArgb(235, 241, 246);
        internal static readonly Color Accent = Color.FromArgb(123, 232, 195);
        internal static Font Font(float size, FontStyle style = FontStyle.Regular)
        { return new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point); }
        internal static GraphicsPath Round(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class FlatButton : Button
    {
        private bool hover;
        internal FlatButton(string text)
        {
            Text = text; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            BackColor = Theme.Background; ForeColor = Theme.Muted; Font = Theme.Font(9);
            Cursor = Cursors.Hand; SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(hover ? Theme.Surface : BackColor);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle,
                Enabled ? (hover ? Theme.Text : ForeColor) : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -3, -3));
        }
    }

    internal sealed class PlanButton : Button
    {
        internal PowerPlan Plan { get; private set; }
        internal string Shortcut { get; set; }
        private readonly float scale;
        private bool hover;
        internal PlanButton(PowerPlan plan, float scale)
        {
            this.scale = scale; Plan = plan; Shortcut = "点击切换";
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            BackColor = Theme.Background; Cursor = Cursors.Hand;
            AccessibleName = plan.Name + (plan.IsActive ? "，当前计划" : "，切换到此计划");
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        private int S(float value) { return (int)Math.Round(value * scale); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(BackColor);
            RectangleF bounds = new RectangleF(1, 1, Width - 3, Height - 3);
            using (var path = Theme.Round(bounds, S(12)))
            using (var brush = new SolidBrush(Plan.IsActive ? Color.FromArgb(30, 48, 46) : hover ? Color.FromArgb(34, 43, 54) : Theme.Surface))
            using (var pen = new Pen(Plan.IsActive ? Theme.Accent : Color.FromArgb(43, 53, 65), Plan.IsActive ? S(1.5f) : 1))
            { g.FillPath(brush, path); g.DrawPath(pen, path); }
            Color tint = GetTint(Plan);
            using (var brush = new SolidBrush(Color.FromArgb(36, tint)))
            using (var path = Theme.Round(new RectangleF(S(17), S(18), S(44), S(44)), S(12)))
                g.FillPath(brush, path);
            DrawSymbol(g, new Rectangle(S(29), S(29), S(21), S(22)), Plan, tint, scale);
            using (var font = Theme.Font(11, FontStyle.Bold))
                TextRenderer.DrawText(g, Plan.Name, font, new Rectangle(S(76), S(15), Width - S(185), S(28)), Theme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            using (var font = Theme.Font(8.5f))
                TextRenderer.DrawText(g, Description(Plan), font, new Rectangle(S(76), S(43), Width - S(105), S(24)), Theme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            using (var font = Theme.Font(8))
                TextRenderer.DrawText(g, Plan.IsActive ? "● 使用中" : Shortcut, font,
                    new Rectangle(Width - S(111), S(18), S(96), S(24)), Plan.IsActive ? Theme.Accent : Theme.Muted,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            if (Focused) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -S(6), -S(6)), Theme.Accent, BackColor);
        }
        private static int Kind(PowerPlan plan)
        {
            string name = plan.Name.ToLowerInvariant();
            if (plan.Id == new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c") || name.Contains("性能") || name.Contains("performance")) return 1;
            if (plan.Id == new Guid("a1841308-3541-4fab-bc81-f71556f20b4a") || name.Contains("节能") || name.Contains("saver")) return 2;
            if (name.Contains("静音") || name.Contains("silent")) return 3;
            if (plan.Id == new Guid("381b4222-f694-41f0-9685-ff5bb260df2e")) return 0;
            return 4;
        }
        private static Color GetTint(PowerPlan plan)
        {
            switch (Kind(plan))
            {
                case 1: return Color.FromArgb(255, 187, 128);
                case 2: return Theme.Accent;
                case 3: return Color.FromArgb(190, 171, 244);
                default: return Color.FromArgb(134, 189, 249);
            }
        }
        private static string Description(PowerPlan plan)
        {
            switch (Kind(plan))
            {
                case 0: return "兼顾日常性能与能源消耗";
                case 1: return "性能优先，适合高负载任务";
                case 2: return "节约用电，延长电池续航";
                default: return "使用此计划在 Windows 中的配置";
            }
        }
        internal static void DrawSymbol(Graphics g, Rectangle r, PowerPlan plan, Color tint, float scale)
        {
            using (var pen = new Pen(tint, 1.7f * scale))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                int kind = Kind(plan);
                if (kind == 1)
                {
                    var points = new PointF[] { new PointF(r.X + r.Width * .6f, r.Y), new PointF(r.X + r.Width * .12f, r.Y + r.Height * .55f),
                        new PointF(r.X + r.Width * .49f, r.Y + r.Height * .55f), new PointF(r.X + r.Width * .37f, r.Bottom),
                        new PointF(r.Right, r.Y + r.Height * .38f), new PointF(r.X + r.Width * .6f, r.Y + r.Height * .38f), new PointF(r.X + r.Width * .6f, r.Y) };
                    g.DrawLines(pen, points);
                }
                else if (kind == 2)
                {
                    g.DrawArc(pen, r, 0, 300);
                    g.DrawLine(pen, r.X + r.Width / 4, r.Bottom, r.Right - r.Width / 4, r.Y + r.Height / 3);
                }
                else if (kind == 3)
                {
                    g.DrawArc(pen, r, 60, 285);
                    g.DrawArc(pen, new Rectangle(r.X + r.Width / 3, r.Y - r.Height / 5, r.Width, r.Height), 85, 145);
                }
                else
                {
                    g.DrawLine(pen, r.X, r.Y + r.Height / 3, r.Right, r.Y + r.Height / 3);
                    g.DrawLine(pen, r.X, r.Y + r.Height * 2 / 3, r.Right, r.Y + r.Height * 2 / 3);
                    g.DrawEllipse(pen, r.X + r.Width / 4, r.Y + r.Height / 3 - 3 * scale, 6 * scale, 6 * scale);
                    g.DrawEllipse(pen, r.X + r.Width * 2 / 3, r.Y + r.Height * 2 / 3 - 3 * scale, 6 * scale, 6 * scale);
                }
            }
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly PowerPlanService service;
        private readonly bool startInTray, preview;
        private readonly float scale;
        private readonly NotifyIcon tray;
        private readonly Icon appIcon;
        private readonly FlowLayoutPanel list;
        private readonly Label activeName, status, countLabel;
        private readonly CheckBox hotkeys;
        private readonly FlatButton refresh;
        private readonly Timer sync;
        private readonly List<int> registeredKeys = new List<int>();
        private readonly Dictionary<int, Guid> keyPlans = new Dictionary<int, Guid>();
        private IList<PowerPlan> plans = new List<PowerPlan>();
        private bool busy, exiting, hadHiddenTip;
        private string signature = "";
        private readonly string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");

        internal MainForm(PowerPlanService service, bool startInTray, bool preview)
        {
            this.service = service; this.startInTray = startInTray; this.preview = preview;
            using (Graphics g = CreateGraphics()) scale = g.DpiX / 96f;
            Text = "电源计划切换器"; BackColor = Theme.Background; ForeColor = Theme.Text;
            Font = Theme.Font(9); AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(S(560), S(806));
            if (Height > Screen.PrimaryScreen.WorkingArea.Height - S(20))
                Height = Screen.PrimaryScreen.WorkingArea.Height - S(20);
            DoubleBuffered = true;
            appIcon = CreateIcon(); Icon = appIcon;

            Label brand = Label("◉  POWER SWITCH", 10, Theme.Accent, FontStyle.Bold);
            Place(brand, 28, 20, 300, 26);
            brand.MouseDown += DragWindow; MouseDown += DragWindow;
            FlatButton hide = Button("─", 470, 17, 28, 28, delegate { HideToTray(); });
            hide.AccessibleName = "最小化到托盘";
            FlatButton close = Button("×", 505, 17, 28, 28, delegate { HideToTray(); });
            close.AccessibleName = "关闭窗口并驻留托盘";
            Place(Label("电源计划", 25, Theme.Text, FontStyle.Bold), 27, 65, 380, 49);
            Place(Label("在性能与续航之间，一键切换。", 9.5f, Theme.Muted), 29, 117, 410, 25);
            refresh = Button("↻  刷新", 454, 119, 76, 28, async delegate { await RefreshPlans(true); });

            var hero = new Panel { BackColor = Color.FromArgb(27, 43, 42) };
            Place(hero, 29, 163, 502, 92);
            var activeTitle = Label("●  正在使用", 8.5f, Theme.Accent);
            activeTitle.SetBounds(S(17), S(12), S(250), S(23)); hero.Controls.Add(activeTitle);
            activeName = Label("读取中…", 18, Theme.Text, FontStyle.Bold);
            activeName.SetBounds(S(14), S(39), S(450), S(39)); hero.Controls.Add(activeName);

            Place(Label("选择电源计划", 10, Theme.Text, FontStyle.Bold), 28, 274, 250, 28);
            countLabel = Label("", 8.5f, Theme.Muted); countLabel.TextAlign = ContentAlignment.MiddleRight;
            Place(countLabel, 412, 274, 118, 28);
            list = new FlowLayoutPanel { AutoScroll = true, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, BackColor = Theme.Background, Padding = Padding.Empty };
            Place(list, 28, 313, 518, Height / scale - 433);
            list.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            list.SizeChanged += delegate { SizeCards(); };

            hotkeys = new CheckBox { Text = "启用快捷键  Ctrl + Alt + 1 ~ 9", AutoSize = false,
                ForeColor = Theme.Muted, Font = Theme.Font(8.5f), BackColor = Theme.Background,
                Cursor = Cursors.Hand, AccessibleName = "启用全局快捷键 Ctrl 加 Alt 加数字 1 到 9" };
            hotkeys.SetBounds(S(30), Height - S(112), S(480), S(28));
            hotkeys.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; Controls.Add(hotkeys);
            try { hotkeys.Checked = !preview && File.Exists(settingsPath) && File.ReadAllText(settingsPath).Trim() == "hotkeys=1"; }
            catch { hotkeys.Checked = false; }
            hotkeys.CheckedChanged += delegate
            {
                ConfigureHotkeys();
                try { File.WriteAllText(settingsPath, hotkeys.Checked ? "hotkeys=1" : "hotkeys=0"); }
                catch (Exception ex) { ShowStatus("快捷键已更新，但无法保存偏好：" + ex.Message, true); }
            };
            status = Label("正在读取 Windows 电源计划…", 8, Theme.Muted);
            status.SetBounds(S(28), Height - S(77), S(504), S(25));
            status.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; Controls.Add(status);
            var tip = Label("关闭窗口后继续驻留托盘", 8, Theme.Muted);
            tip.SetBounds(S(28), Height - S(38), S(350), S(24));
            tip.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; Controls.Add(tip);
            FlatButton exit = Button("退出程序", 454, Height / scale - 40, 78, 28, delegate { ExitApplication(); });
            exit.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;

            tray = new NotifyIcon { Icon = appIcon, Text = "电源计划切换器", Visible = !preview };
            tray.MouseClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowWindow(); };
            tray.ContextMenuStrip = BuildMenu();
            sync = new Timer { Interval = 5000 };
            sync.Tick += async delegate { if (!busy) await RefreshPlans(false); };
            Shown += async delegate
            {
                if (startInTray && !preview) Hide();
                await RefreshPlans(true); sync.Start();
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; HideToTray(); }
            };
        }

        private int S(float value) { return (int)Math.Round(value * scale); }
        private Label Label(string text, float size, Color color, FontStyle style = FontStyle.Regular)
        {
            return new Label { Text = text, Font = Theme.Font(size, style), ForeColor = color,
                BackColor = Color.Transparent, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        }
        private void Place(Control control, float x, float y, float width, float height)
        { control.SetBounds(S(x), S(y), S(width), S(height)); Controls.Add(control); }
        private FlatButton Button(string text, float x, float y, float width, float height, EventHandler click)
        {
            var button = new FlatButton(text); Place(button, x, y, width, height); button.Click += click; return button;
        }
        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            NativeMethods.ReleaseCapture(); NativeMethods.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(44, 54, 65)))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                e.Graphics.DrawLine(pen, S(28), Height - S(45), Width - S(28), Height - S(45));
            }
        }
        private static Icon CreateIcon()
        {
            using (var bitmap = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
                using (var brush = new SolidBrush(Color.FromArgb(28, 48, 45))) g.FillEllipse(brush, 1, 1, 30, 30);
                using (var pen = new Pen(Theme.Accent, 2.8f))
                {
                    pen.StartCap = pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, 8, 8, 16, 17, -52, 284); g.DrawLine(pen, 16, 5, 16, 16);
                }
                IntPtr handle = bitmap.GetHicon();
                try { using (var original = Icon.FromHandle(handle)) return (Icon)original.Clone(); }
                finally { NativeMethods.DestroyIcon(handle); }
            }
        }

        private async Task RefreshPlans(bool explicitRefresh)
        {
            if (busy || exiting) return;
            busy = true; refresh.Enabled = false;
            try
            {
                IList<PowerPlan> fresh = await Task.Run(() => service.GetPlans());
                if (exiting || IsDisposed) return;
                string next = String.Join("|", fresh.Select(p => p.Id + ":" + p.Name + ":" + p.IsActive));
                bool changed = next != signature;
                if (changed)
                {
                    signature = next; plans = fresh; RenderPlans();
                    PowerPlan active = plans.FirstOrDefault(p => p.IsActive);
                    activeName.Text = active == null ? "未知计划，请刷新" : active.Name;
                    string tooltip = "电源计划：" + (active == null ? "未知" : active.Name);
                    tray.Text = tooltip.Length > 63 ? tooltip.Substring(0, 63) : tooltip;
                    countLabel.Text = plans.Count + " 个可用";
                    ReplaceMenu(); ConfigureHotkeys();
                }
                if (explicitRefresh || changed) ShowStatus("已同步 Windows · " + DateTime.Now.ToString("HH:mm:ss"), false);
            }
            catch (Exception ex) { if (!exiting) ShowStatus(ex.Message, true); }
            finally { busy = false; if (!exiting && !IsDisposed) refresh.Enabled = true; }
        }

        private void RenderPlans()
        {
            list.SuspendLayout();
            foreach (Control control in list.Controls.Cast<Control>().ToArray()) control.Dispose();
            for (int i = 0; i < plans.Count; i++)
            {
                PowerPlan plan = plans[i];
                var button = new PlanButton(plan, scale) { Height = S(82), Width = S(501),
                    Margin = new Padding(0, 0, 0, S(9)), TabIndex = i };
                button.Click += async delegate { await SwitchPlan(plan.Id); };
                list.Controls.Add(button);
            }
            list.ResumeLayout(); SizeCards();
        }
        private void SizeCards()
        {
            int width = Math.Min(S(502), list.ClientSize.Width - S(3));
            foreach (Control card in list.Controls) card.Width = Math.Max(S(200), width);
        }

        private async Task SwitchPlan(Guid id)
        {
            if (busy || exiting) return;
            PowerPlan selected = plans.FirstOrDefault(p => p.Id == id);
            if (selected == null) return;
            if (selected.IsActive) { ShowStatus("当前已使用「" + selected.Name + "」", false); return; }
            busy = true; refresh.Enabled = false; list.Enabled = false;
            ShowStatus("正在切换到「" + selected.Name + "」…", false);
            Exception failure = null;
            try { await Task.Run(() => service.Activate(id)); }
            catch (Exception ex) { failure = ex; }
            finally { busy = false; if (!exiting && !IsDisposed) { list.Enabled = true; refresh.Enabled = true; } }
            if (exiting || IsDisposed) return;
            await RefreshPlans(false);
            if (failure != null)
            {
                ShowStatus(failure.Message, true);
                if (!Visible) tray.ShowBalloonTip(4000, "切换失败", failure.Message, ToolTipIcon.Error);
            }
            else
            {
                ShowStatus("已切换到「" + selected.Name + "」 · " + DateTime.Now.ToString("HH:mm:ss"), false);
                if (!Visible) tray.ShowBalloonTip(1600, "电源计划已切换", selected.Name, ToolTipIcon.Info);
            }
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip { Font = Theme.Font(9), ShowImageMargin = false };
            menu.Items.Add(new ToolStripMenuItem("电源计划切换器") { Enabled = false });
            menu.Items.Add(new ToolStripSeparator());
            foreach (PowerPlan plan in plans)
            {
                Guid id = plan.Id;
                var item = new ToolStripMenuItem(plan.Name) { Checked = plan.IsActive, CheckOnClick = false, Tag = id };
                item.Click += async delegate { await SwitchPlan(id); };
                menu.Items.Add(item);
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("打开主窗口", null, delegate { ShowWindow(); });
            menu.Items.Add("刷新计划", null, async delegate { await RefreshPlans(true); });
            menu.Items.Add("退出程序", null, delegate { ExitApplication(); });
            menu.Opening += delegate { foreach (ToolStripItem item in menu.Items) if (item.Tag is Guid) item.Enabled = !busy; };
            return menu;
        }
        private void ReplaceMenu()
        {
            ContextMenuStrip previous = tray.ContextMenuStrip;
            // Never dispose the menu while Windows is still tracking a user's selection.
            if (previous != null && previous.Visible)
            {
                previous.Closed += MenuClosed;
                return;
            }
            tray.ContextMenuStrip = BuildMenu();
            if (previous != null) previous.Dispose();
        }
        private void MenuClosed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            ((ContextMenuStrip)sender).Closed -= MenuClosed;
            if (!exiting && IsHandleCreated) BeginInvoke((Action)ReplaceMenu);
        }
        private void ConfigureHotkeys()
        {
            foreach (int id in registeredKeys) NativeMethods.UnregisterHotKey(Handle, id);
            registeredKeys.Clear(); keyPlans.Clear();
            var conflicts = new List<string>();
            int index = 0;
            foreach (PlanButton card in list.Controls.OfType<PlanButton>())
            {
                card.Shortcut = "点击切换";
                if (hotkeys.Checked && index < 9 && !preview)
                {
                    int id = 8100 + index;
                    // MOD_NOREPEAT | MOD_CONTROL | MOD_ALT.
                    if (NativeMethods.RegisterHotKey(Handle, id, 0x4003, (uint)Keys.D1 + (uint)index))
                    {
                        registeredKeys.Add(id); keyPlans[id] = card.Plan.Id;
                        card.Shortcut = "Ctrl+Alt+" + (index + 1);
                    }
                    else { conflicts.Add((index + 1).ToString()); card.Shortcut = "快捷键占用"; }
                }
                card.Invalidate(); index++;
            }
            if (conflicts.Count > 0) ShowStatus("快捷键被占用：Ctrl+Alt+" + String.Join(" / ", conflicts) + "；仍可点击切换。", true);
        }
        private void ShowStatus(string message, bool error)
        {
            status.Text = message; status.ForeColor = error ? Color.FromArgb(255, 174, 149) : Theme.Muted;
            var tip = status.Tag as ToolTip;
            if (tip == null) { tip = new ToolTip(); status.Tag = tip; }
            tip.SetToolTip(status, message);
        }
        private void ShowWindow()
        {
            Show(); WindowState = FormWindowState.Normal; Activate();
            var ignored = RefreshPlans(false);
        }
        private void HideToTray()
        {
            Hide();
            if (!hadHiddenTip)
            {
                hadHiddenTip = true;
                tray.ShowBalloonTip(2000, "已在托盘运行", "右键电源图标即可切换计划，左键打开窗口。", ToolTipIcon.Info);
            }
        }
        internal void ExitApplication()
        {
            exiting = true; sync.Stop(); tray.Visible = false; Close();
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == NativeMethods.ShowMessage) { ShowWindow(); return; }
            if (message.Msg == 0x0312)
            {
                Guid id;
                if (keyPlans.TryGetValue(message.WParam.ToInt32(), out id)) { var ignored = SwitchPlan(id); }
            }
            base.WndProc(ref message);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (int id in registeredKeys) NativeMethods.UnregisterHotKey(Handle, id);
                if (sync != null) sync.Dispose();
                if (tray != null) { if (tray.ContextMenuStrip != null) tray.ContextMenuStrip.Dispose(); tray.Dispose(); }
                if (appIcon != null) appIcon.Dispose();
                if (status != null && status.Tag is ToolTip) ((ToolTip)status.Tag).Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
