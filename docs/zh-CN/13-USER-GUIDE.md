# 13 用户指南

## 1. 适用范围

本文说明 OCCAD 初版桌面应用的构建、界面、绘图、参数输入、属性、图层、捕捉、夹点、视图和保存流程。

完整支持状态以 [14 初版功能矩阵](14-FEATURE-MATRIX.md) 为准。

## 2. 构建与运行

Windows：

```powershell
.\build.ps1
```

Linux：

```bash
./build.sh
```

如 Bridge SDK 未提供完整 portable runtime，需要配置 `OCCT_ROOT` / `CASROOT` 或运行脚本要求的 OCCT 路径。

## 3. 主界面

当前只有一套经典 CAD Shell：

```text
Menu
Single-row Toolbar
Active Tool Parameter Strip

Model Tree | Viewport | Layers / Properties

Operation Prompt | XY YZ XZ | SNAP ORTHO POLAR
```

顶部不再使用 Ribbon 或三行分组命令区。

## 4. Menu

### 文件

- 新建
- 打开
- 保存
- 另存为
- 设置
- 退出

### 绘图

- 点
- 直线
- 多段线
- 自由多边形
- 正多边形
- 矩形
- 圆
- 圆弧
- 椭圆
- 样条

Circle / Arc / Ellipse / Regular Polygon 的不同绘制方式通过二级菜单选择。

### 建模

- Box
- Cylinder
- Cone
- Frustum
- Sphere
- Ellipsoid
- Torus
- Helix
- Extrude
- Revolve
- Sweep
- Loft

### 视图

- Top / Bottom / Front / Back / Left / Right
- Iso NE / NW / SE / SW
- Fit
- Wireframe / Shaded

### 窗口

控制 Model Tree、Layers、Properties 的显示。

### 语言

切换 `中文 / English`。切换后 Menu、Toolbar、参数条、Panel 和提示会刷新，并保存语言偏好。

## 5. Toolbar

Toolbar 只保留高频入口，为单行布局。空间不足时可横向滚动。

当前图层 ComboBox 位于工具栏中，用于切换当前创建图层。

## 6. Tool 参数条

带参数的 Tool 启动后，Toolbar 下方会出现参数条；Tool 无参数时整条隐藏。

例如正多边形：

```text
正多边形： 边数 [6]  方式 [内接]
```

边数允许 3~360。

在指定圆心之前，也可以直接在 Viewport 输入边数并回车。例如：

```text
启动正多边形
→ 输入 8
→ Enter
→ 指定圆心
→ 指定半径
```

得到八边形。

Box、Cylinder、Cone、Frustum、Sphere、Ellipsoid、Torus、Helix、Ellipse、Extrude 等已有参数 Tool 使用同一参数机制。

## 7. 自由多边形与正多边形

### 自由多边形

逐点指定任意顶点，形成任意闭合 Polygon。

### 正多边形

指定：

1. 边数；
2. 内接/外切模式；
3. 圆心；
4. 半径。

两者是不同的 Tool 语义，不应混用。

## 8. 圆

支持：

- 圆心 + 半径；
- 圆心 + 直径；
- 两点；
- 三点；
- 点 + 圆心。

这些入口复用同一个 Circle Tool，不复制几何实现。

## 9. 圆弧

支持：

- 三点；
- 圆心 → 起点 → 终点；
- 起点 → 圆心 → 终点；
- 起点 → 终点 → 圆心；
- 起点 → 终点 → 弧上点；
- 起点 → 终点 → 切向。

## 10. 椭圆

支持：

- 中心 + 长短轴；
- 轴端点 + 短轴。

## 11. 精确输入

绘图时，精确输入由当前 Tool 阶段解释。数字含义不是全局固定的，而是跟随 Tool 当前 Prompt/Precision 状态。

例如正多边形在圆心前输入整数代表边数，圆心后则进入半径/长度角度输入阶段。

## 12. PropertyGrid

选择实体后，右侧 Properties 显示 Core Descriptor 属性。

规则：

- 数值左对齐；
- Layer 使用下拉；
- Boolean 使用勾选；
- Enum/Choice 使用下拉；
- Color 使用 ColorTable；
- Measurement 只读；
- 分类可折叠。

Point / Vector / Normal 为三行输入：

```text
X  [ ... ]
Y  [ ... ]
Z  [ ... ]
```

Circle / Arc / Ellipse / Rectangle / Regular Polygon 的 `Normal` 可以修改，并走真实几何与 Undo/Redo 事务路径。

## 13. Layers

Layers 面板支持：

- 过滤；
- 新建；
- 重命名；
- 删除非默认层；
- 当前层；
- 显示/隐藏；
- 颜色；
- 线型；
- 线宽；
- 锁定。

默认 Layer `0` 不允许删除或重命名。

## 14. 工作平面

底部：

- XY
- YZ
- XZ

用于切换绘图工作平面。

活动 Tool 不允许切换时，系统会拒绝切换而不是中途改变几何解释。

## 15. SNAP / ORTHO / POLAR

状态栏保留三个真实交互开关：

- SNAP：对象捕捉；
- ORTHO：正交跟踪；
- POLAR：极轴跟踪。

SNAP 菜单可配置 Endpoint、Midpoint、Intersection、Center、Perpendicular、Tangent、Quadrant、Extension、Insertion、Node、Apparent Intersection、Nearest。

## 16. Grip / Selection

初版基础设施包括：

- Replace / Add / Remove / Toggle；
- Window / Crossing Selection；
- Entity / Subobject Selection；
- Preselection；
- Grip / Hot Grip；
- Grip Edit Preview / Commit。

Grip、Snap、Preview、Tracking、Selection Window 都属于 transient，不进入 Document/History。

## 17. 状态栏

底部左侧显示当前操作提示，右侧显示 XY/YZ/XZ 和 SNAP/ORTHO/POLAR。

空闲时提示区可以为空。

不显示永久：

- Ready；
- Command: Ready；
- 版本信息；
- 永久 XYZ 坐标；
- 第二个命令输入框。

## 18. 保存与打开

保存后再次打开，应恢复正式 Document/Entity/Layer/Feature 状态。

Preview、Snap marker、Grip marker、Tracking guide 等 transient 不应被持久化。

## 19. 功能是否“完成”的判断

不能因为源码中存在 Entity/Tool/Action 就认为功能完成。必须实际验证：

`入口 → 参数 → Prompt → Preview → Exact Input → Commit → Property → Grip/Snap → Save/Open`

并确认 Commit/Cancel 后没有 Preview 或 Native transient 残留。
