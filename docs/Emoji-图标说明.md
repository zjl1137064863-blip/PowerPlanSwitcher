# 自定义表情图标

本次在已有 Fluent WinForms 界面上增加按方案自定义表情的能力，电源业务接口和现有 GUID 保持不变。

## 使用

1. 点击方案卡片左侧的图标，或使用 Tab 聚焦卡片后按 F2。
2. 点击输入框，按 Windows + 句号或 Windows + 分号，选择系统表情；也可以粘贴表情或简短符号。
3. 右侧为彩色绘制预览。点击“保存”后立即更新卡片，并写入程序旁的 settings.ini。
4. 点击“恢复默认”，再点击“保存”，删除这个方案的自定义图标并恢复按列表顺序显示的数字 1、2、3、4…；预览会显示对应数字。取消不会保存改动。

图标使用 `icon.<方案 GUID>=<Unicode 内容>` 保存，兼容原来的设置格式；方案重命名不影响对应关系。保存失败时，编辑窗口保留并显示错误，卡片继续显示上一次成功保存的图标。

输入框使用标准 WinForms TextBox 接收系统输入，预览与卡片使用系统 Direct2D / DirectWrite 和 Segoe UI Emoji 彩色绘制。框架仍为 WinForms，没有新增 NuGet、网络图片或字体下载。高对比度模式和渲染能力不可用时允许显示单色字形。可用表情取决于安装的 Windows 字体版本。

## 修改文件

- src/AppSettings.cs：按 GUID 读写图标、UTF-16 有效性和长度检查，复用原子保存。
- src/FluentControls.cs：卡片支持自定义表情、图标点击和 F2 编辑，绘制资源随控件释放。
- src/EmojiRenderer.cs（新增）：Windows 原生彩色字体渲染桥接，不替换 UI 框架。
- src/EmojiIconDialog.cs（新增）：输入、预览、保存、取消和恢复默认。
- src/MainForm.cs：接入编辑窗口、保存回滚、卡片立即更新和入口提示。
- tests/EmojiTests.cs（新增）：表情序列、彩色像素、编辑窗口与点击隔离检查。
- tests/UiSmoke.cs：增加四张卡片保存、重启恢复、单独重置和保存失败回滚。
- build.ps1：编译并运行新增测试。
- README.md、本说明：更新使用和验收信息。

## 验证范围

自动检查涵盖：普通表情、肤色、ZWJ 职业/家庭组合、旗帜、键帽的 UTF-8 文件往返；方案之间互不影响；恢复默认；Direct2D 实际彩色像素；图标点击不触发计划切换；编辑窗口保存完整序列、保存错误时保持打开；主窗口卡片立即更新、重启恢复与保存失败保留原图标。

最终结果：编译与打包成功，7 项服务检查、24 项桌面 UI/设置检查、4 组表情专项检查通过。热键在受限环境中的跳过项已在获准的桌面环境中复测，包括四个实际热键注册、WM_HOTKEY 派发、冲突处理、句柄重建和退出释放。

仍需人工验证：在新程序编辑框中实际按 Win + . / Win + ;，从系统面板选择并保存表情；确认面板选词焦点和不同 Windows 字体版本的显示效果。自动测试使用完整 Unicode 文本模拟输入，不声称已完成真实系统表情面板交互。

可执行 `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test -Package` 重建并打包。测试不修改真实 Windows 电源计划。

参考：[Microsoft 彩色字体支持](https://learn.microsoft.com/en-us/windows/win32/directwrite/color-fonts)。
