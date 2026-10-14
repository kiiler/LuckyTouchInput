# 紧凑悬浮键盘：技术方案

## 决策

以独立的 Windows WinForms 实验入口交付，保留原版入口。
上游的 Floating 仅停止调整目标窗口，原来的布局计算仍然全宽贴底。
新入口拥有自己的四行界面和窗口位置策略，复用 Native 按键发送与 RoundedButton。
不通过外部脚本持续移动窗口，也不替换输入法。

## 项目及职责

| 位置 | 职责 |
| --- | --- |
| src/LuckyTouchInput | 原版应用及共享 Win32、按键、设置和按钮实现 |
| src/LuckyTouchInput.CompactTest | 独立入口、紧凑界面、几何和配置 |
| tests/LuckyTouchInput.CompactTests | 快速布局检查及显式启用的独立交互检查 |
| scripts/Publish.ps1 -Variant Compact | 自包含 ZIP、运行时许可证、链接检查与 SHA-256 |

紧凑项目通过源文件链接复用原版代码，使用 COMPACT_TEST 编译符号隔离诊断目录。
这是小型实验项目的取舍；不会在原版中引入运行时插件系统。
Native.ShowExistingKeyboard 接受窗口标题，默认仍为原版标题。
测试程序不再编译到产品入口，也不随便携包发布。

## 状态和几何

展开矩形与收起按钮矩形分开保存。收起不会覆盖用户的键盘尺寸。
顶部或手柄按下时保存鼠标起点和窗口矩形，并捕获鼠标；移动时计算差值。
拖动阶段不执行定时位置恢复。鼠标释放或捕获丢失时保存最终矩形。

定时器每 200 ms 跟踪前台目标及显示器变化，不周期性覆盖用户摆放位置。
显示器工作区发生变化时，按旧窗口在剩余水平/垂直空间中的比例恢复位置，
保留能够容纳的尺寸，最后执行边界限制。此机制不修改屏幕分辨率或目标窗口。

配置为 %LOCALAPPDATA%\Lucky\TouchInputCompactTest\compact.json。
字段为 X/Y/Width/Height、WorkX/WorkY/WorkWidth/WorkHeight 和 Display。
先写临时文件，再替换配置。文件缺失、无法读取或 JSON 损坏时使用默认值。
单实例互斥量避免两个产品实例同时写同一配置。

## 焦点与输入

保留 WS_EX_NOACTIVATE、ShowWithoutActivation 和 WM_MOUSEACTIVATE 的非激活行为。
候选目标排除自身进程、系统界面、已关闭或最小化窗口。
输入前重新检查前台与目标，避免把文字发送到过时目标。
Shift/Ctrl 是一次性状态；通过 Native.Key 成组发送按下/抬起事件，不全局按住修饰键。
系统输入法负责组合与候选，程序不保存输入文字。

不调用原版 ForceTargetWindow，不缩放、移动或恢复目标应用窗口。
紧凑版不申请原版的 Windows 键盘互斥租约；系统键盘的显隐和遮挡需要单独验证。
Windows 的完整性级别限制仍然适用。测试通过不意味着支持管理员输入框。

## 独立身份

| 项目 | 值 |
| --- | --- |
| EXE | LuckyTouchInput.CompactTest.exe |
| 窗口标题 | Lucky Touch Input · Compact Test |
| 互斥量 | Local\Lucky.TouchInput.CompactTest.SingleInstance |
| 配置目录 | %LOCALAPPDATA%\Lucky\TouchInputCompactTest |

不建议同时展开原版和紧凑版。
将来合入原版布局菜单前，需要重新评估原版窗口避让、系统键盘策略、设置迁移和组合状态。

## 风险与后续

- 缩得过小会降低命中率。最小尺寸和像素检查不能替代手机测试。
- 中文候选可能离键盘较远；候选栏迁移是独立需求。
- 分辨率变化的数学检查已覆盖，但不同 DPI/显示器迁移需要实机验证。
- 三击唤起应按鼠标下窗口的所属进程排除键盘。不能按活动窗口排除，因为键盘不激活。
- 当前布局文件包含界面、状态和几何。后续增加第二种布局时，再按实际变化拆分模块。
