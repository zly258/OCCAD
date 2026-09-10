# OCCAD

OCCAD 是基于 **OcctCSharpBridge / OCCT** 的 Avalonia 桌面 CAD 应用与 CAD Core。

[English](README.md)

## 当前目标

当前分支只做一个**可用、可验证、职责清晰**的 CAD 基线：优先保证 Document / Entity / Layer / Property / Tool / Selection / Snap / Grip / Precision / WorkPlane / History 的正确性，不继续堆叠演示型功能。

`OCCTBIM-Source/release-1.0` 作为 CAD 行为和职责边界参考；OCCAD 保留 C# / .NET / Avalonia / OcctCSharpBridge 技术架构，不复制 Qt 控件、单例和历史实现。

## 架构

```text
Avalonia Shell
    ↓
CadApplicationCore
    ├─ CadSettingsStore
    └─ CadWorkspace
         ├─ Document / Entity / Layer
         ├─ Action / Command / Tool
         ├─ Selection / Preselection / Subobject
         ├─ Snap / Tracking / Precision / WorkPlane
         ├─ Grip / Preview / Transient Scene
         └─ History / Property Transaction
              ↓
        OcctCSharpBridge / OCCT
```

核心约束：

- Entity 保存 CAD 语义、几何、显示属性和稳定 `LayerId`；Viewer object 只是派生显示。
- Tool 只负责交互状态机；Action 是用户操作入口；Command Line、Ribbon 和快捷键最终都委托 Core。
- Preview / Snap marker / Tracking / Grip drag marker 只属于 Transient Scene，不进入 Document / Selection / History。
- Property 与 Layer 修改统一经过 Core transaction/history，不由 Avalonia 直接写模型。
- Avalonia 不维护第二套 Workspace、Selection、Layer、Tool 或 Property 状态。

## 极简 UI

桌面只保留六个长期区域：

```text
┌──────────────────── Ribbon ────────────────────┐
├──── Model ────┬──────── Viewport ────────┬─────┤
│               │                           │属性/│
│               │                           │图层 │
├───────────────┴───────────────────────────┴─────┤
│ Command Line                                    │
├─────────────────────────────────────────────────┤
│ Status                                          │
└─────────────────────────────────────────────────┘
```

- Ribbon：采用 ModelScript 的紧凑三行 Avalonia Ribbon 思路，纯文字、低高度，只呈现真实 Core Action。
- Model：浏览和选择 Document Entity。
- Viewport：唯一主要场景；深色背景、Triedron、Window/Crossing、Grip、Snap、Preselection；**不显示 ViewCube**。
- Inspector：属性 / 图层共用右侧 Tab，不复制模型状态。
- Command Line：命令、坐标、精确值和 Tool Prompt 的统一入口。
- Status：选择、当前层、SNAP / ORTHO / POLAR、工作平面和坐标。

不恢复大型品牌栏、假文档页签、重复 View 工具条、Floating Tool Panel、Dynamic HUD、常驻日志面板以及没有 Core 行为支撑的按钮。

## 当前功能面

当前 executable surface 只保留常用能力。

**二维实体**：Point、Line、Polyline、Rectangle、Circle、Arc、Ellipse、Spline、Polygon、RegularPolygon、CenterLine、CenterMark。

**三维实体**：Box、Cylinder、Cone、Sphere。

**编辑**：Move、Copy、Rotate、Scale、Mirror、Array。

**辅助**：Undo / Redo、Delete、Select All / Invert、Distance Measure、Fit、标准视角、Wireframe / Shaded、Hide / Isolate / ShowAll。

Offset / Trim / Extend / Fillet / Chamfer、实验性 Feature、Annotation、额外 3D Primitive、ImportedShape 和 CAD Exchange 当前均不属于产品面，也不应通过 Registry / Action / Ribbon 暴露。

## 精确绘图

统一精确输入链路：

```text
Viewport / Command Line
    ↓
CadCommandManager
    ↓
Coordinate / Precision parser
    ↓
Effective WorkPlane
    ↓
Snap + Tracking + Ortho/Polar
    ↓
Tool state machine
    ↓
Preview
    ↓
Transaction / History
    ↓
Entity
```

支持绝对坐标、相对坐标、极坐标以及 Tool 长度/角度/因子输入。绘图输入必须使用当前 Effective WorkPlane，而不是偷偷回到世界 XY。

## 交互基线

- 中键：平移；`Shift + 中键`：旋转；中键双击：Fit。
- `F3`：Snap；`F8`：Ortho；`F10`：Polar。
- 绘图时 `T / F / S`：XY / XZ / YZ 工作平面。
- 绘图状态使用中心留空 CAD 十字光标，不遮挡捕捉标记。
- 左→右框选为 Window，右→左为 Crossing。
- 空闲状态右键菜单只保留 ShowAll / Hide / Isolate / Move / Copy / Delete / Property。
- Esc / Backspace / Enter / Space / 右键统一进入 ToolManager 生命周期。

## 编译与运行

要求 Windows x64、`global.json` 指定的 .NET SDK，以及已安装的 OcctCSharpBridge SDK，默认路径：

`C:\Program Files\OcctCSharpBridge\SDK\3.0\win-x64`

```powershell
cd D:\workspace\occt\OCCAD
git pull
.\build.ps1 -Configuration Release
.\run.ps1 -OcctRoot D:\tools\occt-vc144-64
```

Bridge SDK 自带 portable runtime 时 `run.ps1` 自动使用；否则传入 `-OcctRoot`，或设置 `OCCT_ROOT` / `CASROOT`。

完整长期规范见 [docs/README.md](docs/README.md)。
