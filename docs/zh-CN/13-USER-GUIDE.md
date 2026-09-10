# 13 用户指南

本文面向 OCCAD 初版使用者，按“启动 → 绘图 → 编辑 → 属性 → 保存”的真实工作流说明当前已经进入产品面的功能。完整支持边界以 [14-FEATURE-MATRIX.md](14-FEATURE-MATRIX.md) 为准。

## 1. 启动与运行

### Windows

开发构建：

```powershell
.\build.ps1
.\run.ps1
```

如果使用 flat Bridge SDK 且 OCCT runtime 不在标准环境中：

```powershell
.\run.ps1 -OcctRoot D:\tools\occt-vc144-64
```

发布目录中的 `run.ps1` 可以直接启动同目录 OCCAD，不依赖仓库源码目录。

### Linux

```bash
./build.sh
./run.sh
```

Linux 使用 Bridge portable runtime，`runtime/` 与 `occt/` 必须随应用一起存在。

## 2. 主界面

OCCAD 使用经典 CAD Shell，不使用 Ribbon：

```text
Menu
Toolbar Row 1
Toolbar Row 2

Model Tree | CAD Viewport | Layers / Properties

Fixed Tool Parameter Strip
Operation Prompt | XY YZ XZ | SNAP ORTHO POLAR
```

### Toolbar 第一行

```text
撤销 重做 | 当前图层 | 上 前 右 轴测 充满 | 线框 着色
```

### Toolbar 第二行

```text
直线 多段线 矩形 圆 圆弧 正多边形 | 移动 删除 |
长方体 圆柱体 球体 拉伸 旋转
```

Toolbar 只保留高频命令。完整绘图、建模和视图入口放在 Menu 中；文件操作和语言切换也不重复占用 Toolbar。

## 3. 一个标准操作闭环

以“画圆并修改”为例：

1. 选择“圆 → 圆心+半径”。
2. 在 Viewport 指定圆心。
3. 移动鼠标观察 Preview。
4. 单击指定半径，或输入精确半径。
5. Tool 完成，Preview/Snap/Tracking 清理。
6. 单击圆进行选择。
7. 在 Properties 中修改半径、图层、颜色或 Normal。
8. 使用 Grip 进行几何编辑。
9. `Ctrl+Z` 撤销，`Ctrl+Y` 重做。
10. 保存 `.ocad`，重新打开验证正式状态。

OCCAD 的初版功能均以该闭环为目标：

`Create → Preview → Exact Input → Commit → Property → Grip → Undo/Redo`

## 4. Tool 基本操作

Tool 是有阶段的交互命令。底部 Operation Prompt 会提示当前阶段需要的输入。

常用控制：

- `Esc`：取消当前 Tool，并清理正式选择和 Tool transient；
- `Backspace`：回退当前 Tool 的上一阶段；
- `Enter` / `Space`：确认当前阶段，或在允许时结束多点 Tool；
- 右键：执行当前 Tool 的 secondary action，通常等价于确认、结束或取消当前阶段；
- `Tab`：存在多个 Snap 候选时循环候选；
- `Shift+Tab`：反向循环 Snap 候选。

Tool 完成或取消后，Preview、Snap、Tracking、GripDrag 等临时显示不应继续留在 Viewport。

## 5. Tool 参数条

Tool 参数条固定在状态栏正上方，始终保持稳定高度，避免启动/退出 Tool 时 Viewport 跳动。

例如正多边形：

```text
正多边形： 边数 [6]  方式 [内接]
```

参数规则：

- Integer / Double / OptionalDouble → TextBox；
- Boolean → CheckBox；
- Choice → ComboBox；
- String → TextBox；
- 编辑器写回当前 Tool 的 `TrySetParameter()`；
- Preview 的实际尺寸可以反向刷新参数值；
- 正在输入的 TextBox/ComboBox/CheckBox 不会被刷新重建或覆盖。

## 6. 精确输入

OCCAD 没有永久 Command Line，但当 Viewport 聚焦且 Tool 处于输入阶段时，可以直接键入精确值。

常见形式：

```text
100
100,50
100,50,20
@20,0
@50<30
L 100
A 45
ANGLE 45
F 2
Radius=50
```

具体接受的输入由当前 Tool/Stage 决定。

说明：

- `X,Y[,Z]`：坐标输入；
- `@...`：相对坐标；
- 极坐标形式可使用 `<`；
- `L` / `LENGTH`：长度；
- `A` / `ANGLE`：角度；
- `F` / `FACTOR`：比例因子；
- `参数名=值`：直接写 Tool 参数；
- 点输入阶段输入 `O` 可以进入 Offset Point 输入。

非法输入不会建立 Undo 记录，也不应污染正式 Entity。

## 7. 二维绘图

Menu 当前提供：

- Point；
- Line；
- Polyline；
- Free Polygon；
- Regular Polygon；
- Rectangle；
- Circle；
- Arc；
- Ellipse；
- Spline。

### 7.1 Line

依次指定第一点和第二点。移动到第二点前会显示 Preview，可结合 SNAP、ORTHO、POLAR 和精确长度输入。

### 7.2 Polyline

连续指定多个点。达到可结束条件后可使用 Enter/Space/右键结束；Backspace 回退上一段。

### 7.3 Free Polygon

逐点指定任意顶点形成 Polygon。结束前应确认顶点数量满足当前 Tool 要求。

### 7.4 Regular Polygon

流程：

1. 设置 `Sides`，范围 3~360；
2. 设置 `Mode`：Inscribed / Circumscribed；
3. 指定中心；
4. 指定半径。

在指定中心前也可以直接键入边数并回车。

### 7.5 Rectangle

指定第一个角点和对角点。Width/Height 在可观测阶段会同步到参数条。

### 7.6 Circle

当前支持五种方式：

- Center + Radius；
- Center + Diameter；
- Two Points；
- Three Points；
- Point + Center。

Toolbar 的“圆”默认进入 Center + Radius；其他方式从 Draw Menu 进入。

### 7.7 Arc

当前支持：

- Three Points；
- Center → Start → End；
- Start → Center → End；
- Start → End → Center；
- Start → End → Point；
- Start → End → Tangent。

Toolbar 的“圆弧”默认进入 Three Points。

### 7.8 Ellipse

支持：

- Center + Axes；
- Axis Endpoints + Minor Axis。

### 7.9 Spline

连续指定拟合点。达到结束条件后使用 Enter/Space/右键结束。

## 8. 三维基本体和曲线

Menu 当前提供：

- Box；
- Cylinder；
- Cone；
- Frustum；
- Sphere；
- Ellipsoid；
- Torus；
- Helix。

这些 Tool 按阶段指定基点、尺寸、方向或高度，并可在参数条中直接修改可公开参数。

高频 Toolbar 仅保留 Box、Cylinder、Sphere。

## 9. Modeling Feature

当前正式 Tool：

- Extrude；
- Revolve；
- Sweep；
- Loft。

Feature 依赖已存在的 Document Entity/轮廓。选择输入和几何阶段必须按 Operation Prompt 完成；如果所选对象不满足 Feature 前置条件，操作应失败而不污染 Document/History。

## 10. 选择

OCCAD 区分：

- Entity Selection：正式实体选择；
- Subobject Selection：Edge/Face/Vertex 等子对象选择；
- Preselection：鼠标 Hover；
- Window/Crossing：框选。

正式选择支持 Replace / Add / Remove / Toggle 语义。

选择状态是 Core 状态，不由 Viewport highlight 单独决定。Viewer highlight 只是正式 Selection 的派生显示。

## 11. 修改

当前 Classic Shell 正式暴露：

### Move

1. 启动“移动”；
2. 选择一个或多个 Entity；
3. Enter/右键确认选择；
4. 指定基点；
5. 移动鼠标观察 replacement Preview；
6. 指定目标点；
7. 正式 Entity 原子提交；
8. Undo 应一次恢复整次移动。

### Delete

先选择 Entity，再执行删除。删除必须形成可撤销的正式修改。

### 当前未暴露

Copy / Rotate / Scale / Mirror 虽有 Core transaction-level API，但当前没有完成并验收独立交互 Tool，因此不属于当前 Shell 的正式功能。

## 12. Grip 编辑

选择支持 Grip 的 Entity 后会显示 Grip marker。

当前视觉规则：

- 普通 Grip：填充矩形；
- Hot/移动状态：圆形；
- Drag marker：圆形。

Grip 操作原则：

1. 点击 Grip；
2. 进入 GripEditTool；
3. 鼠标移动只修改 duplicate/preview；
4. 最终确认后才修改正式 Entity；
5. 无效输入不得污染正式 Entity；
6. Commit/Cancel 后 GripDrag transient 必须清理。

Grip 大小与命中容差可在 Settings 调整。

## 13. SNAP

SNAP 用于从模型几何中解析精确候选点。

当前基础设施支持：

- Endpoint；
- Midpoint；
- Center；
- Vertex；
- Quadrant；
- Nearest；
- Intersection；
- Perpendicular；
- Tangent；
- Apparent Intersection；
- Extension；
- Insertion；
- Node。

快捷键：

- `F3`：开关 SNAP；
- `Tab`：下一候选；
- `Shift+Tab`：上一候选。

Snap marker 是 transient，不进入保存文件和 History。

## 14. ORTHO / POLAR

- `F8`：开关 ORTHO；
- `F10`：开关 POLAR。

ORTHO 用于正交方向约束；POLAR 使用当前极轴增量进行方向跟踪。切换后当前 Tracking transient 会重新计算。

## 15. 工作平面

底部 XY / YZ / XZ 是真实绘图工作平面，而不是只改变图标状态。

快捷键：

- `T`：XY；
- `S`：YZ；
- `F`：XZ。

如果当前 Tool 阶段允许切换，系统立即改变 WorkPlane 并刷新 Preview/Snap/Tracking；如果当前阶段由 Tool 固定临时平面，Core 会先尝试回退到安全阶段。已确认的旧阶段点不会被强行解释到新平面。

## 16. Viewport 导航和视图

固定视图：

- Top / Bottom；
- Front / Back；
- Left / Right；
- Iso NE / NW / SE / SW；
- Fit；
- Wireframe；
- Shaded。

常用行为：

- 中键导航由 Viewport 处理；
- `Shift + 中键`：旋转；
- 中键双击：Fit All；
- ViewCube 默认关闭；
- 左下角 Triedron 保留。

## 17. Properties

选择实体后右侧 Properties 显示 Core Descriptor 属性。

编辑器规则：

- Numeric：左对齐 TextBox；
- Layer：ComboBox；
- Boolean：CheckBox；
- Enum/Choice：ComboBox；
- Color：OCCAD CAD ColorTable；
- Measurement：只读；
- Point / Vector / Normal：X/Y/Z 三行输入。

平面 Entity 的可编辑 `Normal` 当前覆盖 Circle / Arc / Ellipse / Rectangle / Regular Polygon。

Property 修改必须进入统一 transaction/history/presentation 链，不能只改界面显示。

## 18. Layers

Layers 面板支持：

- 过滤；
- 新建；
- 重命名；
- 删除非默认层；
- 设置当前层；
- Visible；
- Locked；
- Color；
- LineStyle；
- LineWidth。

规则：

- 默认 Layer `0` 不能删除或重命名；
- 顶部 Current Layer ComboBox 与 Layers 面板必须保持一致；
- ByLayer Entity 由 Layer 的颜色/线型/线宽解析最终显示；
- Layer 修改与 Entity presentation 同步由 Core/Document 负责。

## 19. Undo / Redo

- `Ctrl+Z`：Undo；
- `Ctrl+Y` / `Ctrl+Shift+Z`：Redo；
- Toolbar 也提供独立 Undo/Redo。

History 入口统一经过 `CadWorkspace.Undo()` / `CadWorkspace.Redo()`，因此会在历史切换前清理当前 Tool、Selection、Subobject、Preselection 和 Grip 状态。

一次正常用户操作应只形成一个 Undo 单元。

## 20. Settings

Settings 当前可配置的交互/场景项包括：

- Scene Background；
- Display Deviation Coefficient；
- Display Deviation Angle；
- Grip Size；
- Grip Tolerance；
- Snap Marker Size；
- Snap Marker Color；
- Snap Tolerance；
- Selection Tolerance；
- Zoom Sensitivity；
- Language；
- Snap Modes；
- ORTHO / POLAR；
- WorkPlane preset。

设置界面应在 125% / 150% DPI 下保持可读，不依赖固定中文字符串宽度。

## 21. 保存与打开

OCCAD 自有文档保存正式状态：

- Document Entity；
- Geometry / Placement；
- Layer；
- Appearance；
- Feature persistence 数据。

不会保存：

- Preview；
- Snap marker；
- Grip marker；
- Tracking guide；
- Selection Window；
- Preselection 等 transient。

建议发布前至少执行一次：

```text
Create mixed 2D/3D
→ change Layer/Properties
→ Save
→ New/close
→ Open
→ compare geometry/properties/presentation
```

## 22. 语言

当前支持中文 / English 显式切换。Action/Tool/Entity 使用稳定 ID，本地化文字只负责显示，不参与业务识别。

切换语言时 Menu、Toolbar、Properties、Tool 参数条、Prompt 和 Dialog 应同步刷新。

## 23. 常用快捷键

| 快捷键 | 功能 |
|---|---|
| Esc | 取消当前 Tool / 清理当前交互 |
| Backspace | Tool StepBack |
| Enter / Space | 确认阶段或结束 |
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Ctrl+Shift+Z | Redo |
| F3 | SNAP |
| F8 | ORTHO |
| F10 | POLAR |
| Tab / Shift+Tab | Snap 候选循环 |
| T | XY WorkPlane |
| S | YZ WorkPlane |
| F | XZ WorkPlane |
| Shift+中键 | 旋转 |
| 中键双击 | Fit All |

## 24. 当前产品边界

当前初版不把以下能力声明为完成产品功能：

- Copy / Rotate / Scale / Mirror 的交互 Tool；
- Array；
- Offset；
- Trim / Extend；
- Fillet / Chamfer；
- Text / Dimension / Annotation；
- 未经逐方向验收的外部格式 Import/Export。

源码中存在几何 helper、Entity 或 transaction API，不等于当前 UI 正式支持。

## 25. 故障判断

### Preview 残留

先按 Esc。如果仍存在可见对象，记录：

- 哪个 Tool；
- 哪个 Stage；
- Commit 还是 Cancel；
- 是否同时残留 Snap/Grip/Tracking；
- 日志中的 native cleanup 信息。

### Tool 无法结束

确认当前 Operation Prompt 是否仍要求点/选择/参数输入；尝试 Enter、右键或 Backspace，不要连续重复启动同一个 Tool。

### 图层/属性显示不同步

以正式 Entity/Layer Core 状态为准；如果 Viewport 与 Properties 不一致，应视为 presentation 同步缺陷，而不是手工改第二套 UI 状态。

### 启动失败

Windows 使用 `run.ps1`；Linux 使用 `run.sh`。优先确认 Bridge SDK、portable runtime、OCCT resources 和日志，而不是用管理员/root 身份运行应用。

## 26. 功能完成判定

一个功能只有完整跑通以下链路才算产品完成：

`入口 → 参数 → Prompt → Preview → Exact Input → Commit → Property → Grip/Snap → Undo/Redo → Save/Open`

并且 Commit/Cancel 后必须恢复 neutral，不残留 Preview 或 Native transient。
