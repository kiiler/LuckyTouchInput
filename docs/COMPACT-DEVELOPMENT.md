# 紧凑悬浮键盘：开发与发布

## 环境与构建

使用 Windows 11 x64 和 .NET 10 SDK。仓库的 global.json 允许同主版本的较新 feature band。
无需管理员权限。不把 SDK、用户配置、日志、录屏或聊天截图提交到 Git。

```powershell
dotnet build LuckyTouchInput.sln -c Release
dotnet run --project tests/LuckyTouchInput.Tests -c Release --no-build
dotnet run --project tests/LuckyTouchInput.CompactTests -c Release --no-build
```

默认紧凑检查不显示窗口、不注入按键、不创建托盘，也不改用户配置。
输出写到 artifacts/compact-tests 下的新目录；也可使用 --output 指定空目录。
几何检查基于控制布局，不等同于实际远控点击验证。

```powershell
dotnet run --project src/LuckyTouchInput.CompactTest -c Release
pwsh -File scripts/Publish.ps1 -Variant Compact
```

原版仍使用 scripts/Publish.ps1 的默认 Standard 变体。
两个 ZIP 名称和发布目录不同。每个 ZIP 包含对应 EXE、文档、MIT 许可证和 .NET 运行时声明。
CI 构建整个解决方案，执行两套快速检查，并分别打包。CI 不自动执行鼠标注入测试。

## 交互检查

在独立测试桌面或可抛弃的 Windows 会话中执行以下命令。
关闭其他 Lucky 实例和全局鼠标手势脚本，避免覆盖或拦截测试点击。
测试会移动鼠标、切换前台，并向自己创建的独立文本框发送固定测试字符串。
不要在正在编辑重要内容的桌面运行；完整原版 UI 验收仍遵循 CONTRIBUTING.md 的隔离要求。

```powershell
dotnet run --project tests/LuckyTouchInput.CompactTests -c Release -- --interactive --output artifacts/compact-interactive-run1
```

输出目录必须为空，防止把旧的窗口句柄或文本结果误当成本次结果。
探针仅用于测试，主动禁用它自身文本框的 IME，以确定英文测试的预期结果。
这不改变用户其他应用的输入法，也不验证中文候选。
探针生成的固定测试文本只保存在被忽略的测试输出目录。
退出时恢复鼠标位置和此前前台，关闭且必要时终止本次启动的探针进程。
交互检查失败时保留 results.txt，检查前台、权限级别和输入法，不能只靠 SendInput 返回值判断成功。

## 手机验收记录

记录主机工作区、DPI、远控软件和版本、手机显示缩放、初始键盘尺寸。
不要把设备名称、私人聊天内容或输入记录放进公共报告。
检查以下项目：

1. 同一输入框连续输入，观察焦点和键盘是否稳定。
2. 拖动、缩放、换边后继续输入。
3. 收起展开、关闭重开，核对位置与尺寸。
4. 输入中文并实际选词；确认候选栏可见。
5. 修改远控分辨率，确认键盘留在屏幕内。
6. 分别记录舒适度、误触、遮挡和长时间使用结果。

## 三击唤起示例

scripts/Compact-TripleTap.example.ahk 是 AutoHotkey v1 的可选示例。
它不包含某位用户的其他快捷键，也不会修改现有脚本或设置开机启动。
先填写自己的程序路径，再人工合入现有脚本；不要同时注册两个 ~LButton Up 热键。
前三次点击仍会传给目标应用。键盘区域内点击会重置计数，不触发重新打开。

## 维护与回退

保留测试版独立身份，避免写原版配置或接管原版互斥量。
发布前构建两种变体，并运行包检查。变更输入逻辑后需要重跑交互验收。
关闭紧凑版，再运行原版即可回退。删除紧凑目录即可移除便携程序；可选删除独立配置目录。
不要在不了解来源的情况下清除任何原版 native-keyboard-lease.ini。
