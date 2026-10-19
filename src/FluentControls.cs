using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PowerPlanSwitcher
{
    internal enum CardVisualState { Normal, Hover, Selected, Disabled, Switching }

    internal sealed class PowerPlanCard : UserControl
    {
        internal PowerPlan Plan { get; private set; }
        internal string Shortcut = "";
        internal string DefaultIcon = "1";
        internal Theme Palette;
        private readonly EmojiRenderer emojiRenderer = new EmojiRenderer();
        private string customIcon = "";
        private bool hover, switching, iconHover, iconPressed;
        internal event EventHandler EditIconRequested;
        internal string CustomIcon { get { return customIcon; } set { customIcon = value ?? ""; Invalidate(); } }
        internal bool Switching { get { return switching; } set { switching = value; Invalidate(); } }
        internal CardVisualState VisualState
        { get { return switching ? CardVisualState.Switching : !Enabled ? CardVisualState.Disabled : Plan.IsActive ? CardVisualState.Selected : hover ? CardVisualState.Hover : CardVisualState.Normal; } }

        internal PowerPlanCard(PowerPlan plan, Theme palette)
        {
            Plan = plan; Palette = palette; TabStop = true; Cursor = Cursors.Hand;
            AccessibleName = plan.Name; AccessibleRole = AccessibleRole.RadioButton;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }
        internal void PerformClick() { iconPressed = false; if (Enabled && !Switching) OnClick(EventArgs.Empty); }
        private Rectangle IconBounds
        { get { float s = DeviceDpi / 96f; return new Rectangle((int)(10 * s), (Height - (int)(44 * s)) / 2, (int)(40 * s), (int)(44 * s)); } }
        internal void RequestIconEdit() { if (Enabled && !Switching && EditIconRequested != null) EditIconRequested(this, EventArgs.Empty); }
        protected override void OnMouseDown(MouseEventArgs e)
        { iconPressed = e.Button == MouseButtons.Left && IconBounds.Contains(e.Location); if (e.Button == MouseButtons.Left) Focus(); base.OnMouseDown(e); }
        protected override void OnClick(EventArgs e)
        {
            if (iconPressed) { iconPressed = false; RequestIconEdit(); return; }
            base.OnClick(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        { bool next = IconBounds.Contains(e.Location); if (next != iconHover) { iconHover = next; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = iconHover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F2) { RequestIconEdit(); e.Handled = e.SuppressKeyPress = true; }
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { PerformClick(); e.Handled = e.SuppressKeyPress = true; }
            base.OnKeyDown(e);
        }
        protected override void OnPaint(PaintEventArgs e) { DrawCard(e.Graphics, ClientRectangle, DeviceDpi / 96f); }

        internal void DrawCard(Graphics g, Rectangle bounds, float scale)
        {
            Theme t = Palette; CardVisualState state = VisualState;
            bool selected = Plan.IsActive;
            bool highlighted = t.HighContrast && (selected || hover);
            Color background = selected ? t.Selected : hover && Enabled ? t.Hover : t.Surface;
            Color foreground = highlighted ? t.OnAccent : state == CardVisualState.Disabled ? t.Muted : t.Text;
            Color secondary = highlighted ? t.OnAccent : t.Muted;
            g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(t.Background);
            using (var path = Theme.Round(new RectangleF(.5f, .5f, bounds.Width - 1, bounds.Height - 1), 4 * scale))
            using (var brush = new SolidBrush(background))
            using (var pen = new Pen(t.Border, 1)) { g.FillPath(brush, path); g.DrawPath(pen, path); }
            if (selected)
                using (var brush = new SolidBrush(highlighted ? t.OnAccent : t.Accent))
                using (var path = Theme.Round(new RectangleF(1, 18 * scale, 3 * scale, bounds.Height - 36 * scale), 1.5f * scale)) g.FillPath(brush, path);
            if (iconHover && Enabled)
                using (var path = Theme.Round(IconBounds, 4 * scale))
                using (var pen = new Pen(t.Muted, 1)) g.DrawPath(pen, path);
            if (String.IsNullOrEmpty(CustomIcon))
                using (var numberFont = Theme.Font(20, FontStyle.Bold))
                    TextRenderer.DrawText(g, DefaultIcon, numberFont, IconBounds, foreground,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            else if (!emojiRenderer.TryDraw(g, new Rectangle((int)(10 * scale), (bounds.Height - (int)(40 * scale)) / 2, (int)(40 * scale), (int)(40 * scale)), CustomIcon, foreground, !t.HighContrast))
                using (var emojiFont = new Font("Segoe UI Emoji", 19))
                    TextRenderer.DrawText(g, CustomIcon, emojiFont, IconBounds, foreground,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            int textX = (int)(58 * scale), radioX = bounds.Width - (int)(34 * scale);
            int shortWidth = String.IsNullOrEmpty(Shortcut) ? 0 : (int)(100 * scale);
            int lineY = (int)((bounds.Height - 46 * scale) / 2);
            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            using (var title = Theme.Font(10.5f, FontStyle.Bold, Plan.Name))
            using (var detail = Theme.Font(9, FontStyle.Regular, "说明"))
            using (var shortcutFont = Theme.Font(8))
            {
                TextRenderer.DrawText(g, Plan.Name, title, new Rectangle(textX, lineY, Math.Max(1, radioX - textX - shortWidth - (int)(12 * scale)), (int)(24 * scale)), foreground, flags);
                TextRenderer.DrawText(g, Switching ? "正在切换，请稍候…" : Description(Plan), detail,
                    new Rectangle(textX, lineY + (int)(25 * scale), Math.Max(1, radioX - textX - (int)(10 * scale)), (int)(21 * scale)), secondary, flags);
                if (shortWidth > 0)
                    TextRenderer.DrawText(g, Shortcut, shortcutFont, new Rectangle(radioX - shortWidth - (int)(10 * scale), lineY, shortWidth, (int)(24 * scale)), secondary,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
            float circleSize = 17 * scale, circleY = (bounds.Height - circleSize) / 2;
            if (Switching)
            {
                using (var pen = new Pen(t.Accent, 2 * scale)) g.DrawArc(pen, radioX, circleY, circleSize, circleSize, -70, 270);
            }
            else if (selected)
            {
                using (var brush = new SolidBrush(highlighted ? t.OnAccent : t.Accent)) g.FillEllipse(brush, radioX, circleY, circleSize, circleSize);
                using (var brush = new SolidBrush(highlighted ? t.Accent : t.OnAccent))
                    g.FillEllipse(brush, radioX + 5.5f * scale, circleY + 5.5f * scale, 6 * scale, 6 * scale);
            }
            else using (var pen = new Pen(secondary, 1.2f * scale)) g.DrawEllipse(pen, radioX, circleY, circleSize, circleSize);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(bounds, -(int)(6 * scale), -(int)(6 * scale)), foreground, background);
        }

        private static int Kind(PowerPlan plan)
        {
            string name = plan.Name.ToLowerInvariant();
            if (plan.Id == new Guid("381b4222-f694-41f0-9685-ff5bb260df2e")) return 0;
            if (plan.Id == new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c") || name.Contains("性能") || name.Contains("performance")) return 1;
            if (plan.Id == new Guid("a1841308-3541-4fab-bc81-f71556f20b4a") || name.Contains("节能") || name.Contains("saver")) return 2;
            if (name.Contains("静音") || name.Contains("silent")) return 3;
            return 4;
        }
        private static string Description(PowerPlan plan)
        {
            switch (Kind(plan))
            {
                case 0: return "兼顾性能与能源消耗";
                case 1: return "性能优先，适合高负载任务";
                case 2: return "降低能源消耗，延长电池续航";
                default: return "使用此方案在 Windows 中的配置";
            }
        }
        protected override AccessibleObject CreateAccessibilityInstance() { return new CardAccessibleObject(this); }
        protected override void Dispose(bool disposing) { if (disposing) emojiRenderer.Dispose(); base.Dispose(disposing); }
        private sealed class CardAccessibleObject : ControlAccessibleObject
        {
            private readonly PowerPlanCard card;
            internal CardAccessibleObject(PowerPlanCard card) : base(card) { this.card = card; }
            public override AccessibleStates State { get { return base.State | (card.Plan.IsActive ? AccessibleStates.Checked : 0) | AccessibleStates.Focusable; } }
            public override string DefaultAction { get { return "选择电源计划"; } }
            public override void DoDefaultAction() { card.PerformClick(); }
        }
    }

    internal sealed class ToggleSwitch : CheckBox
    {
        internal Theme Palette;
        private bool hover;
        internal ToggleSwitch(Theme palette)
        {
            Palette = palette; AutoSize = false; Cursor = Cursors.Hand; Text = "";
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Theme t = Palette; float s = DeviceDpi / 96f;
            Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(t.Surface);
            RectangleF track = new RectangleF((Width - 40 * s) / 2, (Height - 20 * s) / 2, 40 * s, 20 * s);
            using (var path = Theme.Round(track, 10 * s))
            using (var brush = new SolidBrush(Checked ? t.Accent : hover ? t.Hover : t.Surface))
            using (var pen = new Pen(Checked ? t.Accent : t.Muted, s)) { g.FillPath(brush, path); g.DrawPath(pen, path); }
            using (var brush = new SolidBrush(Checked ? t.OnAccent : t.Muted))
                g.FillEllipse(brush, track.X + (Checked ? 24 : 4) * s, track.Y + 4 * s, 12 * s, 12 * s);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -1, -1), t.Text, t.Surface);
        }
    }

    internal sealed class SettingRow : UserControl
    {
        internal readonly ToggleSwitch Toggle;
        internal Theme Palette;
        private readonly string title, description;
        internal SettingRow(string title, string description, Theme palette)
        {
            this.title = title; this.description = description; Palette = palette;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Toggle = new ToggleSwitch(palette) { AccessibleName = title + "，" + description };
            Controls.Add(Toggle); TabStop = false;
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e); float s = DeviceDpi / 96f;
            Toggle.SetBounds(Width - (int)(66 * s), (Height - (int)(30 * s)) / 2, (int)(50 * s), (int)(30 * s));
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Theme t = Palette; float s = DeviceDpi / 96f; Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(t.Background);
            using (var path = Theme.Round(new RectangleF(.5f, .5f, Width - 1, Height - 1), 4 * s))
            using (var brush = new SolidBrush(t.Surface))
            using (var pen = new Pen(t.Border, 1)) { g.FillPath(brush, path); g.DrawPath(pen, path); }
            int x = (int)(14 * s), width = Math.Max(1, Toggle.Left - x - (int)(10 * s));
            using (var font = Theme.Font(9.5f, FontStyle.Regular, title))
            using (var detail = Theme.Font(8, FontStyle.Regular, description))
            {
                var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
                TextRenderer.DrawText(g, title, font, new Rectangle(x, (int)(5 * s), width, (int)(23 * s)), t.Text, flags);
                TextRenderer.DrawText(g, description, detail, new Rectangle(x, (int)(28 * s), width, (int)(18 * s)), t.Muted, flags);
            }
        }
    }
}
