# Power Switch Fluent UI 改造与验收

日期：2026-10-19。本文记录改造阶段的 WinForms 视觉、交互、相关 DPI/主题配置及回归测试；后续发布信息见版本更新说明。

## 改造前检查

已检查全部 C# 源码、构建脚本、manifest、图标、测试、README 和忽略配置。

| 项目 | 源码实际情况 |
|---|---|
| 框架 | C# 5 / WinForms，无 csproj，无 NuGet，使用系统 .NET Framework 编译器 |
| 目标版本 | 原来未显式声明目标框架，使用已安装框架引用；README 标注最低 4.5 |
| 本机 | .NET Framework Release 534296，当前显示器 120 DPI（125%） |
| 文件 | `src/MainForm.cs` 同时含主题、手绘 Button、窗口和托盘逻辑；业务接口在 `PowerPlans.cs` |
| 资源 | `src/app.ico`、`src/app.manifest`，没有 resx 或外部字体资源 |
| 窗口 | `FormBorderStyle.None`，手绘关闭按钮、手工拖动、绝对坐标布局 |
| DPI | `AutoScaleMode.None`，构造时读取一次 DPI，manifest 为 system-aware |
| 电源执行 | **没有 PowerShell 调用**。直接 P/Invoke `powrprof.dll`；使用 Task.Run，不阻塞 UI |
| 成功核验 | `PowerPlanService.Activate` 写入后调用 GetActive 验证；UI 进一步重新读取完整列表 |
| 偏好 | 程序目录 `settings.ini`，原来只有 `hotkeys=0/1`；关闭固定隐藏到托盘 |

`src/PowerPlans.cs` 保持原样：接口、GUID、枚举顺序、计划写入与核验逻辑均未修改；不创建或修改电源方案参数，不操作 Windows 电源模式。

## 界面与交互

- 恢复原生可调整尺寸标题栏、最小化、最大化、拖动及系统贴靠能力。
- 在支持的系统上请求 DWM 深色标题栏和 Windows 11 圆角；未启用 Mica。
- 移除大型当前计划卡片，列表标题右侧显示当前系统计划。
- 电源卡片为双缓冲 UserControl：80 个逻辑像素高、8 像素间距、4 像素圆角、1 像素中性边框、左侧 3 像素强调条及单选圆圈。
- 支持 Normal / Hover / Selected / Disabled / Switching；整卡、键盘 Enter/Space 和无障碍默认动作均可操作。
- 忙碌期间禁止重复切换；仅在接口返回且重新读取系统状态后更新选择；错误时保留上次确认的选择、恢复控件并显示错误和完整提示。
- 两个真实可保存开关：全局快捷键、关闭后驻留托盘。复用同一个 INI 文件，保留未知键，兼容旧偏好；保存失败恢复原开关值。
- 快捷键仍是枚举顺序的 Ctrl+Alt+1…9，没有排序或改变方案映射。
- 原生最小化保留任务栏入口；关闭时由驻留开关决定隐藏或退出。托盘始终提供重新打开和退出入口。
- 深浅主题及强调色来自 Windows，失败回退 #60CDFF；收到主题通知时重读；高对比度使用 SystemColors。
- 拉丁字优先 Segoe UI Variable Text / Segoe UI Variable / Segoe UI；中文显式优先 Microsoft YaHei UI / Microsoft YaHei。

## 新控件与文件清单

| 文件 | 修改内容 |
|---|---|
| `src/MainForm.cs` | TableLayoutPanel 自适应布局；接入卡片、设置行和主题；保留异步服务、托盘、热键；原生窗口生命周期 |
| `src/FluentControls.cs`（新增） | PowerPlanCard UserControl、ToggleSwitch CheckBox、SettingRow UserControl |
| `src/Theme.cs`（新增） | 集中管理配色、系统强调色、深浅主题、高对比度、字体和圆角路径 |
| `src/AppSettings.cs`（新增） | 延用 settings.ini，两个设置持久化、兼容原格式、临时文件及原子替换 |
| `src/NativeMethods.cs`（新增） | 抽取原热键/单实例互操作，新增 DWM 外观请求及热键错误码 |
| `src/Program.cs` | 显式 TargetFramework 4.7.2；保留单实例、--tray；改进客户区预览并可指定深浅主题 |
| `src/app.manifest` | 保留 Windows 10 兼容性与 asInvoker，移除会覆盖新 DPI 配置的旧 dpiAware |
| `src/app.config`（新增） | 最低 .NET Framework 4.7.2、PerMonitorV2 |
| `build.ps1` | 分发 EXE.config，新增 -Package 打包选项 |
| `tests/UiSmoke.cs` | 扩展真实 WinForms 生命周期、模拟电源后端、INI、主题、布局和原生热键回归 |
| `README.md` | 使用、配置、构建、分发与测试说明 |
| `docs/Fluent-UI-验收.md`（本文件） | 检查记录、验证边界和人工验收步骤 |

没有新增 NuGet 或第三方依赖，仍使用系统 .NET Framework 编译器。为启用框架提供的 PerMonitorV2，最低运行版本显式设为 4.7.2。分发时必须包含 `PowerPlanSwitcher.exe.config`。

参考：[Microsoft WinForms 高 DPI 配置](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/high-dpi-support-in-windows-forms?view=netframeworkdesktop-4.8)、[DWM 窗口属性](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute)。

## 已执行的验证

执行 `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test -Package` 构建、运行服务测试及 UI 测试并打包。热键注册在受限环境返回 Win32 错误 5；已另外在获准的桌面环境外运行同一 UI 测试，验证实际注册、消息派发和释放。

结果：7 项服务检查通过；最终桌面环境下 23 项 UI/设置检查通过。受限环境跳过的热键检查已在桌面复测补齐。Release ZIP 实际解压后验证启动、预览导出、`--tray` 隐藏启动、第二实例唤起已有窗口及原生关闭退出均成功。

| 验收项 | 自动验证与边界 |
|---|---|
| 编译 | 系统 C# 编译器成功生成 EXE，无新增依赖 |
| 启动 | 真实 WinForms 消息循环、当前 120 DPI 布局及客户区预览正常；发布 ZIP 解压后的实际进程通过 --tray / 重复启动唤起 / 正常关闭检查 |
| 四种方案 | 模拟接口依次切换四种计划，检查单一选中、忙碌、重复点击保护；没有改变真实 Windows 计划 |
| 实际系统读取 | 原生 API 读取四个现有 GUID、中文名称和实际激活计划，读取前后没有改变计划 |
| 异常 | 模拟访问拒绝、策略覆盖、读取失败；检查选中保留、明确错误、控件恢复 |
| 热键 | 实际注册四个原映射；投递 WM_HOTKEY 到模拟后端；检查冲突提示、禁用释放、句柄重建重新注册和退出释放。没有注入真实按键 |
| 托盘 | 创建 NotifyIcon；调用计划选择和重新打开菜单处理器；模拟后端状态正确。通知区域的实际鼠标点击尚需人工验证 |
| 关闭与最小化 | 在真实消息循环中执行最小化/恢复，关闭隐藏、菜单恢复、关闭退出和正常 Dispose |
| 配置 | 旧配置迁移、两个开关保存并重启恢复、保留未知键、无法写入时回滚 |
| 控件状态 | 五种卡片状态及无障碍 Checked 状态 |
| 布局 | 当前真实 125% DPI；100/125/150/200% 比例下执行 WinForms 布局几何检查，确认卡片不重叠、开关留在行内。模拟几何不等于真实监视器 DPI 测试 |
| 主题 | 深浅客户区渲染、高对比度配色模拟；发送主题消息后重新读取实际 Windows 主题和强调色。没有修改系统主题或高对比度设置 |
| PowerShell 异常 | 不适用：运行时没有 PowerShell 进程，原生接口异常路径已验证；PowerShell 仅用于构建 |

自动输出和界面预览位于被 Git 忽略的 `artifacts/`。`fluent-test-dark.png` / `fluent-test-light.png` 使用模拟方案；`fluent-dark.png` / `fluent-light.png` 为只读实际方案的客户区预览。预览不包含系统 DWM 绘制的标题栏。

## 尚需人工验收

1. 运行新程序，观察原生标题栏深浅配色、Windows 11 圆角、任务栏预览和 Win+Z / 拖动贴靠；最大化后还原，确认无异常空白或控件重叠。
2. 在 Windows 显示设置中分别使用 100%、125%、150%、200% 缩放，重新启动程序，检查中文完整性、图标比例、卡片高度、开关位置；在不同缩放的显示器间拖动窗口，检查动态 DPI。低可用高度时允许列表出现滚动条。
3. 先记录当前计划，依次点击四种现有计划，用 `powercfg /getactivescheme` 核对实际 GUID，最后切回原计划。确认切换后没有修改或新建任何方案。
4. 启用全局快捷键，焦点切换到其他应用，实际按 Ctrl+Alt+1…4，核对映射和当前计划；关闭开关后再次按键应无反应；结束后恢复原计划。
5. 关闭窗口，观察通知区域图标；真实左键重新打开，真实右键选择计划及退出。关闭驻留开关后点击 ×，应彻底退出，托盘无残留、热键不再触发。
6. 在 Windows 中实际切换浅色、深色和强调色；再打开辅助功能高对比度主题，检查文字、选中圈、焦点框和开关可读性，结束后恢复原设置。
7. 在其他 Windows 10 / 11 电脑上将 Release ZIP 解压到新目录，确认 EXE.config、README 与 EXE 同在；首次运行、--tray、重复启动唤起和设置重启恢复都正常。若在受组织策略限制的设备测试，确认错误信息明确且不会误报成功。本机解压启动和单实例流程已自动验证。

以上人工项目尚未全部完成，不将模拟接口、消息投递或比例布局检查表述为真实电源切换、物理快捷键或跨屏验证成功。
