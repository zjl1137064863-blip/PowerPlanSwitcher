using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace PowerPlanSwitcher
{
    public sealed class PowerPlan
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public bool IsActive { get; set; }

        public PowerPlan(Guid id, string name)
        {
            Id = id;
            Name = String.IsNullOrWhiteSpace(name) ? "未命名计划" : name;
        }
    }

    public interface IPowerApi
    {
        IList<PowerPlan> Enumerate();
        Guid GetActive();
        void SetActive(Guid id);
    }

    public sealed class PowerPlanService
    {
        private readonly IPowerApi api;
        public PowerPlanService(IPowerApi api) { this.api = api; }

        public IList<PowerPlan> GetPlans()
        {
            IList<PowerPlan> plans = api.Enumerate();
            Guid active = api.GetActive();
            foreach (PowerPlan plan in plans) plan.IsActive = plan.Id == active;
            if (plans.Count == 0) throw new InvalidOperationException("系统没有可用的电源计划。");
            return plans;
        }

        public void Activate(Guid id)
        {
            if (!api.Enumerate().Any(p => p.Id == id))
                throw new InvalidOperationException("这个计划已被移除，请刷新后重试。");
            api.SetActive(id);
            if (api.GetActive() != id)
                throw new InvalidOperationException("系统没有保持所选计划，可能受到组织策略或设备管理软件的控制。");
        }
    }

    // Use the Windows power API directly: no subprocess, console, or locale parsing.
    public sealed class NativePowerApi : IPowerApi
    {
        private const uint AccessScheme = 16;
        private const uint NoMoreItems = 259;
        private const uint MoreData = 234;

        public IList<PowerPlan> Enumerate()
        {
            var plans = new List<PowerPlan>();
            for (uint index = 0; ; index++)
            {
                byte[] buffer = new byte[16];
                uint size = (uint)buffer.Length;
                uint result = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    AccessScheme, index, buffer, ref size);
                if (result == NoMoreItems) break;
                Check(result, "读取电源计划失败");
                if (size != 16) throw new InvalidOperationException("系统返回了无效的电源计划标识。");
                Guid id = new Guid(buffer);
                plans.Add(new PowerPlan(id, ReadName(id)));
            }
            return plans;
        }

        private static string ReadName(Guid id)
        {
            uint size = 0;
            uint result = PowerReadFriendlyName(IntPtr.Zero, ref id, IntPtr.Zero,
                IntPtr.Zero, null, ref size);
            if (result != 0 && result != MoreData) Check(result, "读取计划名称失败");
            if (size == 0) return "未命名计划";
            byte[] buffer = new byte[size];
            Check(PowerReadFriendlyName(IntPtr.Zero, ref id, IntPtr.Zero,
                IntPtr.Zero, buffer, ref size), "读取计划名称失败");
            return Encoding.Unicode.GetString(buffer, 0, (int)size).TrimEnd('\0');
        }

        public Guid GetActive()
        {
            IntPtr pointer;
            Check(PowerGetActiveScheme(IntPtr.Zero, out pointer), "读取当前计划失败");
            try { return (Guid)Marshal.PtrToStructure(pointer, typeof(Guid)); }
            finally { LocalFree(pointer); }
        }

        public void SetActive(Guid id)
        {
            Check(PowerSetActiveScheme(IntPtr.Zero, ref id), "切换电源计划失败");
        }

        private static void Check(uint result, string action)
        {
            if (result != 0)
                throw new InvalidOperationException(action + "：" + new Win32Exception((int)result).Message
                    + "（错误 " + result + "）");
        }

        [DllImport("powrprof.dll")]
        private static extern uint PowerEnumerate(IntPtr root, IntPtr scheme, IntPtr subgroup,
            uint access, uint index, [Out] byte[] buffer, ref uint size);
        [DllImport("powrprof.dll")]
        private static extern uint PowerReadFriendlyName(IntPtr root, ref Guid scheme,
            IntPtr subgroup, IntPtr setting, [Out] byte[] buffer, ref uint size);
        [DllImport("powrprof.dll")]
        private static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr scheme);
        [DllImport("powrprof.dll")]
        private static extern uint PowerSetActiveScheme(IntPtr root, ref Guid scheme);
        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);
    }
}
