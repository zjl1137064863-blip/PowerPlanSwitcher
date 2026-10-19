using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using PowerPlanSwitcher;

internal static class EmojiTests
{
    private static int passed;
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Pass(string message) { passed++; Console.WriteLine("PASS " + message); }
    private static bool HasColor(Bitmap bitmap)
    {
        int count = 0;
        for (int x = 0; x < bitmap.Width; x++) for (int y = 0; y < bitmap.Height; y++)
        {
            Color pixel = bitmap.GetPixel(x, y);
            if (Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) - Math.Min(pixel.R, Math.Min(pixel.G, pixel.B)) > 35) count++;
        }
        return count > 15;
    }
    [STAThread]
    private static int Main()
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        string output = AppDomain.CurrentDomain.BaseDirectory;
        string path = Path.Combine(output, "emoji-tests-" + Guid.NewGuid().ToString("N") + ".ini");
        Guid first = Guid.NewGuid(), second = Guid.NewGuid();
        try
        {
            var settings = new AppSettings { HotkeysEnabled = true, CloseToTray = false };
            foreach (string emoji in new[] { "😀", "🌙", "⚡", "🌿", "👩🏽‍💻", "👨‍👩‍👧‍👦", "🇨🇳", "1️⃣" })
            {
                Assert(AppSettings.IsValidIcon(emoji), "Valid emoji rejected: " + emoji);
                settings.SetPlanIcon(first, emoji); settings.Save(path);
                Assert(AppSettings.Load(path).GetPlanIcon(first) == emoji, "Unicode sequence changed on disk");
            }
            settings.SetPlanIcon(first, "👩🏽‍💻"); settings.SetPlanIcon(second, "🌙"); settings.Save(path);
            var loaded = AppSettings.Load(path);
            Assert(loaded.GetPlanIcon(first) == "👩🏽‍💻" && loaded.GetPlanIcon(second) == "🌙" && loaded.HotkeysEnabled && !loaded.CloseToTray, "Per-GUID preferences interfered");
            loaded.SetPlanIcon(first, ""); loaded.Save(path);
            Assert(AppSettings.Load(path).GetPlanIcon(first) == "" && AppSettings.Load(path).GetPlanIcon(second) == "🌙", "Reset cleared another plan");
            Assert(!AppSettings.IsValidIcon("x\ny") && !AppSettings.IsValidIcon("a b") && !AppSettings.IsValidIcon("\ud800") && !AppSettings.IsValidIcon(new string('x', 65)), "Invalid INI input accepted");
            Pass("Emoji / skin-tone / ZWJ / flag / keycap round-trip, per-GUID independence and reset");

            var theme = new Theme(true, false, Color.FromArgb(96, 205, 255));
            using (var bitmap = new Bitmap(160, 80))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var renderer = new EmojiRenderer())
            {
                graphics.Clear(theme.Surface);
                Assert(renderer.TryDraw(graphics, new Rectangle(40, 0, 80, 80), "😀", theme.Text, true), "Native emoji renderer unavailable");
                Assert(HasColor(bitmap), "Emoji was not rendered in color");
                bitmap.Save(Path.Combine(output, "emoji-color-test.png"));
                Pass("Native Direct2D/DirectWrite renders real color emoji pixels");
            }
            using (var card = new PowerPlanCard(new PowerPlan(first, "平衡"), theme) { Size = new Size(520, 80), CustomIcon = "👩🏽‍💻" })
            using (var bitmap = new Bitmap(520, 80))
            {
                int edits = 0, switches = 0;
                card.EditIconRequested += delegate { edits++; }; card.Click += delegate { switches++; };
                var mouseDown = typeof(PowerPlanCard).GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic);
                var click = typeof(PowerPlanCard).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
                mouseDown.Invoke(card, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 28, 40, 0) }); click.Invoke(card, new object[] { EventArgs.Empty });
                Assert(edits == 1 && switches == 0, "Editing icon also switched power plan");
                card.PerformClick(); Assert(switches == 1, "Body click lost switching behavior");
                using (Graphics graphics = Graphics.FromImage(bitmap)) card.DrawCard(graphics, new Rectangle(0, 0, 520, 80), 1);
                Assert(HasColor(bitmap), "Card lost color rendering");
                bitmap.Save(Path.Combine(output, "emoji-card-test.png"));
                Pass("Icon click edits without switching; body click still switches; composite emoji card renders");
            }

            string persisted = null;
            using (var dialog = new EmojiIconDialog("平衡", "🌙", theme, icon => { persisted = icon; return null; }))
            {
                dialog.Show(); Application.DoEvents();
                Assert(dialog.Controls.Find("resetIcon", true).Single().Width >= 96, "Reset button was squeezed by layout");
                var input = (TextBox)dialog.Controls.Find("emojiInput", true).Single();
                input.Text = "👨‍👩‍👧‍👦"; Application.DoEvents();
                using (var bitmap = new Bitmap(dialog.Width, dialog.Height))
                { dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, dialog.Size)); bitmap.Save(Path.Combine(output, "emoji-editor-test.png")); }
                ((Button)dialog.Controls.Find("saveIcon", true).Single()).PerformClick();
                Assert(persisted == "👨‍👩‍👧‍👦" && dialog.DialogResult == DialogResult.OK, "Editor did not save full emoji sequence");
            }
            using (var dialog = new EmojiIconDialog("平衡", "🌙", theme, icon => "模拟：设置目录不可写"))
            {
                dialog.Show(); Application.DoEvents();
                ((Button)dialog.Controls.Find("saveIcon", true).Single()).PerformClick();
                Assert(dialog.Visible && dialog.DialogResult != DialogResult.OK, "Failed save closed editor");
                ((Button)dialog.Controls.Find("resetIcon", true).Single()).PerformClick();
                Assert(((TextBox)dialog.Controls.Find("emojiInput", true).Single()).Text == "", "Reset button did not clear icon");
                dialog.Close();
            }
            Pass("Actual WinForms editor saves full sequence, previews, resets and stays open on save error");
            Console.WriteLine("All " + passed + " emoji checks passed. Windows emoji-panel keyboard interaction requires manual acceptance.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL " + error); return 1; }
    }
}
