using System;
using System.Collections.Generic;
using System.Linq;
using PowerPlanSwitcher;

internal static class ServiceTests
{
    private sealed class FakeApi : IPowerApi
    {
        internal Guid A = Guid.NewGuid(), B = Guid.NewGuid();
        internal Guid Active;
        internal bool Fail, Ignore;
        internal int Writes;
        internal FakeApi() { Active = A; }
        public IList<PowerPlan> Enumerate() { return new List<PowerPlan> { new PowerPlan(A, "平衡"), new PowerPlan(B, "自定义 (静音)") }; }
        public Guid GetActive() { return Active; }
        public void SetActive(Guid id)
        {
            Writes++;
            if (Fail) throw new InvalidOperationException("Access denied");
            if (!Ignore) Active = id;
        }
    }
    private static int passed;
    private static void Test(string name, Action action)
    {
        action(); passed++; Console.WriteLine("PASS " + name);
    }
    private static void Assert(bool success, string message)
    { if (!success) throw new Exception(message); }
    private static void Throws(Action action, string expected)
    {
        try { action(); }
        catch (InvalidOperationException ex) { Assert(ex.Message.Contains(expected), "Unexpected error: " + ex.Message); return; }
        throw new Exception("Expected error was not raised.");
    }
    private static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        try
        {
            Test("Read and mark active plan; preserve custom Unicode names", delegate
            {
                var api = new FakeApi(); var service = new PowerPlanService(api);
                var plans = service.GetPlans();
                Assert(plans.Count == 2 && plans[0].IsActive && !plans[1].IsActive, "Wrong active plan");
                Assert(plans[1].Name == "自定义 (静音)", "Name changed");
            });
            Test("Switch and verify system state", delegate
            {
                var api = new FakeApi(); var service = new PowerPlanService(api); service.Activate(api.B);
                Assert(api.Active == api.B && service.GetPlans()[1].IsActive && api.Writes == 1, "Switch failed");
            });
            Test("Removed plan never reaches setter", delegate
            {
                var api = new FakeApi(); var service = new PowerPlanService(api);
                Throws(() => service.Activate(Guid.NewGuid()), "移除"); Assert(api.Writes == 0, "Unexpected write");
            });
            Test("Permission errors are reported without changing active plan", delegate
            {
                var api = new FakeApi { Fail = true }; var service = new PowerPlanService(api);
                Throws(() => service.Activate(api.B), "Access denied"); Assert(api.Active == api.A, "Active state changed");
            });
            Test("Detect policy override after switch", delegate
            {
                var api = new FakeApi { Ignore = true }; var service = new PowerPlanService(api);
                Throws(() => service.Activate(api.B), "组织策略");
            });
            Test("Empty friendly name has fallback", delegate
            { Assert(new PowerPlan(Guid.NewGuid(), "  ").Name == "未命名计划", "Missing fallback"); });
            Test("Native Windows API reads plans and active GUID (read-only)", delegate
            {
                var api = new NativePowerApi(); Guid before = api.GetActive();
                var plans = new PowerPlanService(api).GetPlans();
                Assert(plans.Any(p => p.Id == before && p.IsActive), "Active scheme absent");
                Assert(plans.All(p => !String.IsNullOrWhiteSpace(p.Name)), "Empty plan name");
                Assert(before == api.GetActive(), "Read changed system plan");
                foreach (var plan in plans) Console.WriteLine("  " + (plan.IsActive ? "* " : "  ") + plan.Name + " / " + plan.Id);
            });
            Console.WriteLine("All " + passed + " tests passed. No real power plan was changed."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL " + ex); return 1; }
    }
}
