using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.7.2")]
[assembly: System.Reflection.AssemblyVersion("2.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("2.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("2.0.0")]

namespace PowerPlanSwitcher
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool preview = args.Length >= 2 && args[0] == "--preview";
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
                    Theme previewTheme = null;
                    if (preview && Array.Exists(args, a => a == "--dark")) previewTheme = new Theme(true, false, System.Drawing.Color.FromArgb(96, 205, 255));
                    if (preview && Array.Exists(args, a => a == "--light")) previewTheme = new Theme(false, false, System.Drawing.Color.FromArgb(0, 95, 184));
                    using (var form = new MainForm(new PowerPlanService(new NativePowerApi()), tray, preview, null, previewTheme))
                    {
                        if (preview)
                        {
                            form.Shown += delegate
                            {
                                var timer = new System.Windows.Forms.Timer { Interval = 100 };
                                timer.Tick += delegate
                                {
                                    if (!form.InitialLoadComplete) return;
                                    timer.Stop();
                                    form.SavePreview(Path.GetFullPath(args[1]));
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
