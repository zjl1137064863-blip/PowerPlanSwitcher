using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace PowerPlanSwitcher
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool preview = args.Length == 2 && args[0] == "--preview";
            bool tray = Array.Exists(args, a => a == "--tray");
            bool created;
            using (var mutex = new Mutex(true, @"Local\PowerPlanSwitcher.7D43B3AE", out created))
            {
                if (!created && !preview)
                {
                    NativeMethods.PostMessage((IntPtr)0xffff, NativeMethods.ShowMessage, IntPtr.Zero, IntPtr.Zero);
                    return;
                }
                try
                {
                    using (var form = new MainForm(new PowerPlanService(new NativePowerApi()), tray, preview))
                    {
                        if (preview)
                        {
                            form.Shown += delegate
                            {
                                var timer = new System.Windows.Forms.Timer { Interval = 1100 };
                                timer.Tick += delegate
                                {
                                    timer.Stop();
                                    using (var bitmap = new System.Drawing.Bitmap(form.Width, form.Height))
                                    {
                                        form.DrawToBitmap(bitmap, form.ClientRectangle);
                                        bitmap.Save(Path.GetFullPath(args[1]), System.Drawing.Imaging.ImageFormat.Png);
                                    }
                                    timer.Dispose();
                                    form.ExitApplication();
                                };
                                timer.Start();
                            };
                        }
                        Application.Run(form);
                    }
                }
                catch (Exception error)
                {
                    MessageBox.Show(error.Message, "电源计划切换器", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally { if (created) mutex.ReleaseMutex(); }
            }
        }
    }
}
