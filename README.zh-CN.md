# Lucky Touch Input

## 紧凑悬浮键盘（fork 实验功能）

新增独立的右侧紧凑键盘，支持顶部拖动、右下角缩放、＋／−调节、左右换边、收起展开和位置记忆。
沿用 Windows 当前输入法，不调整目标应用窗口。原版入口和配置保持独立。

构建 `src/LuckyTouchInput.CompactTest`，或运行 `pwsh -File scripts/Publish.ps1 -Variant Compact`。

- [需求与验收](docs/COMPACT-REQUIREMENTS.md)
- [技术方案](docs/COMPACT-DESIGN.md)
- [开发与发布](docs/COMPACT-DEVELOPMENT.md)
- [验证结果与限制](docs/COMPACT-VALIDATION.md)

已获得手机远控初步验证反馈；中文候选和长时间稳定性仍需进一步验证。


面向 Windows 平板与远控场景的自适应触控输入面板。

> 当前为 Alpha。Lucky Touch Input 不负责建立远程连接，也不是输入法；它运行在 Windows
> 主机侧。可在远控客户端传回的桌面上操作，也可以在 Windows 触屏上直接用；不要求特定客户端。

[English](README.md)

`0.1.0-alpha.1` 提供“标准 / 手机大键 / 常用”三种布局，并重新整理 Windows 键盘
让位与互斥行为。常用布局增加 Tab、单次 Ctrl、方向键与编辑面板；手机大键保留更简洁的底行。
本预览版通过 350 项自动检查与 59 项离线 Windows 11 VM 交互检查；验收边界见下文。

[下载 Windows x64 便携版](https://github.com/fofc12/LuckyTouchInput/releases/tag/v0.1.0-alpha.1) ·
[反馈问题](https://github.com/fofc12/LuckyTouchInput/issues)

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
- 不联网、无遥测、无账号、无自动更新；除 .NET/WinForms 外没有第三方 NuGet 依赖。

## 运行要求

- Windows 11 x64
- 使用依赖框架版本时需要 .NET 10 Desktop Runtime
- 不需要管理员权限

下载 ZIP 解压即可运行，已经包含 .NET 运行时，无需另行安装。首版未做代码签名；如 Windows
拦截，请先核对下载来源与 SHA-256，不要关闭系统安全防护。暂不提供 ARM64 包。

## 快速开始

1. 解压 Windows x64 便携包，运行 `LuckyTouchInput.exe`。
2. 先点一下要输入的编辑器或文本框；面板会跟随该前台窗口，并在两者之间保留很小的缝隙。
3. 点击模式按钮可在**跟随前台**、**固定应用**、**自由悬浮**之间切换；点击**设置**可调整高度、
   左侧缝隙、回车行为、登录启动，以及是否与 Windows 原生触摸键盘共存。
4. 两秒内连续点击两次 `—` 可以防误触地收起；双击托盘图标或选择“显示键盘”即可恢复。
   点击 `×` 或托盘菜单中的“退出”可以完整退出。
   再运行一次 EXE 也会恢复已有键盘，不会叠加第二个实例。

远控场景先使用“跟随前台 + 回车常驻”，再按实际冲突情况选择系统键盘策略。
首个 Alpha 的选项名为“共存 / 抑制”；alpha.1 改为“让位 / 互斥”，默认让位，具体行为见下文。

## 使用场景：折叠屏 + UU 超级屏 + Codex

折叠屏展开后，通过网易 UU 远程查看 Windows 桌面：上方留给 Codex 阅读和编辑，
下方使用运行在 Windows 上的 Lucky Touch Input，任务栏保留在键盘下面。
UU 负责连接与桌面画面，Lucky Touch Input 负责主机侧按键与窗口避让，不需要 Codex 插件。

![折叠屏使用示例：Codex 在上、键盘与任务栏在下；聊天区经 AI 替换](assets/foldable-uu-codex.png)

基于作者提供的早期 Alpha 实拍，经过裁剪、轻度去噪和背景整理；私人标题与聊天区用 AI
替换成示例内容，键盘、开始按钮、模型选择与时间等保留原图。它不是未经修改的会话截图，
也不是新版手机大键布局截图或延迟、全客户端兼容性验收。
设置方式、国际版边界与图片处理说明见[折叠屏场景指南](docs/FOLDABLE-UU.md)。

## 构建与运行

安装 .NET 10 SDK 后执行：

```powershell
dotnet build LuckyTouchInput.sln -c Release
dotnet run --project tests/LuckyTouchInput.Tests -c Release
dotnet run --project src/LuckyTouchInput/LuckyTouchInput.csproj -c Release
```

在没有旧键盘配置的临时 Windows 用户或测试虚拟机中执行
`pwsh -File tests/Invoke-UiAcceptance.ps1`，验证实际输入、模式切换、回车策略、设置、滚轮、
重复启动恢复和系统设置往返。脚本拒绝覆盖现有用户配置；不要在日常工作桌面上运行完整验收。

生成带 SHA-256 校验文件的自包含 x64 便携包：

```powershell
pwsh -File scripts/Publish.ps1
```

## 三种模式

- **跟随前台**：跟随并避让当前普通前台窗口。
- **固定应用**：固定到切换模式时选中的进程。
- **自由悬浮**：保持输入面板，不调整目标窗口。

设置保存在 `%LOCALAPPDATA%\Lucky\TouchInput\settings.ini`。默认回车后保持键盘，不启用
Windows 触摸键盘抑制。

## 常用布局与系统键盘互斥

在“设置 → 布局”循环切换：

| 布局 | 用途与默认高度 |
| --- | --- |
| 标准 | 保留原有操作入口；跟随/固定 42%，悬浮 34% |
| 手机大键 | 简洁五键底行、加宽右下回车；48%（悬浮模式上限 44%） |
| 常用 | 桌面式错位字母行，Tab、Ctrl、左右方向键；44% |

高度仍可调整，切换布局会采用该布局的默认高度。旧 `LargeKeys` 设置会迁移，原有偏好不会
被强制切成新布局。主机无法仅凭 4K 远程桌面推断手机的物理尺寸；这些预设不改变 Windows
缩放或分辨率。

![常用布局，1280 像素宽的实际控件绘制](assets/lucky-touch-input-everyday.png)

上图由 WinForms 控件绘制，用于检查排版，不是手机实拍或输入成功的证明。
Ctrl 与 Shift 都只作用于下一次按键，再点一次可取消；支持 Ctrl+A、Shift+Tab、Shift+方向键等。
“工具 → 编辑”提供全选、复制、粘贴、剪切、撤销、重做、Home/End 和上下方向键。
“工具”还保留滚轮及候选翻页。回车收起选项只作用于单独回车，不主动收起 Shift/Ctrl+回车。

系统键盘策略可在设置或托盘菜单中切换：

| 情况 | 行为 |
| --- | --- |
| 系统键盘：让位（默认） | 检测到目标显示器的 Windows 键盘时，暂时隐藏 Lucky 并恢复目标窗口；不修改系统键盘偏好 |
| 系统键盘：互斥（实验） | 仅在 Lucky 展开且目标有效时临时抑制触摸键盘；收起、目标消失或退出时释放设置租约 |
| 辅助功能“屏幕键盘” OSK | 无论上述选项如何，都让位；不关闭 OSK，不改其自启动，不停用 Windows 输入服务 |

Windows 键盘关闭后，Lucky 会在目标有效时恢复。若触摸键盘无法隐藏，就停止反复抢占，
等待其关闭；双击托盘或再次运行 EXE 可主动重试。恢复设置时，只恢复仍保持本程序写入值的
选项；运行期间检测到的用户或系统新值优先。强制结束后，租约留待下次启动恢复。

现代触摸键盘需要窗口与输入面板位置共同确认，不会仅凭 `TextInputHost` 进程就隐藏候选、
表情或语音窗口。Windows 未报告有效面板位置时，可能无法自动互斥；此时请用系统触摸键盘
设置或选择让位，不要停用服务。OSK 与触摸键盘的区别见
[微软说明](https://support.microsoft.com/en-us/accessibility/windows/use-the-on-screen-keyboard-osk-to-type)。
UAC 安全桌面、高权限输入及真实 UU 连接下的短暂闪现仍须实测。

## 更新与移除

- 更新：先退出键盘，再把新 ZIP 解压到原目录。设置保留；换目录后请重新开关一次“启动”。
- 从“零西触键”试用版迁移：首次启动、且新配置尚不存在时，会读取旧 `LuckyTouchKeyboard`
  配置，保留高度、常驻和抑制选项；旧文件不改动。旧 Codex 模式映射为固定应用，UU 模式映射为跟随前台。
- 升级前先退出旧版并关闭其登录入口，避免两套程序同时管理窗口。本程序不会自动停止第三方键盘或改写其启动脚本。
- 移除：关闭“启动”，退出程序后删除解压目录。需要清除偏好时，再删除上述用户配置目录。
  若曾强制结束抑制中的程序，请先重新启动一次，再正常退出，以恢复原生键盘设置。

## 常见问题

- 数字不见了：设置、变量、提示面板暂时借用数字行，点亮的按钮会改名为“数字”，点它返回。
- 候选翻页不生效：本程序发送 Page Up / Page Down，具体行为由输入法负责；没有自建候选框。
- 系统键盘突然出现：先区分触摸键盘与辅助功能的“屏幕键盘”（`osk.exe`）。实验抑制只处理前者，
  不会关闭 Windows 输入服务或强制结束 OSK。检测到 OSK 时会自动让位。
  目标应用关闭后，本程序仍在托盘运行，释放抑制，不再占住画面。
  若出现的是黑色 OSK，请在 Windows“设置 → 辅助功能 → 键盘”关闭“屏幕键盘”，并检查
  辅助功能中心的登录启动选项；不要停用整个 Windows 输入服务。屏幕键盘与触摸键盘是两套独立功能。
- 窗口不适合被调整：选择“自由悬浮”。窗口最小尺寸、游戏、特殊弹窗和高权限窗口可能限制避让效果。
- 双拼：微软拼音快捷开关不会覆盖已有的自定义方案；已有其他方案时，请在 Windows 拼音设置中选择。

## 重要边界

- 文字组合、词库和候选由当前 Windows 输入法负责。本程序的候选按钮只发送 Page Up / Page Down。
- Windows 完整性级别会限制 `SendInput`，不支持向高权限窗口或 UAC 安全桌面输入。
- 原生键盘抑制会临时修改当前用户的输入设置，因此属于实验选项；程序保存恢复租约，alpha.1 在
  不拥有输入面板时即释放。它不是系统级禁用；极短闪现仍需按 Windows 版本与远控客户端实测。
- 首个 Alpha 的界面以中文为主；交互稳定后再补完整英文资源。

更多信息参见[架构](docs/ARCHITECTURE.md)、[隐私](docs/PRIVACY.md)、
[安全说明](SECURITY.md)和[贡献指南](CONTRIBUTING.md)。

## 当前状态

`0.1.0-alpha.1` 通过 350 项配置/布局/键盘策略检查与 59 项离线 Win11 交互检查，
覆盖实际文字、修饰键、设置与模式、回车、滚轮、重复启动、最小化、两种策略下的 OSK 让位
和恢复，以及崩溃后的系统设置恢复。1280×752 工作区下，可见窗口间隙 3 px，任务栏无重叠。
具体结果与尚未验证的范围见[验收说明](docs/VALIDATION.md)。

![Alpha.1 在 Windows 11 上的 1280×752 验收界面](assets/lucky-touch-input-clean-win11-alpha1.png)

不同手机、折叠屏和远控客户端的真实触摸体验仍需反馈；虚拟机通过不代表所有设备已经验证。

## 许可证

[MIT](LICENSE)

<details>
<summary>作者与支持</summary>

作者：free2ing（零西） · haoyunmail@qq.com

<img src="assets/support-alipay.png" alt="支付宝支持" width="120">
<img src="assets/support-wechat.png" alt="微信支持" width="120">

</details>
