# OCCAD

OCCAD 是基于 **OcctCSharpBridge / OCCT** 的 Avalonia 桌面 CAD 应用与 CAD Core。

[English](README.md)

## 当前目标

当前分支优先做一个**可用、可验证、职责清晰**的 CAD 基线，不继续堆叠演示型 UI。

`OCCTBIM-Source/release-1.0` 用作 Document / Entity / Layer / Property / Tool / Grip / Snap / WorkPlane / Viewport 交互语义参考；OCCAD 保留 C# / .NET / Avalonia / OcctCSharpBridge 技术架构，不复制 Qt 控件、单例和历史实现。

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

`CadApplicationCore` 是桌面应用组合根。Avalonia 不再自行创建第二个 Workspace，也不保存一套平行 CAD 状态。

## 极简 UI

桌面界面长期只保留六个区域：

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

- Ribbon：纯文字、低高度，只显示真实注册的 Core Action。
- Model：浏览和选择 Document Entity。
- Viewport：唯一主要场景；深色背景、ViewCube、Triedron、Window/Crossing、Grip、Snap、Preselection。
- Inspector：属性 / 图层共用一个右侧 Tab，不重复占用视口。
- Command Line：命令、坐标、精确值和完整 Tool Prompt 的唯一入口。
- Status：选择、当前层、SNAP / ORTHO / POLAR、工作平面和坐标。

不恢复大型品牌栏、假文档页签、重复 View 工具条、Floating Tool Panel、Dynamic HUD、常驻日志面板和没有 Core 行为支撑的按钮。

## 交互基线

- 中键：平移；`Shift + 中键`：旋转；中键双击：Fit。
- `F3`：Snap；`F8`：Ortho；`F10`：Polar。
- 绘图时 `T / F / S`：XY / XZ / YZ 工作平面。
- 绘图状态使用中心留空 CAD 十字光标，不遮挡捕捉标记。
- 左→右框选为 Window，右→左为 Crossing。
- 空闲状态右键菜单只保留 Source 对应的 ShowAll / Hide / Isolate / Select / Move / Copy / Delete / Property。
- 空闲时直接输入字母进入统一 Command Line；Space 重复上一命令；绘图时数字进入统一精确输入。
- Esc / Backspace / Enter / Space / 右键均进入统一 ToolManager 生命周期，不由 UI 直接修改模型。

## 数据与事务

- Document / Entity geometry 是权威数据；Viewer object 是派生显示。
- Preview、Snap marker、Tracking、Grip drag marker 都属于 Transient Scene，不进入 Document / Selection / History。
- Tool 完成或取消后必须回到 neutral state，清理 Preview、Snap、Tracking、WorkPlane、Preselection 和 pointer transient。
- Grip PointerMove 只编辑 duplicate preview，Accept 时一次写回真实 Entity。
- Property 与 Layer 修改走 Core transaction / history；UI 不直接写 Entity geometry。
- Entity 使用稳定 `LayerId`；Layer 名称只是可编辑显示值。

## 当前功能面

默认 Ribbon 暴露已经稳定并注册的核心能力：

- 新建、Undo / Redo、删除、全选、反选、距离测量；
- Point / Line / Polyline / Circle / Arc / Rectangle / Polygon / RegularPolygon / Ellipse / Spline；
- Box / Cylinder / Cone / Sphere / Ellipsoid / Torus；
- Extrude / Revolve / Sweep / Loft；
- Move / Copy / Rotate / Scale / Mirror / Array / Offset / Trim / Extend / Fillet / Chamfer；
- CenterLine / Text / Length / Angle / Radius / Diameter；
- Fit / Isometric / Top / Front / Right / Wireframe / Shaded / Hide / Isolate / ShowAll。

Core 中存在但未形成稳定产品工作流的能力可以保留，但默认 UI 不暴露占位入口。

## 设置

应用级设置由 `CadSettingsStore` 管理，并在退出时保存到：

`%LOCALAPPDATA%\OCCAD\settings.json`

当前 UI 不提供一套额外“假首选项”窗口。设置只在有明确 Core owner 和真实生效路径时才进入界面。

## 编译与运行

要求 Windows x64、`global.json` 指定的 .NET SDK，以及已安装的 OcctCSharpBridge SDK，默认路径：

`C:\Program Files\OcctCSharpBridge\SDK\3.0\win-x64`

```powershell
cd D:\workspace\occt\OCCAD
git pull
.\build.ps1
.\run.ps1 -OcctRoot D:\tools\occt-vc144-64
```

Bridge SDK 自带 portable runtime 时 `run.ps1` 自动使用；否则传入 `-OcctRoot`，或设置 `OCCT_ROOT` / `CASROOT`。

完整长期规范见 [docs/README.md](docs/README.md)。
