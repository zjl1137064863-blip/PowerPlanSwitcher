# 电源计划切换器

一个轻量的 Windows 托盘小程序，可直接切换系统已有的电源计划，支持自定义计划与中文名称。

## 直接使用

双击 `dist/PowerPlanSwitcher.exe`，无需安装。在主窗口点击计划卡片即可切换，绿色边框表示当前计划。

- **托盘切换**：右键任务栏通知区域的电源图标，直接选择计划；左键打开主窗口。图标可能收在通知区域的向上箭头内。
- **快捷键**：勾选窗口底部的开关后，使用 `Ctrl + Alt + 1` 至 `9`，依次对应窗口中的前九个计划。快捷键冲突会显示在对应卡片上；关闭开关或退出时会释放快捷键。此偏好保存在程序旁的 `settings.ini`。
- **关闭窗口**：窗口的 `×`、最小化按钮或 `Alt + F4` 都会隐藏到托盘。点击“退出程序”才能完全退出。
- **同步**：每五秒读取一次当前计划，也可以点击“刷新”。从 Windows 设置中切换或新增、删除计划后会同步。
- **后台启动**：运行 `PowerPlanSwitcher.exe --tray`，直接驻留托盘。
- **单实例**：重复打开程序会唤起已运行的窗口。

程序调用 Windows 原生电源管理 API，只选择现有计划，不创建计划、不修改具体参数，也不添加开机启动项。通常无需管理员权限；若系统策略限制切换，程序会显示 Windows 返回的错误。电源计划与 Windows 11 设置中的“电源模式”是不同的设置，部分设备只提供一个电源计划。

要求 Windows 10 / 11 和 .NET Framework 4.5 或更新版本（当前电脑已具备）。整个 `dist` 文件夹可以移动；不要把它放在无法写入的目录中，否则快捷键偏好无法保存。

## 从源码构建

无需安装 .NET SDK，使用 Windows 自带的 .NET Framework C# 编译器：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

编译并执行测试：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

测试覆盖切换、失效计划、权限错误、策略覆盖和中文计划名称，以及界面点选、状态更新、重复点击、隐藏和退出流程。界面测试使用模拟电源接口，真实系统集成检查只读取计划，不改变当前计划。

## 文件说明

- `src/PowerPlans.cs`：Windows API 与计划切换、结果核验。
- `src/MainForm.cs`：主窗口、托盘、快捷键和自动同步。
- `src/Program.cs`：程序入口与单实例控制。
- `tests/ServiceTests.cs`：业务测试与只读系统集成检查。
- `artifacts/preview.png`：构建时生成的界面预览（如有）。

可用 `PowerPlanSwitcher.exe --preview 路径.png` 导出自身窗口的界面预览后退出，此模式不会启用托盘或快捷键。
