using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PowerPlanSwitcher
{
    internal sealed class MainForm : Form
    {
        private readonly PowerPlanService service;
        private readonly bool startInTray, preview;
        private readonly NotifyIcon tray;
        private readonly Icon appIcon;
        private readonly TableLayoutPanel root, list;
        private readonly Label activeName, status, sectionTitle;
        private readonly SettingRow hotkeyRow, trayRow;
        private readonly ToggleSwitch hotkeys, closeToTray;
        private readonly Button refresh;
        private readonly Timer sync;
        private readonly ToolTip tips = new ToolTip();
        private readonly List<int> registeredKeys = new List<int>();
        private readonly Dictionary<int, Guid> keyPlans = new Dictionary<int, Guid>();
        private readonly string settingsPath;
        private readonly Theme themeOverride;
        private AppSettings settings;
        private Theme theme;
        private IList<PowerPlan> plans = new List<PowerPlan>();
        private bool busy, exiting, resourcesDisposed, restoringSettings, hadHiddenTip, statusError, menuUpdatePending;
        private string signature = "", catalogSignature = "", startupWarning = "", hotkeyWarning = "";
        internal bool InitialLoadComplete { get; private set; }

        internal MainForm(PowerPlanService service, bool startInTray, bool preview,
            string settingsFile = null, Theme themeOverride = null)
        {
            this.service = service; this.startInTray = startInTray; this.preview = preview;
            this.themeOverride = themeOverride; theme = themeOverride ?? Theme.ReadSystem();
            settingsPath = settingsFile ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini");
            try { settings = AppSettings.Load(settingsPath); }
            catch (Exception ex) { settings = new AppSettings(); startupWarning = "无法读取偏好设置：" + ex.Message; }

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            Font = Theme.Font(9); Text = "Power Switch";
            FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(560, 692);
            MinimumSize = new Size(460, 510); DoubleBuffered = true;
            appIcon = CreateIcon(); Icon = appIcon;

            root = new TableLayoutPanel { Name = "layout", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6,
                Padding = new Padding(20), Margin = Padding.Empty, TabStop = false };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (float height in new[] { 80f, 36f, 0f, 32f, 112f, 48f })
                root.RowStyles.Add(new RowStyle(height == 0 ? SizeType.Percent : SizeType.Absolute, height == 0 ? 100 : height));
            Controls.Add(root);

            var heading = Table(2, 3);
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
            heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            heading.Controls.Add(Label("◉  Power Switch", 9, true), 0, 0);
            refresh = Button("刷新", "refresh");
            refresh.Click += async delegate { await RefreshPlans(true); };
            heading.Controls.Add(refresh, 1, 0);
            var title = Label("电源计划", 20, true); heading.Controls.Add(title, 0, 1); heading.SetColumnSpan(title, 2);
            var subtitle = Label("选择电源方案；点击左侧图标可更换表情。", 9, false);
            heading.Controls.Add(subtitle, 0, 2); heading.SetColumnSpan(subtitle, 2);
            root.Controls.Add(heading, 0, 0);

            var section = Table(2, 1);
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            sectionTitle = Label("电源方案", 9.5f, true); section.Controls.Add(sectionTitle, 0, 0);
            activeName = Label("当前：读取中…", 9, false); activeName.TextAlign = ContentAlignment.MiddleRight;
            activeName.Name = "activeName"; section.Controls.Add(activeName, 1, 0);
            root.Controls.Add(section, 0, 1);

            list = Table(1, 0); list.Name = "plans"; list.AutoScroll = true;
            list.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(list, 0, 2);
            root.Controls.Add(Label("应用设置", 9.5f, true), 0, 3);

            var settingGroup = Table(1, 2);
            settingGroup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            settingGroup.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            settingGroup.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            hotkeyRow = new SettingRow("全局快捷键", "Ctrl + Alt + 1 ~ 9，按列表顺序切换", theme) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 8) };
            trayRow = new SettingRow("关闭窗口后驻留托盘", "关闭后可从通知区域重新打开", theme) { Dock = DockStyle.Fill, Margin = Padding.Empty };
            hotkeys = hotkeyRow.Toggle; hotkeys.Name = "hotkeys"; hotkeys.Checked = settings.HotkeysEnabled;
            closeToTray = trayRow.Toggle; closeToTray.Name = "closeToTray"; closeToTray.Checked = settings.CloseToTray;
            hotkeys.CheckedChanged += SettingsChanged; closeToTray.CheckedChanged += SettingsChanged;
            settingGroup.Controls.Add(hotkeyRow, 0, 0); settingGroup.Controls.Add(trayRow, 0, 1);
            root.Controls.Add(settingGroup, 0, 4);

            var footer = Table(2, 1); footer.Padding = new Padding(0, 8, 0, 0);
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            status = Label("正在读取 Windows 电源计划…", 8.5f, false); status.Name = "status";
            footer.Controls.Add(status, 0, 0);
            var exit = Button("退出程序", "exit"); exit.Click += delegate { ExitApplication(); };
            footer.Controls.Add(exit, 1, 0); root.Controls.Add(footer, 0, 5);

            tray = new NotifyIcon { Icon = appIcon, Text = "Power Switch", Visible = !preview };
            tray.MouseClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowWindow(); };
            tray.ContextMenuStrip = BuildMenu();
            sync = new Timer { Interval = 5000 };
            sync.Tick += async delegate { if (!busy) await RefreshPlans(false); };
            Shown += async delegate
            {
                FitInitialSize();
                if (startInTray && !preview) Hide();
                await RefreshPlans(true); InitialLoadComplete = true;
                if (!exiting) sync.Start();
                if (!String.IsNullOrEmpty(startupWarning)) ShowStatus(startupWarning, true);
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (!exiting && e.CloseReason == CloseReason.UserClosing && settings.CloseToTray)
                { e.Cancel = true; HideToTray(); }
                else { exiting = true; sync.Stop(); tray.Visible = false; }
            };
            ApplyTheme();
            ResumeLayout(true);
        }

        private static TableLayoutPanel Table(int columns, int rows)
        { return new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columns, RowCount = rows, Margin = Padding.Empty, Padding = Padding.Empty, TabStop = false }; }
        private static Label Label(string text, float size, bool primary)
        {
            return new Label { Text = text, Font = Theme.Font(size, primary ? FontStyle.Bold : FontStyle.Regular, text),
                Tag = primary ? "primary" : "secondary", Dock = DockStyle.Fill, AutoEllipsis = true,
                Margin = Padding.Empty, TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false };
        }
        private static Button Button(string text, string name)
        {
            var button = new Button { Text = text, Name = name, Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat,
                Font = Theme.Font(9, FontStyle.Regular, text), Margin = Padding.Empty, Cursor = Cursors.Hand, UseVisualStyleBackColor = false };
            button.FlatAppearance.BorderSize = 0; return button;
        }
        private int S(float value) { return (int)Math.Round(value * DeviceDpi / 96f); }
        private void FitInitialSize()
        {
            Rectangle work = Screen.FromControl(this).WorkingArea;
            int count = plans.Count == 0 ? 4 : plans.Count;
            int content = S(348 + Math.Min(count, 6) * 80 + Math.Max(0, Math.Min(count, 6) - 1) * 8);
            int chrome = Height - ClientSize.Height;
            int desired = Math.Min(content, Math.Max(S(430), work.Height - chrome - S(24)));
            MinimumSize = new Size(Math.Min(S(460), work.Width - S(16)), Math.Min(S(510), work.Height - S(16)));
            ClientSize = new Size(Math.Min(S(560), work.Width - S(24)), desired);
            Location = new Point(work.Left + (work.Width - Width) / 2, work.Top + Math.Max(0, (work.Height - Height) / 2));
        }

        private void SettingsChanged(object sender, EventArgs e)
        {
            if (restoringSettings) return;
            bool previousHotkeys = settings.HotkeysEnabled, previousTray = settings.CloseToTray;
            settings.HotkeysEnabled = hotkeys.Checked; settings.CloseToTray = closeToTray.Checked;
            try
            {
                settings.Save(settingsPath);
                if (previousHotkeys != settings.HotkeysEnabled) ConfigureHotkeys();
                ShowStatus(String.IsNullOrEmpty(hotkeyWarning) ? "设置已保存" : hotkeyWarning, !String.IsNullOrEmpty(hotkeyWarning));
            }
            catch (Exception ex)
            {
                settings.HotkeysEnabled = previousHotkeys; settings.CloseToTray = previousTray;
                restoringSettings = true; hotkeys.Checked = previousHotkeys; closeToTray.Checked = previousTray; restoringSettings = false;
                ShowStatus("设置未保存，已恢复原设置：" + ex.Message, true);
            }
        }

        private async Task RefreshPlans(bool explicitRefresh)
        {
            if (busy || exiting) return;
            SetBusy(true, null);
            try
            {
                IList<PowerPlan> fresh = await Task.Run(() => service.GetPlans());
                if (exiting || IsDisposed) return;
                bool changed = ApplyPlans(fresh);
                if (explicitRefresh || changed)
                    ShowStatus(String.IsNullOrEmpty(hotkeyWarning) ? "已同步 Windows 电源计划" : hotkeyWarning, !String.IsNullOrEmpty(hotkeyWarning));
            }
            catch (Exception ex) { if (!exiting) ShowStatus(ex.Message, true); }
            finally { if (!exiting) SetBusy(false, null); }
        }

        private bool ApplyPlans(IList<PowerPlan> fresh)
        {
            string nextCatalog = String.Join("|", fresh.Select(p => p.Id + ":" + p.Name));
            string next = nextCatalog + "|active:" + String.Join(",", fresh.Where(p => p.IsActive).Select(p => p.Id.ToString()));
            if (next == signature) return false;
            bool rebuild = nextCatalog != catalogSignature;
            signature = next; catalogSignature = nextCatalog; plans = fresh;
            if (rebuild)
            {
                RenderPlans(); ConfigureHotkeys();
                if (!InitialLoadComplete) FitInitialSize();
            }
            else
            {
                foreach (PowerPlanCard card in list.Controls.OfType<PowerPlanCard>())
                { card.Plan.IsActive = plans.First(p => p.Id == card.Plan.Id).IsActive; card.Invalidate(); }
            }
            PowerPlan active = plans.FirstOrDefault(p => p.IsActive);
            activeName.Text = "当前：" + (active == null ? "未知，请刷新" : active.Name);
            tips.SetToolTip(activeName, activeName.Text);
            sectionTitle.Text = "电源方案 · " + plans.Count;
            string tooltip = "Power Switch · " + (active == null ? "未知计划" : active.Name);
            tray.Text = tooltip.Length > 63 ? tooltip.Substring(0, 63) : tooltip;
            ReplaceMenu(); return true;
        }

        private void RenderPlans()
        {
            list.SuspendLayout();
            foreach (Control control in list.Controls.Cast<Control>().ToArray()) control.Dispose();
            list.RowStyles.Clear(); list.RowCount = plans.Count + 1;
            for (int i = 0; i < plans.Count; i++)
            {
                PowerPlan plan = plans[i]; int gap = i == plans.Count - 1 ? 0 : S(8);
                list.RowStyles.Add(new RowStyle(SizeType.Absolute, S(80) + gap));
                var card = new PowerPlanCard(plan, theme) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, gap), TabIndex = i, Enabled = !busy,
                    DefaultIcon = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), CustomIcon = settings.GetPlanIcon(plan.Id) };
                card.Click += async delegate { await SwitchPlan(plan.Id); };
                card.EditIconRequested += delegate { EditPlanIcon(plan); };
                tips.SetToolTip(card, plan.Name); list.Controls.Add(card, 0, i);
            }
            list.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            list.ResumeLayout(true);
        }

        private void EditPlanIcon(PowerPlan plan)
        {
            if (busy || exiting) return;
            string defaultIcon = list.Controls.OfType<PowerPlanCard>().First(c => c.Plan.Id == plan.Id).DefaultIcon;
            using (var dialog = new EmojiIconDialog(plan.Name, settings.GetPlanIcon(plan.Id), theme, icon => SavePlanIcon(plan.Id, icon), defaultIcon))
                dialog.ShowDialog(this);
        }
        internal string SavePlanIcon(Guid id, string icon)
        {
            string previous = settings.GetPlanIcon(id);
            try { settings.SetPlanIcon(id, icon); settings.Save(settingsPath); }
            catch (Exception ex)
            {
                settings.SetPlanIcon(id, previous);
                ShowStatus("图标未保存：" + ex.Message, true);
                return "图标未保存：" + ex.Message;
            }
            foreach (var card in list.Controls.OfType<PowerPlanCard>().Where(c => c.Plan.Id == id)) card.CustomIcon = icon;
            ShowStatus(String.IsNullOrEmpty(icon) ? "已恢复默认图标" : "图标已保存", false);
            return null;
        }

        private void SetBusy(bool value, Guid? target)
        {
            busy = value; refresh.Enabled = !value;
            foreach (PowerPlanCard card in list.Controls.OfType<PowerPlanCard>())
            { card.Switching = value && target.HasValue && card.Plan.Id == target.Value; card.Enabled = !value; }
        }

        private async Task SwitchPlan(Guid id)
        {
            if (busy || exiting) return;
            PowerPlan selected = plans.FirstOrDefault(p => p.Id == id);
            if (selected == null) return;
            if (selected.IsActive) { ShowStatus("当前已使用「" + selected.Name + "」", false); return; }
            SetBusy(true, id); ShowStatus("正在切换到「" + selected.Name + "」…", false);
            try
            {
                IList<PowerPlan> fresh = await Task.Run(() => { service.Activate(id); return service.GetPlans(); });
                if (exiting || IsDisposed) return;
                ApplyPlans(fresh);
                ShowStatus("已切换到「" + selected.Name + "」", false);
                if (!Visible && !preview) tray.ShowBalloonTip(1600, "电源计划已切换", selected.Name, ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                if (!exiting)
                {
                    // Keep the last confirmed selection. A later refresh reads actual Windows state.
                    ShowStatus(ex.Message, true);
                    if (!Visible && !preview) tray.ShowBalloonTip(4000, "切换失败", ex.Message, ToolTipIcon.Error);
                }
            }
            finally { if (!exiting) SetBusy(false, null); }
        }

        private ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip { Font = Theme.Font(9, FontStyle.Regular, "电源计划"), ShowImageMargin = true,
                BackColor = theme.Surface, ForeColor = theme.Text };
            menu.Renderer = theme.HighContrast ? (ToolStripRenderer)new ToolStripSystemRenderer() : new ToolStripProfessionalRenderer(new MenuColors(theme));
            menu.Items.Add(new ToolStripMenuItem("Power Switch") { Enabled = false });
            menu.Items.Add(new ToolStripSeparator());
            foreach (PowerPlan plan in plans)
            {
                Guid id = plan.Id;
                var item = new ToolStripMenuItem(plan.Name) { Checked = plan.IsActive, Tag = id };
                item.Click += async delegate { await SwitchPlan(id); }; menu.Items.Add(item);
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("打开主窗口", null, delegate { ShowWindow(); });
            menu.Items.Add("刷新计划", null, async delegate { await RefreshPlans(true); });
            menu.Items.Add("退出程序", null, delegate { ExitApplication(); });
            menu.Opening += delegate { foreach (ToolStripItem item in menu.Items) if (item.Tag is Guid) item.Enabled = !busy; };
            return menu;
        }
        private sealed class MenuColors : ProfessionalColorTable
        {
            private readonly Theme theme;
            internal MenuColors(Theme theme) { this.theme = theme; UseSystemColors = false; }
            public override Color ToolStripDropDownBackground { get { return theme.Surface; } }
            public override Color ImageMarginGradientBegin { get { return theme.Surface; } }
            public override Color ImageMarginGradientMiddle { get { return theme.Surface; } }
            public override Color ImageMarginGradientEnd { get { return theme.Surface; } }
            public override Color MenuItemSelected { get { return theme.Hover; } }
            public override Color MenuItemBorder { get { return theme.Border; } }
            public override Color MenuBorder { get { return theme.Border; } }
            public override Color CheckBackground { get { return theme.Selected; } }
            public override Color CheckSelectedBackground { get { return theme.Selected; } }
            public override Color SeparatorDark { get { return theme.Border; } }
            public override Color SeparatorLight { get { return theme.Border; } }
        }
        private void ReplaceMenu()
        {
            if (exiting || resourcesDisposed) return;
            ContextMenuStrip previous = tray.ContextMenuStrip;
            if (previous != null && previous.Visible)
            {
                if (!menuUpdatePending) { menuUpdatePending = true; previous.Closed += MenuClosed; }
                return;
            }
            tray.ContextMenuStrip = BuildMenu();
            if (previous != null) { Font font = previous.Font; previous.Dispose(); font.Dispose(); }
        }
        private void MenuClosed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            ((ContextMenuStrip)sender).Closed -= MenuClosed; menuUpdatePending = false;
            if (!exiting && IsHandleCreated) BeginInvoke((Action)ReplaceMenu);
        }
        private void ReleaseHotkeys()
        {
            if (IsHandleCreated) foreach (int id in registeredKeys) NativeMethods.UnregisterHotKey(Handle, id);
            registeredKeys.Clear(); keyPlans.Clear();
        }
        private void ConfigureHotkeys()
        {
            ReleaseHotkeys(); var conflicts = new List<string>(); int index = 0;
            foreach (PowerPlanCard card in list.Controls.OfType<PowerPlanCard>())
            {
                card.Shortcut = "";
                if (settings.HotkeysEnabled && index < 9 && !preview)
                {
                    int id = 8100 + index;
                    if (NativeMethods.RegisterHotKey(Handle, id, 0x4003, (uint)Keys.D1 + (uint)index))
                    { registeredKeys.Add(id); keyPlans[id] = card.Plan.Id; card.Shortcut = "Ctrl+Alt+" + (index + 1); }
                    else
                    {
                        int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                        conflicts.Add((index + 1) + "（错误 " + error + "）");
                        card.Shortcut = error == 1409 ? "快捷键占用" : "快捷键不可用";
                    }
                }
                tips.SetToolTip(card, card.Plan.Name + (String.IsNullOrEmpty(card.Shortcut) ? "" : " · " + card.Shortcut) + "\n点击左侧图标，或按 F2 更换表情。");
                card.AccessibleDescription = card.Shortcut + "。按 F2 更换图标。"; card.Invalidate(); index++;
            }
            hotkeyWarning = conflicts.Count == 0 ? "" : "快捷键注册失败：Ctrl+Alt+" + String.Join(" / ", conflicts) + "，可点击切换。";
        }
        private void ShowStatus(string message, bool error)
        { statusError = error; status.Text = message; status.ForeColor = error ? theme.Error : theme.Muted; tips.SetToolTip(status, message); }
        internal void ShowWindow()
        {
            if (exiting) return;
            Show(); WindowState = FormWindowState.Normal; Activate(); var ignored = RefreshPlans(false);
        }
        private void HideToTray()
        {
            Hide();
            if (!hadHiddenTip && !preview)
            {
                hadHiddenTip = true;
                tray.ShowBalloonTip(2000, "Power Switch 已在托盘运行", "右键电源图标切换计划，左键重新打开窗口。", ToolTipIcon.Info);
            }
        }
        internal void ExitApplication() { exiting = true; sync.Stop(); tray.Visible = false; Close(); }

        private void ApplyTheme()
        {
            if (resourcesDisposed) return;
            theme = themeOverride ?? Theme.ReadSystem();
            ApplyControlTheme(this);
            if (status != null) status.ForeColor = statusError ? theme.Error : theme.Muted;
            if (IsHandleCreated) NativeMethods.ApplyWindowTheme(Handle, theme);
            if (tray != null) ReplaceMenu();
            Invalidate(true);
        }
        private void ApplyControlTheme(Control control)
        {
            control.BackColor = theme.Background; control.ForeColor = Equals(control.Tag, "primary") ? theme.Text : theme.Muted;
            var card = control as PowerPlanCard; if (card != null) card.Palette = theme;
            var setting = control as SettingRow; if (setting != null) setting.Palette = theme;
            var toggle = control as ToggleSwitch; if (toggle != null) { toggle.Palette = theme; toggle.BackColor = theme.Surface; }
            var button = control as Button;
            if (button != null)
            { button.ForeColor = theme.Text; button.FlatAppearance.MouseOverBackColor = theme.Hover; button.FlatAppearance.MouseDownBackColor = theme.Selected; }
            foreach (Control child in control.Controls) ApplyControlTheme(child);
            control.Invalidate();
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (theme != null) NativeMethods.ApplyWindowTheme(Handle, theme);
            if (list != null && plans.Count > 0 && !exiting) ConfigureHotkeys();
        }
        protected override void OnHandleDestroyed(EventArgs e)
        {
            ReleaseHotkeys();
            base.OnHandleDestroyed(e);
        }
        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            Rectangle work = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min(S(460), work.Width - S(16)), Math.Min(S(510), work.Height - S(16)));
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == NativeMethods.ShowMessage) { ShowWindow(); return; }
            if (message.Msg == 0x0312)
            {
                Guid id; if (keyPlans.TryGetValue(message.WParam.ToInt32(), out id)) { var ignored = SwitchPlan(id); }
            }
            bool themeChanged = message.Msg == 0x001A || message.Msg == 0x031A || message.Msg == 0x0015 || message.Msg == 0x0320;
            base.WndProc(ref message);
            if (themeChanged && root != null && !exiting && !resourcesDisposed && IsHandleCreated) BeginInvoke((Action)ApplyTheme);
        }
        internal void SavePreview(string path)
        {
            using (var bitmap = new Bitmap(root.Width, root.Height))
            { root.DrawToBitmap(bitmap, root.ClientRectangle); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
        }
        private static Icon CreateIcon()
        {
            using (var bitmap = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
                using (var brush = new SolidBrush(Color.FromArgb(32, 32, 32))) g.FillEllipse(brush, 1, 1, 30, 30);
                using (var pen = new Pen(Color.FromArgb(96, 205, 255), 2.8f))
                { pen.StartCap = pen.EndCap = LineCap.Round; g.DrawArc(pen, 8, 8, 16, 17, -52, 284); g.DrawLine(pen, 16, 5, 16, 16); }
                IntPtr handle = bitmap.GetHicon();
                try { using (var original = Icon.FromHandle(handle)) return (Icon)original.Clone(); }
                finally { NativeMethods.DestroyIcon(handle); }
            }
        }
        private static void DisposeFonts(Control control)
        {
            foreach (Control child in control.Controls) DisposeFonts(child);
            if (control is Label || control is Button) control.Font.Dispose();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && !resourcesDisposed)
            {
                resourcesDisposed = true; exiting = true; ReleaseHotkeys();
                if (sync != null) sync.Dispose();
                if (tray != null)
                {
                    tray.Visible = false;
                    if (tray.ContextMenuStrip != null) { Font font = tray.ContextMenuStrip.Font; tray.ContextMenuStrip.Dispose(); font.Dispose(); }
                    tray.Dispose();
                }
                tips.Dispose(); if (appIcon != null) appIcon.Dispose();
                DisposeFonts(this);
                Font.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
