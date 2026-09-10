# OCCAD

OCCAD 是基于 **OcctCSharpBridge / Open CASCADE Technology (OCCT)** 构建的 Avalonia 桌面 CAD 应用与可扩展 CAD 内核。

[English](README.md) · [文档索引](docs/README.md) · [用户指南](docs/zh-CN/13-USER-GUIDE.md) · [功能矩阵](docs/zh-CN/14-FEATURE-MATRIX.md) · [命令参考](docs/zh-CN/15-COMMAND-REFERENCE.md) · [发布指南](docs/zh-CN/16-RELEASE-GUIDE.md)

## 项目定位

OCCAD 初版不追求堆功能数量，而是优先形成完整 CAD 交互闭环：

`Create → Preview → Exact Input → Commit → Property → Grip → Undo/Redo`

核心能力包括 Document / Entity / Layer、Tool / Action / Command / Transaction / History、Selection / Preselection / Subobject Selection、WorkPlane / Snap / Tracking / Precision Input、Grip / Preview / Transient ownership，以及二维绘图、三维基本体、Modeling Feature、模型树、图层、属性、本地化和 OCCAD 自有文档持久化。

## 技术基线

- .NET 10
- Avalonia 12 原生 `FluentTheme`
- OcctCSharpBridge SDK 3.0 / ABI 5
- Open CASCADE Technology 7.9.0
- Windows x64 / Linux x64

## UI 基线

OCCAD 只保留一套紧凑经典 CAD Shell：

```text
Menu：文件 | 绘图 | 建模 | 修改 | 视图 | 窗口 | 语言

Toolbar Row 1
撤销 重做 | 当前图层 | 上 前 右 轴测 充满 | 线框 着色

Toolbar Row 2
直线 多段线 矩形 圆 圆弧 正多边形 | 移动 删除 |
长方体 圆柱体 球体 拉伸 旋转

Model Tree | CAD Viewport | Layers / Properties

Fixed Tool Parameter Strip
Operation Prompt | XY YZ XZ | SNAP ORTHO POLAR
```

设计规则：

- 不使用 Ribbon；
- Toolbar 最多两行，只保留高频命令；
- 完整命令集合保留在 Menu；
- 普通 UI 使用 Avalonia 原生 Fluent；
- CAD ColorTable 作为业务控件保留；
- Tool 参数条固定在状态栏正上方，避免 Viewport 跳动；
- Viewport 深色，左下角 Triedron 保留；
- ViewCube 默认关闭；
- 不显示永久 Command Line、Ready、版本文字或永久坐标噪声。

## 初版功能面

### 二维绘图

- Point
- Line
- Polyline
- Free Polygon
- Regular Polygon
- Rectangle
- Circle
- Arc
- Ellipse
- Spline

Circle：Center+Radius、Center+Diameter、Two Points、Three Points、Point+Center。

Arc：Three Points、Center→Start→End、Start→Center→End、Start→End→Center、Start→End→Point、Start→End→Tangent。

Ellipse：Center+Axes、Axis Endpoints+Minor Axis。

### 三维与曲线

- Box
- Cylinder
- Cone
- Frustum
- Sphere
- Ellipsoid
- Torus
- Helix

### Modeling Feature

- Extrude
- Revolve
- Sweep
- Loft

### 修改

- Move
- Delete

Copy / Rotate / Scale / Mirror 当前只保留 Core transaction-level 能力，待独立交互 Tool 完成并验收后再进入产品面。Array / Offset / Trim / Extend / Fillet / Chamfer / Annotation 不属于当前初版正式功能。

### 交互基础设施

- Entity / Subobject Selection
- Preselection
- Window / Crossing
- SNAP 候选发现与循环
- Grip / Hot Grip / Grip Edit
- XY / YZ / XZ WorkPlane
- ORTHO / POLAR
- 点/长度/角度/比例精确输入
- Preview / Tracking transient ownership
- 原子 Undo / Redo

## 快速开始

### Windows

```powershell
git clone https://github.com/zly258/OCCAD.git
cd OCCAD
.\build.ps1
.\run.ps1
```

flat Bridge SDK + 外部 OCCT runtime：

```powershell
.\run.ps1 -OcctRoot D:\tools\occt-vc144-64
```

### Linux

```bash
git clone https://github.com/zly258/OCCAD.git
cd OCCAD
./build.sh
./run.sh
```

Bridge SDK 路径可通过 `OCCTCSHARPBRIDGE_SDK` 覆盖。

## 常用快捷键

| 快捷键 | 功能 |
|---|---|
| Esc | 取消当前 Tool |
| Backspace | 回退 Tool 阶段 |
| Enter / Space | 确认/结束当前阶段 |
| Ctrl+Z | Undo |
| Ctrl+Y / Ctrl+Shift+Z | Redo |
| F3 | SNAP |
| F8 | ORTHO |
| F10 | POLAR |
| Tab / Shift+Tab | Snap 候选循环 |
| T / S / F | XY / YZ / XZ WorkPlane |
| Shift+中键 | 旋转视图 |
| 中键双击 | Fit All |

详细工作流和精确输入语法见 [用户指南](docs/zh-CN/13-USER-GUIDE.md)。

## Properties 与 Layers

Properties 由 Core Descriptor 驱动，不维护 UI 自己的模型状态。

- Numeric 左对齐；
- Layer 使用 ComboBox；
- Color / LineStyle / LineWidth 支持 ByLayer；
- Color 使用 OCCAD CAD ColorTable；
- Point / Vector / Normal 使用 X/Y/Z 工程输入；
- Circle / Arc / Ellipse / Rectangle / Regular Polygon 的 `Normal` 可编辑；
- 属性修改进入 transaction/history/presentation 同步链。

Layers 支持当前层、可见性、锁定、颜色、线型、线宽、新建、重命名和删除非默认层。Layer `0` 始终受保护。

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

`CadWorkspace` 是一次 CAD 会话的 composition root。Core 对 Document、Selection、Tool 生命周期、WorkPlane、Snap、Grip、Transaction/History 和 Transient ownership 负责；Avalonia 只适配输入和呈现 Core 状态，不维护第二套 CAD 模型。

详见 [架构设计](docs/zh-CN/04-ARCHITECTURE.md) 和 [开发手册](docs/zh-CN/08-DEVELOPMENT-GUIDE.md)。

## 构建与验收

Windows 权威构建入口：

```powershell
.\build.ps1
```

Linux：

```bash
./build.sh
```

当前仓库不维护独立 Test 项目，也不使用 GitHub Actions 作为产品验收路径。发布验收以本地真实 build + 手工/native 交互回归为准。

不能仅因为源码存在或 Action 已注册就认为功能完成。

## 发布包

Windows：

```powershell
.\publish.ps1
```

默认输出：

```text
artifacts\publish\OCCAD
```

发布包带 `run.ps1`。使用 portable Bridge 时，native runtime 与 OCCT resources 一起进入应用目录。

Linux：

```bash
./publish.sh
```

默认输出：

```text
artifacts/publish/OCCAD-linux-x64/
artifacts/publish/OCCAD-linux-x64.tar.gz
```

Linux 发布包带 `run.sh`、portable runtime 和 OCCT resources。

创建正式 Release 前按 [发布与交付指南](docs/zh-CN/16-RELEASE-GUIDE.md) 完整执行 Gate。

## 文档

当前中英文文档按相同编号维护。

推荐入口：

- [详细用户指南](docs/zh-CN/13-USER-GUIDE.md)
- [初版功能矩阵](docs/zh-CN/14-FEATURE-MATRIX.md)
- [命令与功能参考](docs/zh-CN/15-COMMAND-REFERENCE.md)
- [架构设计](docs/zh-CN/04-ARCHITECTURE.md)
- [开发手册](docs/zh-CN/08-DEVELOPMENT-GUIDE.md)
- [构建与验收](docs/zh-CN/11-BUILD-VALIDATION.md)
- [发布与交付指南](docs/zh-CN/16-RELEASE-GUIDE.md)

完整索引见 [docs/README.md](docs/README.md)。

## 发布准备状态

仓库当前已经按初版发布要求完成文档体系和发布脚本收口。正式标记 Release Ready 之前，仍必须完成发布指南规定的真实 Windows build、应用启动、核心交互回归、Save/Open、本地化/DPI 和干净目录发布包启动验证。

## License

OCCAD 原创代码采用仓库根目录的 **OCCAD Non-Commercial License 1.0**。第三方依赖继续遵循各自许可证，详见 `THIRD_PARTY_NOTICES.md`。
