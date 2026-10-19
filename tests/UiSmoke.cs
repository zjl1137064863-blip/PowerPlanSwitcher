using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PowerPlanSwitcher;

internal static class UiSmoke
{
    private sealed class FakeApi : IPowerApi
    {
        internal readonly Guid A = new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");
        internal readonly Guid B = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        internal Guid Active;
        internal int Writes;
        internal FakeApi() { Active = A; }
        public IList<PowerPlan> Enumerate()
        { return new List<PowerPlan> { new PowerPlan(A, "平衡"), new PowerPlan(B, "高性能") }; }
        public Guid GetActive() { return Active; }
        public void SetActive(Guid id) { Writes++; Active = id; }
    }
    private static async Task Until(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.ElapsedMilliseconds > 5000) throw new Exception("UI operation timed out");
            await Task.Delay(20);
        }
    }
    private static void Assert(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    [STAThread]
    private static int Main()
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        int result = 1;
        var api = new FakeApi();
        using (var form = new MainForm(new PowerPlanService(api), false, true))
        {
            form.Shown += async delegate
            {
                try
                {
                    var list = form.Controls.OfType<FlowLayoutPanel>().Single();
                    await Until(() => list.Controls.Count == 2);
                    Assert(list.Controls.OfType<PlanButton>().First().Plan.IsActive, "Initial active card is incorrect");
                    list.Controls.OfType<PlanButton>().ElementAt(1).PerformClick();
                    await Until(() => list.Enabled && list.Controls.OfType<PlanButton>().ElementAt(1).Plan.IsActive);
                    Assert(api.Active == api.B && api.Writes == 1, "Click did not activate the selected plan");
                    list.Controls.OfType<PlanButton>().ElementAt(1).PerformClick();
                    Assert(api.Writes == 1, "Clicking the active plan should not write to Windows");
                    form.Close();
                    Assert(!form.Visible && !form.IsDisposed, "Closing the window should hide it");
                    result = 0;
                    Console.WriteLine("PASS WinForms smoke: render, click, active-state update, no-op click, hide and exit (fake power API)");
                }
                catch (Exception ex) { Console.Error.WriteLine("FAIL " + ex); }
                finally { form.ExitApplication(); }
            };
            Application.Run(form);
        }
        return result;
    }
}
