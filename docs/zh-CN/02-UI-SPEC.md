# 02 界面规范

## 1. 最终界面基线

OCCAD 使用原生 WPF 经典 CAD 桌面界面，正式冻结为 `Menu + ToolBar + Viewport + Dock + Command Line + StatusBar`。不使用 Ribbon，不引入第三方主题/控件作为主框架。

```text
┌────────────────────────────────────────────────────────────┐
│ Menu：文件 编辑 绘图 修改 视图 面板 设置                    │
├────────────────────────────────────────────────────────────┤
│ ToolBar：S F T | 当前层 | L/锁 | A/锁 | 完成 | 取消          │
├──────────────┬───────────────────────────────┬──────────────┤
│ Model Dock   │                               │ Layer Dock   │
│              │           Viewport            ├──────────────┤
│              │                               │ Property Dock│
├──────────────┴───────────────────────────────┴──────────────┤
│ Command Output / 当前 Tool Prompt                           │
│ > Command / Tool Input                                      │
├────────────────────────────────────────────────────────────┤
│ StatusBar：Snap | Ortho | Polar | Coordinate | Progress     │
└────────────────────────────────────────────────────────────┘
```

Viewport 始终是最大区域；Command Line 保持紧凑，不允许挤压主要绘图区。

## 2. Menu

顶级菜单固定：文件、编辑、绘图、修改、视图、面板、设置。常用命令直接放顶级菜单下一层，通过 Separator 分类；最多再增加一层真正的命令族。

Menu、键盘快捷键、Command Line 必须使用同一个稳定 Action ID，不允许 MainWindow 再维护第二套命令 switch。

未注册 Action 不得显示为可执行菜单。已经注册的基础 Entity/Tool 必须有一致入口。

## 3. ToolBar 与 ToolPanel

ToolBar 常驻：S/F/T 工作平面、Current Layer、当前阶段 Length/Angle、Finish/Cancel 以及真正全局的精度状态。Radius、Width、Height、Depth、Sides 等 Tool 稳定参数只进入 `CadToolPanel`。

## 4. Command Line

Command Line 位于 StatusBar 上方，包含一行输出/Prompt 和一行输入。它不是独立命令系统，而是 `CadCommandManager → CadActionManager / Active Tool` 的界面。

Idle 状态支持 `LINE/L`、`CIRCLE/C`、`MOVE/M`、`BOX`、`FIT`、`UNDO` 等命令与别名；空输入 Enter 重复最近 repeatable Action。Up/Down 浏览原始命令输入历史，Esc 清空输入或取消 Active Tool。

Active Tool 状态下输入优先交给当前 Tool：支持 Finish/Cancel/StepBack、`L 100`、`A 45`、`F 2` 和 `parameter=value`。坐标输入必须走统一 WorkPlane/precision point contract，不能绕过 Tool 状态机直接修改 Entity；在该 contract 完成前不得用 MainWindow 特例模拟坐标提交。

Prompt 来源必须是 `ActiveTool.Prompt`。Command Line 主显示完整 Prompt；StatusBar 只显示状态、Snap/Tracking、坐标、错误/结果和后台进度。

## 5. WorkPlane / Tracking 显示

WorkPlane 是几何约束，不是常驻绘图对象。默认不绘制工作平面局部 X/Y 两条轴线；左下角 triedron 已提供全局方向参考。

Tracking guide 仅在 ORTHO/POLAR 实际命中跟踪方向时显示。没有 tracking result 时不绘制从工作平面原点到鼠标的额外指引线，避免与 Line/Polyline 等 Tool 的真实 Preview 重叠、加粗或闪烁。

切换 S/F/T 只改变工作平面，不改变相机。需要强调工作平面时应采用短暂、按需的 Presentation，不把显示状态写进 `CadWorkPlane` Core。

## 6. Dock / Property / Layer

Model 默认左侧；Layer 默认右上；Property 默认右下。Dock 是 Workspace/Document/Selection 状态的视图，不保存第二份业务数据。

Layer 编辑采用稳定集合和局部刷新；编辑非当前层属性不得改变 Current Layer。PropertyGrid 单选完整、多选共同属性，ByLayer/custom 状态独立，double 显示 3 位但底层不舍入。

## 7. Viewport 与 DPI

默认黑色背景，左下小型坐标轴，右上 ViewCube。Preview 使用最终 Entity 几何和解析后外观；Snap/Grip/Selection marker 保持屏幕像素稳定。

字体继承系统；布局按 DIP/DPI 工作。125%/150% 缩放下窗口、Dock、Menu、Command Line、ToolPanel 不得超屏、裁切或错位。
