# 折叠屏场景：网易 UU 超级屏 + Codex

[返回 README](../README.zh-CN.md) · [English below](#english)

展开折叠屏，在同一张远程桌面上阅读 Codex 回复、编辑提示词，再直接点下方的大按键输入。
这张配图基于作者提供的实际场景照片，经局部重绘处理。使用 UU 超级屏的说明来自作者；仅凭照片不能
确认客户端版本、功能开关或实际串流分辨率。

![折叠屏上的 Codex 与 Lucky Touch Input，标题和聊天区经 AI 替换为示例](../assets/foldable-uu-codex.png)

图中是早期 Alpha 布局，不是开发中的手机大键预设。图片经过裁剪、轻度去噪和外围背景整理；
私人标题、聊天及内嵌截图由 AI 替换成简短的示例对话，不再使用马赛克。设备、键盘、输入框、
模型选择、Windows 开始按钮与时间等沿用原图；示例文字不是原始会话或实际执行记录。

## 三部分各自做什么

- **UU 远程 / 超级屏**：负责远程连接和桌面画面。网易的官方商店介绍将超级屏描述为可突破
  被控端物理屏幕的分辨率与帧率限制；具体选项以安装的客户端为准。
  [来源：网易发布的 App Store 页面](https://apps.apple.com/ae/app/uu%E8%BF%9C%E7%A8%8B-%E8%BF%9C%E7%A8%8B%E5%8A%9E%E5%85%AC-%E6%B8%B8%E6%88%8F%E4%B8%B2%E6%B5%81/id1642306791)。
- **Codex**：运行在 Windows 主机上的目标应用，上方区域用于阅读与编辑。
- **Lucky Touch Input**：同样运行在 Windows 主机上，下方提供按键并调整普通目标窗口，
  为键盘和任务栏留出空间。它不是手机输入法，也不负责远程传输。

普通 Windows 触屏平板也可以直接使用后两部分，不必建立 UU 连接。

## 如何尝试

1. 展开设备并连接 Windows 主机。如果当前 UU 客户端提供“超级屏”，选择适合当前屏幕比例
   的桌面画面。先让远程画面尺寸稳定，再调整键盘；不需要照抄照片的尺寸。
2. 在 Windows 上打开 Codex 和 Lucky Touch Input，先点 Codex 输入框，再切到“固定应用”。
   如果需要在多个编辑器之间切换，可以改用“跟随前台”。
3. 在键盘设置中保留“回车常驻”，按实际手感调整高度。检查输入框、最后一行内容和任务栏
   是否都能看到，再试一次输入、回车和滚动。图中的候选按钮由当前 Windows 输入法决定是否响应。
4. 使用此方案时，输入由远程桌面上的 Lucky Touch Input 完成，不必同时唤起手机自身的键盘。
   若手机键盘已经出现，先收起它再试。Windows 原生键盘先不启用抑制；alpha.1 的默认策略
   为自动让位，并提供实验性的互斥，详见 [README](../README.zh-CN.md#常用布局与系统键盘互斥)。

## 适用边界

- 超级屏的画面设置与 Lucky Touch Input 的窗口避让是两件事。本程序不能控制手机端键盘，
  也不能保证阻止所有远控客户端的画面上移、缩放或遮挡。
- 海外 App Store 页面能访问，不等于一个独立“国际版”或 Android 客户端具有相同菜单和功能。
  请以实际安装版本为准；这里不承诺所有地区、设备或客户端都已验证。
- 图片展示上下布局，不用于证明具体设备型号、原生分辨率、清晰度、延迟或持续稳定性。
  正式测试范围见[验收说明](VALIDATION.md)。
- 不需要采用原照片中的权限或模型选择，也不支持向 UAC 安全桌面或高权限窗口输入。

## 图片与隐私

公开文件为 `assets/foldable-uu-codex.png`（1373 × 1047）；这是裁剪后的文件尺寸，
不是设备分辨率。私人窗口标题、聊天和内嵌截图已替换为普通的项目说明示例；原图的模型
选择、权限标签、开始按钮、任务栏和时间按作者意愿保留。检查未发现明显不适合展示的内容；
示例对话不代表真实会话，无关的私人项目讨论不作为本项目功能宣传。

AI 编辑仅用于外围背景及标题/聊天区域。键盘、设备外形、输入框与任务栏保留原图像素位置，
只作轻度传统去噪，没有采用 AI 重画的键位、模型选项或系统图标。最终导出为扁平 PNG，
不嵌入未处理原图、隐藏图层、EXIF、文本元数据或原始缩略图；未处理原图不纳入仓库或发布包。

图片来源及第三方界面归属见[第三方说明](THIRD-PARTY.md)。

## English

This locally retouched, author-supplied foldable photo illustrates a UU Super Screen remote session: Codex in the upper
area, Lucky Touch Input below, and the Windows taskbar beneath the keyboard. Both applications run
on the Windows host. Lucky Touch Input is neither a phone IME nor a remote transport or Codex plugin.
The author's description identifies the Super Screen setup; the photo alone cannot verify its
toggle, client version, or streaming resolution.

### Setup

1. Unfold the device and connect to Windows. If the installed UU client offers Super Screen, choose
   a desktop canvas suited to the device before adjusting the keyboard.
2. Open both Windows applications, focus the Codex composer, then select **Pinned application**
   (`固定应用`). Use **Follow foreground** to work across multiple editors.
3. Keep Enter set to stay open, adjust keyboard height, and check that the composer, last line, and
   taskbar are visible. Try typing, Enter, and scrolling. Candidate paging depends on the Windows IME.
4. Tap Lucky Touch Input in the streamed desktop rather than opening the phone's own keyboard.
   Start with Windows keyboard suppression off; consult the [README](../README.md#troubleshooting)
   if the native keyboard repeatedly overlaps.

UU owns the connection and canvas; Lucky Touch Input owns host-side keys and window avoidance.
An overseas store listing does not establish feature parity for a separately branded international
edition or Android client. This setup cannot prevent every client-side pan or keyboard overlay.
A Windows touchscreen tablet can use Lucky Touch Input directly without UU.

### Photo scope and privacy

This is an early-alpha layout, not the new phone large-key preset or a benchmark. No device model,
native resolution, latency, or universal compatibility claim is inferred from the photo.
The 1373 × 1047 asset dimensions describe only the cropped image.

The private title, conversation, and embedded screenshot were replaced with AI-generated demonstration
content, not mosaic. These words are not the original conversation or a record of executed work.
AI was also used for the surrounding background. The actual device, keyboard, composer controls,
model selector, Start button, taskbar, and clock retain the original photo with mild conventional
denoising. The author chose to leave those ordinary UI details visible.
The flattened PNG embeds no unedited original, hidden layers, EXIF, text metadata, or original
thumbnail. The unedited source photo is not included in the repository or packages.

This example does not recommend any particular Codex permission/model setting or support input to
the UAC secure desktop. See [validation](VALIDATION.md) for measured coverage and
[third-party notices](THIRD-PARTY.md) for image attribution. Super Screen's advertised display
capability is described in the [official NetEase App Store listing](https://apps.apple.com/ae/app/uu%E8%BF%9C%E7%A8%8B-%E8%BF%9C%E7%A8%8B%E5%8A%9E%E5%85%AC-%E6%B8%B8%E6%88%8F%E4%B8%B2%E6%B5%81/id1642306791).
