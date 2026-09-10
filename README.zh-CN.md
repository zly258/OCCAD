# OCCAD

OCCAD 是基于 **OcctCSharpBridge / Open CASCADE Technology (OCCT)** 构建的 Avalonia 桌面 CAD 应用与可扩展 CAD 内核。

[English](README.md)

## 项目定位

OCCAD 不是 OCCT API Demo，也不是单纯的模型查看器。当前目标是形成一个可继续扩展的 CAD 初版基线，重点保证：

`Create → Preview → Exact Input → Commit → Property → Grip → Undo/Redo`

这条核心链路具有明确的状态、事务、历史和 Native 资源生命周期。

核心组成：

- Document / Entity / Layer；
- Tool / Action / Transaction / History；
- Selection / Preselection / Subobject Selection；
- WorkPlane / Snap / Tracking / Precision Input；
- Grip / Preview / Transient Scene；
- 二维绘图、三维基本体和基础建模 Feature；
- Model Tree / Layers / Properties；
- 本地化、持久化、导入导出、构建和发布。

## 技术基线

- .NET 10
- Avalonia 12 原生 `FluentTheme`
- OcctCSharpBridge SDK 3.0 / ABI 5
- Open CASCADE Technology 7.9.0
- Windows x64 / Linux x64

## UI 基线

当前主界面只保留一套 **经典 CAD Shell**：

```text
Menu
├─ 文件
├─ 绘图
├─ 建模
├─ 视图
├─ 窗口
└─ 语言

Single-row Toolbar

Model Tree | CAD Viewport | Layers / Properties

Fixed Tool Parameter Strip
Operation Prompt | XY YZ XZ | SNAP ORTHO POLAR
```

UI 规则：

- 不使用 Ribbon，也不保留旧 CleanShell / Ribbon 兼容层；
- 不使用三行分组工具区；
- Button / TextBox / ComboBox / CheckBox / Menu 等使用 Avalonia 原生 Fluent；
- `CadTheme` 只保留布局指标和 CAD 产品必要的颜色/尺寸，不维护第二套全局控件皮肤；
- Tool 参数条固定停靠在底部状态栏上方，参数出现/消失不会改变 Viewport 高度；
- 设置页标签列按内容共享自适应，Value 列占据剩余宽度；
- 弹窗操作按钮文字水平、垂直居中；
- 自定义 CAD ColorTable 保留；
- Viewport 保持深色，左下角 Triedron 保留，ViewCube 默认关闭；
- 底部不显示永久 Command Input、`Ready`、版本字符串或永久坐标噪声；
- 当前操作提示保留在状态栏。

## Tool 参数输入与实时联动

Tool 的 `ParameterPanel` 自动呈现在**底部固定参数条**。参数条属于 Tool 状态的 UI 投影，不保存第二份业务状态。

例如正多边形：

```text
正多边形： 边数 [6]  方式 [内接]
```

- `Sides`：3~360；
- `Mode`：Inscribed / Circumscribed；
- 在指定圆心之前，也可以直接键入边数并回车。

Box、Cylinder、Cone、Frustum、Sphere、Ellipsoid、Torus、Helix、Ellipse、Extrude 等已有 Tool 参数同样使用统一参数条。

参数编辑器与当前 Tool 双向联动：用户输入提交到 Tool；常用尺寸型 Preview 会反向刷新参数条中的实际值，例如 Circle 的 Radius/Diameter、Rectangle 的 Width/Height、Ellipse 的 Major/Minor Radius、Box 的 Length/Width/Height、Cylinder 的 Radius/Height。用户正在编辑输入框时不会被实时刷新覆盖。

## ESC 与工作平面

`Esc` 是统一 CAD 取消动作：

`取消当前 Tool → 清空 Entity/Subobject/Preselection → 清理 Snap/Tracking → 返回 Viewport`

因此无论焦点在 Viewport 还是参数编辑器，`Esc` 都不应留下活动命令或旧选择。

XY / YZ / XZ 是当前绘图工作平面。绘图过程中请求切换时：

- 如果当前阶段允许切换，立即应用新工作平面并刷新 Preview；
- 如果当前 Tool 正处于固定临时施工平面阶段，Core 会先回退到最近的可切换阶段，再应用新平面；
- User Plane Lock 或固定 Grip Plane 仍会阻止切换；
- Circle、Arc、Rectangle、Ellipse、Regular Polygon 等缓存平面基向量的 Tool 必须在切换后同步刷新缓存，不能出现 UI 已切换而几何仍沿用旧平面的状态。

## PropertyGrid

PropertyGrid 由 Core Descriptor 驱动，不维护第二套属性模型。

- Property 和 Value 均左对齐；
- Numeric 不右对齐；
- Point / Vector / Normal 使用三行工程输入：

```text
X  [ ... ]
Y  [ ... ]
Z  [ ... ]
```

- Circle / Arc / Ellipse / Rectangle / Regular Polygon 的 `Normal` 可编辑；
- Layer 使用下拉选择；
- Color / LineStyle / LineWidth 支持 ByLayer；
- 颜色编辑继续使用 OCCAD 自定义 ColorTable。

## 初版功能

二维：Point、Line、Polyline、Free Polygon、Regular Polygon、Rectangle、Circle、Arc、Ellipse、Spline。

Circle：Center+Radius、Center+Diameter、Two Points、Three Points、Point+Center。

Arc：Three Points、Center→Start→End、Start→Center→End、Start→End→Center、Start→End→Point、Start→End→Tangent。

Ellipse：Center+Axes、Axis Endpoints+Minor Axis。

三维/曲线：Box、Cylinder、Cone、Frustum、Sphere、Ellipsoid、Torus、Helix。

Feature：Extrude、Revolve、Sweep、Loft。

视图：Top / Bottom / Front / Back / Left / Right、Iso NE/NW/SE/SW、Fit、Wireframe、Shaded。

交互基础：Window/Crossing Selection、Subobject Selection、Preselection、Snap、Grip、XY/YZ/XZ WorkPlane、ORTHO、POLAR、Preview、Tracking、Precision Input。

完整状态见 [初版功能矩阵](docs/zh-CN/14-FEATURE-MATRIX.md)。

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

Core 不引用 Avalonia。UI 只负责输入适配和状态呈现，不复制 Document、Selection、History、Layer、Geometry 或 Tool 业务状态。工作平面安全回退、Tool 生命周期、事务和资源所有权属于 Core，不应在不同 UI 入口各自实现一套。

## 目录

```text
OCCAD/
├─ src/
│  ├─ OCCAD.Core/
│  └─ OCCAD.Avalonia/
├─ docs/
│  ├─ zh-CN/
│  ├─ en-US/
│  └─ adr/
├─ build.ps1 / build.sh
├─ run.ps1 / run.sh
├─ publish.ps1 / publish.sh
└─ OCCAD.sln
```

## 构建与验证

Windows：

```powershell
.\build.ps1
```

Linux：

```bash
./build.sh
```

如 Bridge SDK 未提供完整 portable runtime，需要设置 `OCCT_ROOT` / `CASROOT` 或按运行脚本参数提供 OCCT 路径。

当前项目不维护独立 Test 项目。验证顺序为：

`静态检查 → Core build → Avalonia build → 实机交互回归 → Native/Transient 清理检查`

重点手工验证：

- 参数 Tool 启动/取消时 Viewport 高度不跳动；
- 参数输入与 Preview 实际值联动，编辑过程中不被刷新覆盖；
- XY/YZ/XZ 在二维 Tool 和分阶段三维 Tool 中行为一致；
- `Esc` 同时取消命令并清空所有选择；
- 设置页中英文长标签不截断，弹窗按钮文字居中；
- 125% / 150% DPI 下布局可用；
- Tool 完成/取消后 Preview、Snap、Tracking 等 transient 无残留。

不能仅因为源码存在或 Action 已注册就视为功能完成，也不能在没有真实构建输出时宣称构建通过。

## 文档

从 [docs/README.md](docs/README.md) 开始。重点文档：

- `02-UI-SPEC.md`
- `03-INTERACTION-SPEC.md`
- `04-ARCHITECTURE.md`
- `05-ENTITY-TOOL-CONTRACT.md`
- `09-EXTENSION-GUIDE.md`
- `10-TRANSACTION-RESOURCE-CONTRACT.md`
- `11-BUILD-VALIDATION.md`
- `13-USER-GUIDE.md`
- `14-FEATURE-MATRIX.md`

## License

OCCAD 原创代码采用仓库根目录的 **OCCAD Non-Commercial License 1.0**：非商业用途可按条款使用；商业产品、商业工程交付、收费服务以及营利组织生产/设计/工程流程中的使用需要单独书面授权。

OCCT、OcctCSharpBridge、Avalonia、.NET 等第三方依赖仍遵循各自许可证，详见 `THIRD_PARTY_NOTICES.md`。
