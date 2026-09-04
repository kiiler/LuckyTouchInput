# Lucky Touch Input

面向 Windows 平板与远控场景的自适应触控输入面板。

> 当前为 Alpha。Lucky Touch Input 不负责建立远程连接，也不是输入法；它运行在 Windows
> 主机侧，可以配合 RustDesk、TeamViewer、UU远程、Moonlight 和 Windows App 使用。

[English](README.md)

![Lucky Touch Input 主键盘界面](assets/lucky-touch-input-main.png)

## 解决什么问题

用手机或平板远控 Windows 时，客户端键盘经常难以唤醒、遮住输入框，或者让桌面突然上移。
Windows 平板直接使用时，系统键盘也可能过于密集。Lucky Touch Input 在主机侧提供大按键、
稳定常驻和可预测的窗口避让。

## 0.1 Alpha 功能

- 无焦点激活的置顶触控面板，点按按键时尽量保留编辑器光标。
- 跟随前台、固定应用、自由悬浮三种模式。
- 适配手机、平板、折叠屏、DPI 缩放和多显示器。
- 标准 QWERTY、常驻数字行、Shift、退格、回车、候选翻页和滚轮。
- 退出即清空的会话片段，以及只插入、不自动提交的提示词快捷入口。
- 可选的微软拼音全拼/小鹤双拼切换。
- 可选的 Windows 原生触摸键盘抑制，并带异常退出恢复租约。
- 当前用户登录启动开关和系统托盘恢复入口。
- 不联网、无遥测、无账号、无自动更新、无第三方运行时包。

## 运行要求

- Windows 11 x64
- 使用依赖框架版本时需要 .NET 10 Desktop Runtime
- 不需要管理员权限

正式发布包计划采用自包含便携格式。

## 快速开始

1. 解压 Windows x64 便携包，运行 `LuckyTouchInput.exe`。
2. 先点一下要输入的编辑器或文本框；面板会跟随该前台窗口，并在两者之间保留很小的缝隙。
3. 点击模式按钮可在**跟随前台**、**固定应用**、**自由悬浮**之间切换；点击**设置**可调整高度、
   左侧缝隙、回车行为、登录启动，以及是否与 Windows 原生触摸键盘共存。
4. 两秒内连续点击两次 `—` 可以防误触地收起；双击托盘图标或选择“显示键盘”即可恢复。
   点击 `×` 或托盘菜单中的“退出”可以完整退出。

远控场景建议先使用“跟随前台 + 回车常驻 + 系统键盘共存”。只有 Windows 原生键盘反复覆盖
远控画面时，再在设置里启用实验性的“系统键盘：抑制”。

## 构建与运行

安装 .NET 10 SDK 后执行：

```powershell
dotnet build LuckyTouchInput.sln -c Release
dotnet run --project tests/LuckyTouchInput.Tests -c Release
dotnet run --project src/LuckyTouchInput/LuckyTouchInput.csproj -c Release
```

在交互式 Windows 桌面执行 `pwsh -File tests/Invoke-UiAcceptance.ps1`，可以实际验证无焦点按键、
目标窗口避让与恢复，以及默认状态不会改动系统键盘注册表。

生成带 SHA-256 校验文件的自包含 x64 便携包：

```powershell
pwsh -File scripts/Publish.ps1
```

## 三种模式

- **跟随前台**：跟随并避让当前普通前台窗口。
- **固定应用**：固定到切换模式时选中的进程。
- **自由悬浮**：保持输入面板，不调整目标窗口。

设置保存在 `%LOCALAPPDATA%\Lucky\TouchInput\settings.ini`。默认回车后保持键盘，默认与
Windows 原生触摸键盘共存。

## 重要边界

- 文字组合、词库和候选由当前 Windows 输入法负责。本程序的候选按钮只发送 Page Up / Page Down。
- Windows 完整性级别会限制 `SendInput`，不支持向高权限窗口或 UAC 安全桌面输入。
- 原生键盘抑制会临时修改当前用户的输入设置，因此属于实验选项；程序会保存原值，并在退出或
  下次启动发现异常租约时恢复。
- 首个 Alpha 的界面以中文为主；交互稳定后再补完整英文资源。

更多信息参见[架构](docs/ARCHITECTURE.md)、[隐私](docs/PRIVACY.md)、
[安全说明](SECURITY.md)和[贡献指南](CONTRIBUTING.md)。

## 当前状态

`0.1.0-alpha` 是首个公开设计基线。签名发布前仍需在干净的 Windows 安装和更多真实触控设备上
验收，并补充正式应用图标。

## 许可证

[MIT](LICENSE)

<details>
<summary>作者与支持</summary>

作者：free2ing（零西） · haoyunmail@qq.com

<img src="assets/support-alipay.png" alt="支付宝支持" width="120">
<img src="assets/support-wechat.png" alt="微信支持" width="120">

</details>
