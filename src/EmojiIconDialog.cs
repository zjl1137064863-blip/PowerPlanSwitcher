using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PowerPlanSwitcher
{
    internal sealed class EmojiIconDialog : Form
    {
        private readonly TextBox input;
        private readonly Label feedback;
        private readonly Button save;
        private readonly IconPreview preview;
        private readonly Theme theme;
        private readonly string defaultIcon;
        private readonly Func<string, string> persist;
        private readonly List<Font> ownedFonts = new List<Font>();

        internal EmojiIconDialog(string planName, string currentIcon, Theme theme, Func<string, string> persist, string defaultIcon = "1")
        {
            this.theme = theme; this.persist = persist; this.defaultIcon = defaultIcon;
            SuspendLayout(); AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            Text = "更换图标 · " + planName; FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false; MaximizeBox = MinimizeBox = false;
            ClientSize = new Size(460, 256); BackColor = theme.Background; ForeColor = theme.Text;
            Font = OwnFont(9, "更换图标");
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(20) };
            foreach (float height in new[] { 28f, 48f, 56f, 42f, 42f }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); Controls.Add(root);
            root.Controls.Add(Label(planName + " · 选择表情图标", 12, true), 0, 0);
            root.Controls.Add(Label("点击输入框，按 Win + . 或 Win + ; 选择表情。\n也可以粘贴表情或简短符号。", 9, false), 0, 1);

            var editor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
            editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); editor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
            input = new TextBox { Name = "emojiInput", Dock = DockStyle.Fill, Margin = new Padding(0, 7, 12, 7),
                Font = OwnFont(18, null), BorderStyle = BorderStyle.FixedSingle, BackColor = theme.Surface,
                ForeColor = theme.Text, MaxLength = 64, AutoSize = false, AccessibleName = "表情图标，按 Windows 加句号打开系统表情面板" };
            preview = new IconPreview(theme, defaultIcon) { Dock = DockStyle.Fill, Margin = Padding.Empty };
            editor.Controls.Add(input, 0, 0); editor.Controls.Add(preview, 1, 0); root.Controls.Add(editor, 0, 2);
            feedback = Label("", 8.5f, false); root.Controls.Add(feedback, 0, 3);
            var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = Padding.Empty };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80)); buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            var reset = Button("恢复默认", "resetIcon"); reset.Dock = DockStyle.None; reset.Anchor = AnchorStyles.Left;
            reset.AutoSize = true; reset.MinimumSize = new Size(96, 32); reset.Margin = new Padding(0, 5, 0, 0);
            reset.Click += delegate { input.Text = ""; input.Focus(); };
            var cancel = Button("取消", "cancelIcon"); cancel.DialogResult = DialogResult.Cancel;
            save = Button("保存", "saveIcon"); save.BackColor = theme.Accent; save.ForeColor = theme.OnAccent;
            save.Click += delegate { SaveIcon(); };
            buttons.Controls.Add(reset, 0, 0); buttons.Controls.Add(cancel, 1, 0); buttons.Controls.Add(save, 2, 0);
            root.Controls.Add(buttons, 0, 4); AcceptButton = save; CancelButton = cancel;
            input.TextChanged += delegate { UpdatePreview(); };
            input.Text = currentIcon ?? ""; UpdatePreview();
            Shown += delegate { input.Focus(); input.SelectAll(); };
            ResumeLayout(true);
        }
        private Font OwnFont(float size, string text)
        {
            Font font = text == null ? new Font("Segoe UI Emoji", size) : Theme.Font(size, FontStyle.Regular, text);
            ownedFonts.Add(font); return font;
        }
        private Label Label(string text, float size, bool primary)
        { return new Label { Text = text, Dock = DockStyle.Fill, Margin = Padding.Empty, AutoEllipsis = true,
            Font = OwnFont(size, text), ForeColor = primary ? theme.Text : theme.Muted, TextAlign = ContentAlignment.MiddleLeft }; }
        private Button Button(string text, string name)
        {
            var button = new Button { Text = text, Name = name, Dock = DockStyle.Fill, Margin = new Padding(4, 5, 0, 0),
                FlatStyle = FlatStyle.Flat, Font = OwnFont(9, text), ForeColor = theme.Text, BackColor = theme.Surface, Cursor = Cursors.Hand };
            button.FlatAppearance.BorderColor = theme.Border; return button;
        }
        private void UpdatePreview()
        {
            string icon = input.Text.Trim(); bool valid = icon.Length == 0 || AppSettings.IsValidIcon(icon);
            save.Enabled = valid; preview.Emoji = valid ? icon : ""; preview.Invalidate();
            feedback.ForeColor = valid ? theme.Muted : theme.Error;
            feedback.Text = !valid ? "请输入表情或简短符号，不要包含空格或换行。" : icon.Length == 0 ? "保存后恢复默认数字图标（" + defaultIcon + "）。" : "右侧为实际图标预览，点击保存后生效。";
        }
        private void SaveIcon()
        {
            string error = persist(input.Text.Trim());
            if (!String.IsNullOrEmpty(error)) { feedback.ForeColor = theme.Error; feedback.Text = error; return; }
            DialogResult = DialogResult.OK; Close();
        }
        protected override void OnHandleCreated(EventArgs e)
        { base.OnHandleCreated(e); if (theme != null) NativeMethods.ApplyWindowTheme(Handle, theme); }
        protected override void Dispose(bool disposing)
        { if (disposing) { foreach (Font font in ownedFonts) font.Dispose(); ownedFonts.Clear(); } base.Dispose(disposing); }

        private sealed class IconPreview : Control
        {
            internal string Emoji = "";
            private readonly Theme theme;
            private readonly string defaultIcon;
            private readonly EmojiRenderer renderer = new EmojiRenderer();
            internal IconPreview(Theme theme, string defaultIcon)
            { this.theme = theme; this.defaultIcon = defaultIcon; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(theme.Background); int size = (int)(44 * DeviceDpi / 96f);
                var bounds = new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);
                if (Emoji.Length != 0 && renderer.TryDraw(e.Graphics, bounds, Emoji, theme.Text, !theme.HighContrast)) return;
                using (var font = Emoji.Length == 0 ? Theme.Font(20, FontStyle.Bold) : new Font("Segoe UI Emoji", 22))
                    TextRenderer.DrawText(e.Graphics, Emoji.Length == 0 ? defaultIcon : Emoji, font, ClientRectangle, theme.Muted,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
            protected override void Dispose(bool disposing) { if (disposing) renderer.Dispose(); base.Dispose(disposing); }
        }
    }
}
