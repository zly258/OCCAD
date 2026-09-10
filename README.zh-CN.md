# OCCAD

OCCAD 是基于 **OcctCSharpBridge / Open CASCADE Technology (OCCT)** 构建的 Avalonia 桌面 CAD 应用与可扩展 CAD 内核。

[English](README.md)

## 项目定位

当前目标是形成可继续扩展的 CAD 初版基线，重点保证：

`Create → Preview → Exact Input → Commit → Property → Grip → Undo/Redo`

核心组成包括 Document / Entity / Layer、Tool / Action / Transaction / History、Selection / Preselection / Subobject Selection、WorkPlane / Snap / Tracking / Precision Input、Grip / Preview / Transient Scene，以及二维、三维、Feature、模型树、图层、属性、本地化和持久化。

## 技术基线

- .NET 10
- Avalonia 12 原生 `FluentTheme`
- OcctCSharpBridge SDK 3.0 / ABI 5
- Open CASCADE Technology 7.9.0
- Windows x64 / Linux x64

## UI 基线

OCCAD 只保留一套经典 CAD Shell：

```text
Menu
├─ 文件
├─ 绘图
├─ 建模
├─ 视图
├─ 窗口
└─ 语言

Toolbar Row 1
撤销 重做 | 当前图层 | 上 前 右 轴测 充满 | 线框 着色

Toolbar Row 2
直线 多段线 矩形 圆 圆弧 正多边形 | 长方体 圆柱体 球体 拉伸 旋转

Model Tree | CAD Viewport | Layers / Properties

Fixed Tool Parameter Strip
Operation Prompt | XY YZ XZ | SNAP ORTHO POLAR
```

UI 规则：

- 不使用 Ribbon；
- Toolbar 只保留高频命令，最多两行，不把全部命令平铺出来；
- 文件、语言和完整命令集合放在 Menu；
- Button / TextBox / ComboBox / CheckBox / Menu 使用 Avalonia 原生 Fluent；
- `CadTheme` 只保留布局尺寸、Viewport/Overlay 和 CAD 必需视觉；
- 自定义 ColorTable 保留；
- Tool 参数条固定在底部状态栏上方，不因 Tool 切换改变 Viewport 高度；
- Viewport 深色，左下角 Triedron 保留，ViewCube 默认关闭；
- 底部不显示永久 Command Input、Ready、版本字符串或永久坐标噪声。

## Tool 参数输入

带参数的 Tool 通过统一 `ParameterPanel` 在底部固定参数条显示真实参数。

例如正多边形：

```text
正多边形： 边数 [6]  方式 [内接]
```

- `Sides`：3~360；
- `Mode`：Inscribed / Circumscribed；
- 指定圆心前可直接键入边数并回车；
- 参数输入会写回当前 Tool；Preview 可反向刷新可观测的实际尺寸值；
- 用户正在编辑的输入框不会被实时刷新覆盖。

Box、Cylinder、Cone、Frustum、Sphere、Ellipsoid、Torus、Helix、Ellipse、Extrude 等已有参数 Tool 复用同一机制。

## PropertyGrid

PropertyGrid 由 Core Descriptor 驱动：

- Property / Value 左对齐；
- Numeric 左对齐；
- Point / Vector / Normal 使用三行工程输入：

```text
X  [ ... ]
Y  [ ... ]
Z  [ ... ]
```

- Layer 使用下拉；
- Color / LineStyle / LineWidth 支持 ByLayer；
- Color 继续使用 OCCAD 自定义 ColorTable；
- Circle / Arc / Ellipse / Rectangle / Regular Polygon 的 `Normal` 可编辑。

## 交互基线

- `Esc`：取消当前 Tool，并清空 Entity / Subobject / Preselection；
- `Backspace`：回退当前 Tool 阶段；
- `Enter/Space`：提交当前有效输入或完成 Tool；
- XY / YZ / XZ：切换绘图工作平面；如果当前阶段固定临时 ToolPlane，会先安全回退到允许切换的阶段；
- `Ctrl+Z`：撤销；
- `Ctrl+Y` / `Ctrl+Shift+Z`：重做；
- Preview/Snap/Tracking/Grip 都属于 transient，Commit/Cancel 后必须清理。

## 初版功能

二维：Point、Line、Polyline、Free Polygon、Regular Polygon、Rectangle、Circle、Arc、Ellipse、Spline。

Circle：Center+Radius、Center+Diameter、Two Points、Three Points、Point+Center。

Arc：Three Points、Center→Start→End、Start→Center→End、Start→End→Center、Start→End→Point、Start→End→Tangent。

Ellipse：Center+Axes、Axis Endpoints+Minor Axis。

三维/曲线：Box、Cylinder、Cone、Frustum、Sphere、Ellipsoid、Torus、Helix。

Feature：Extrude、Revolve、Sweep、Loft。

视图：Top / Bottom / Front / Back / Left / Right、Iso NE/NW/SE/SW、充满（`view.fit`）、Wireframe、Shaded。

## 架构

```text
OCCAD.Avalonia
      ↓
OCCAD.Core
      ↓
OcctNet / OcctCSharpBridge
      ↓
OCCT
```

Core 不引用 Avalonia。UI 负责输入适配和状态呈现，不复制 Document、Selection、History、Layer、Geometry 或 Tool 业务状态。

## 构建

Windows：

```powershell
.\build.ps1
```

Linux：

```bash
./build.sh
```

如 Bridge SDK 未提供完整 portable runtime，需要设置 `OCCT_ROOT` / `CASROOT` 或按运行脚本提供 OCCT 路径。

当前项目不维护独立 Test 项目。验证顺序为：

`Core build → Avalonia build → 实机交互回归 → Native/Transient 清理检查`

不能仅因为源码存在或 Action 已注册就视为功能完成。

## 文档

从 [docs/README.md](docs/README.md) 开始。重点文档：

- `02-UI-SPEC.md`
- `03-INTERACTION-SPEC.md`
- `04-ARCHITECTURE.md`
- `05-ENTITY-TOOL-CONTRACT.md`
- `10-TRANSACTION-RESOURCE-CONTRACT.md`
- `11-BUILD-VALIDATION.md`
- `13-USER-GUIDE.md`
- `14-FEATURE-MATRIX.md`

## License

OCCAD 原创代码采用仓库根目录的 **OCCAD Non-Commercial License 1.0**。第三方依赖继续遵循各自许可证，详见 `THIRD_PARTY_NOTICES.md`。
