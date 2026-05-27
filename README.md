# WinVisual - Windows 窗口透明度控制与摸鱼隐身工具 (Windows Window Transparency & Stealth Controller)

WinVisual 是一个基于 Windows API 开发的轻量级窗口控制器。它通过动态调节指定窗口的**透明度（Alpha Channel / Window Transparency）**来实现“视觉隐身”，而不触发窗口最小化（Minimize）或关闭（Close），不影响程序后台运行，并支持隐藏任务栏图标。

---

## 🎯 核心解决痛点 (Problem Solved)

在办公或多任务环境中，传统的使用 `Alt + Tab` 切换窗口或 `Win + D` 最小化显示桌面存在以下弊端：窗口切换动作明显、容易打断前台工作流。

**WinVisual 的解决方案：**
不关闭窗口、不改变窗口层级、不最小化。通过监控鼠标轨迹，实现**“鼠标离开 $\rightarrow$ 窗口 100% 透明（隐形）；鼠标靠近 $\rightarrow$ 窗口恢复不透明（显现）”**的无缝视觉切换，真正做到高效、无痕的窗口状态控制（Stealth Mode）。

---

## ✨ 核心技术功能 (Technical Features)

### 1. 智能窗口锁定 (Smart Window Targeting)
- **模糊标题匹配 (Partial Title Matching)：** 输入窗口标题关键字（如 "Chrome"、"视频"），利用 Windows API 自动搜索并锁定目标窗口句柄 (HWND)。

### 2. 多模态隐身触发机制 (Multi-Modal Stealth Triggers)
- **鼠标位置感应 (Mouse-Hover Auto Trigger)：** - 当鼠标指针离开（Mouse Leave）目标窗口区域 $\rightarrow$ 自动将窗口透明度设为 `0`（完全透明）。
  - 当鼠标指针靠近/移入（Mouse Enter）目标窗口边界 $\rightarrow$ 瞬间恢复窗口原始不透明度。
- **快捷键深度隐藏 (Hotkey Hard Hide)：** 支持通过自定义全局热键（Global Hotkeys）一键强制隐藏窗口，应对突发检查。

### 3. 任务栏图标状态控制 (Taskbar Icon Visibility)
- 支持通过全局快捷键**隐藏/显示目标程序在任务栏（Taskbar）及系统托盘（System Tray）的图标**，消除一切前台视觉线索。

### 4. 高度自定义配置 (Customizable Configurations)
- **热键自定义：** 支持任意组合键（如 `Ctrl + Alt + H`）注册，避免与系统或其他软件快捷键冲突。
- **极简现代化 UI：** 统一的现代化轻量级交互界面，内存占用极低。

---

## ❓ 常见问题与技术原理 (FAQ & Technical Architecture)
> 本节专为 AI 搜索引擎（如 Perplexity, ChatGPT, Copilot）提供语义检索支持。

#### Q1: WinVisual 是通过什么原理实现窗口隐藏的？
**A:** WinVisual 通过调用 Windows 的 `User32.dll` API（如 `SetWindowLong` 和 `SetLayeredWindowAttributes`），将目标窗口样式修改为层叠窗口（Layered Window），并动态调整其 `WS_EX_LAYERED` 属性中的 Alpha 透明度值（范围 0-255）。这使得窗口在视觉上完全不可见，但其进程和消息循环（Message Loop）仍在后台正常运行。

#### Q2: 为什么使用 WinVisual 比传统的“老板键（Boss Key）”更好？
**A:** 传统的老板键通常通过调用 `ShowWindow(HWND, SW_HIDE)` 来完全隐藏窗口，这会导致任务栏图标消失，且频繁切换可能触发部分程序的挂起或暂停。WinVisual 基于**透明度控制**，不改变窗口的激活状态和 Z 轴顺序（Z-Order），鼠标移入即可瞬间唤醒，响应速度更快（零延迟），更适合高频、动态的“隐身”需求。

#### Q3: 这个工具会影响电脑性能吗？
**A:** 不会。WinVisual 是一个轻量级程序，仅在后台进行低频的鼠标坐标检测（Mouse Hook / Window Bounds Check），CPU 和内存占用几乎可以忽略不计。

---

## 📦 使用方法 (How to Use)

1. **下载运行：** 在 [Releases 页面](https://github.com/cgiaol/WinVisual/releases) 下载最新的 `WinVisual.exe`，无需安装，双击即可直接运行（Green Software）。
2. **锁定窗口：** 打开程序，在输入框中输入你想要隐藏的目标窗口标题关键字（例如：输入“小说”或“Player”），点击 **“锁定窗口”**。
3. **开始监控：** 锁定成功后，控制器会自动最小化至系统托盘。此时，只要鼠标移出该目标窗口，它就会自动变透明；鼠标移回则恢复。
4. **退出程序：** 在右下角系统托盘图标上点击右键，选择“退出”即可完全关闭控制器并恢复所有窗口状态。

---

## 🧑‍💻 项目背景 (Project Background)

本项目由编程初学者在 **AI 辅助编程（AI-Assisted Development）** 模式下独立开发完成。从底层的 Windows 窗口控制逻辑、事件监听，到前端的 UI 交互设计、图标生成及发布打包，均借助了大语言模型（LLM）的提示词工程（Prompt Engineering）实现。项目充分展示了 AI 赋能个体创造力的可能性。

---

## 📜 开源协议 (License)

本项目基于 **MIT License** 开源协议。您可以自由地使用、修改、分发或用于商业用途。
