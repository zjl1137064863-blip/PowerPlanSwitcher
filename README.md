# PowerPlanSwitcher · 电源计划切换器

当前版本：**v2.0.0**。从 [GitHub Releases](https://github.com/zjl1137064863-blip/PowerPlanSwitcher/releases/latest) 下载完整程序包；[查看本版更新](docs/releases/v2.0.0.md)。

一个轻量的 Windows WinForms 托盘小程序，可直接切换系统已有的电源计划，支持自定义计划与中文名称。界面采用紧凑的 Fluent 风格，跟随 Windows 深浅主题、强调色及高对比度设置。

## 直接使用

从 GitHub Releases 下载 `PowerPlanSwitcher.zip`，完整解压后双击 `PowerPlanSwitcher.exe`，无需安装。在主窗口点击计划卡片即可切换，左侧强调色竖条和已选圆圈表示当前计划。源码构建后的程序位于 `dist/`。

请将 `PowerPlanSwitcher.exe.config` 与 EXE 保存在同一文件夹；它用于启用 .NET Framework 的 PerMonitorV2 DPI 支持。

- **托盘切换**：右键任务栏通知区域的电源图标，直接选择计划；左键打开主窗口。图标可能收在通知区域的向上箭头内。
- **快捷键**：打开“全局快捷键”开关后，使用 `Ctrl + Alt + 1` 至 `9`，依次对应列表中的前九个计划，映射保持不变。注册冲突或权限限制会显示错误；关闭开关或退出时会释放快捷键。
- **关闭窗口**：“关闭窗口后驻留托盘”默认开启，`×` 或 `Alt + F4` 会隐藏窗口。关闭这个开关后，关闭窗口会直接退出。窗口使用原生标题栏，最小化后保留任务栏入口，支持最大化、缩放和系统贴靠。
- **保存偏好**：两个开关都保存在程序旁的 `settings.ini`；兼容旧版只有 `hotkeys=1` 的文件。保存失败时恢复原设置并显示错误。
- **自定义表情图标**：默认图标按方案列表顺序显示数字 `1、2、3、4…`。点击任一方案左侧的图标，或聚焦卡片后按 `F2`，打开图标编辑窗口。点击输入框后按 `Win + .` 或 `Win + ;`，从 Windows 表情面板选择表情，也可直接粘贴。右侧显示彩色预览，点击“保存”生效；“恢复默认”后再保存即可还原为数字。支持肤色、旗帜、家庭和职业等组合表情，具体可用字符取决于 Windows 字体版本。
- **图标记忆**：每个方案的图标按 GUID 保存在同一份 `settings.ini` 中，重启或修改方案名称后仍会保留；编辑图标不改变当前电源计划。取消或保存失败会保留原图标。
- **同步**：每五秒读取一次当前计划，也可以点击“刷新”。从 Windows 设置中切换或新增、删除计划后会同步。
- **后台启动**：运行 `PowerPlanSwitcher.exe --tray`，直接驻留托盘。
- **单实例**：重复打开程序会唤起已运行的窗口。

程序调用 Windows 原生电源管理 API，只选择现有计划，不创建计划、不修改具体参数，也不添加开机启动项。通常无需管理员权限；若系统策略限制切换，程序会显示 Windows 返回的错误。电源计划与 Windows 11 设置中的“电源模式”是不同的设置，部分设备只提供一个电源计划。

要求 Windows 10 / 11 和 .NET Framework 4.7.2 或更新版本。整个程序文件夹可以移动；放在可写目录中才能保存偏好。Windows 11 上会请求深色标题栏和系统圆角，系统不支持的原生效果会自动回退。

## 从源码构建

无需安装 .NET SDK，使用 Windows 自带的 .NET Framework C# 编译器：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

编译并执行测试：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

构建并生成用于 Release 的压缩包（仅包含 EXE、运行配置和说明，不包含个人设置）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test -Package
```

测试覆盖四种方案切换、失效计划、权限错误、策略覆盖、中文名称、控件状态、设置持久化、关闭行为、托盘菜单处理和热键生命周期。界面测试使用模拟电源接口，真实系统集成检查只读取计划，不改变当前计划。受限环境可能禁止热键注册，测试会明确标注跳过项。

100%、125%、150%、200% 的自动布局几何检查不等同于四种真实显示器 DPI 的人工验收。详见 [改造说明与验收记录](docs/Fluent-UI-验收.md)。

## 文件说明

- `src/PowerPlans.cs`：Windows API 与计划切换、结果核验。
- `src/MainForm.cs`：自适应主窗口、托盘、快捷键和自动同步。
- `src/FluentControls.cs`：可复用电源卡片、ToggleSwitch 和设置行。
- `src/EmojiIconDialog.cs`：接收系统表情面板输入，提供彩色预览、保存、取消和恢复默认。
- `src/EmojiRenderer.cs`：使用 Windows 自带的 Direct2D / DirectWrite 绘制 Segoe UI Emoji 彩色表情，不增加第三方依赖；无法使用时回退为单色字形。
- `src/Theme.cs`：集中管理深浅主题、高对比度、强调色与字体回退。
- `src/AppSettings.cs`：复用 INI 设置格式，支持原子保存和旧设置迁移。
- `src/NativeMethods.cs`：原生窗口效果与全局快捷键。
- `src/app.config`：运行时版本和 PerMonitorV2 DPI 配置。
- `src/Program.cs`：程序入口与单实例控制。
- `tests/ServiceTests.cs`：业务测试与只读系统集成检查。
- `tests/UiSmoke.cs`：WinForms、设置和原生热键检查。
- `tests/EmojiTests.cs`：Unicode 表情持久化、彩色像素、图标点击隔离及编辑窗口检查。Windows 表情面板的真实选词输入仍需人工验收。

可用 `PowerPlanSwitcher.exe --preview 路径.png` 导出窗口客户区的界面预览后退出，此模式不会启用托盘或快捷键；追加 `--dark` 或 `--light` 可以预览相应配色。预览不包含由系统绘制的标题栏。
