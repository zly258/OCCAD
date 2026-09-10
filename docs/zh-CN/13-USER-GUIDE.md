# 13 用户指南

## 1. 主界面

OCCAD 当前使用经典 CAD Shell：

```text
Menu
Toolbar Row 1
Toolbar Row 2

Model Tree | Viewport | Layers / Properties

Fixed Tool Parameter Strip
Operation Prompt | XY YZ XZ | SNAP ORTHO POLAR
```

Toolbar 最多两行，只放高频命令；完整命令仍通过 Menu 进入。

### 第一行

```text
撤销 重做 | 当前图层 | 上 前 右 轴测 充满 | 线框 着色
```

### 第二行

```text
直线 多段线 矩形 圆 圆弧 正多边形 | 长方体 圆柱体 球体 拉伸 旋转
```

文件操作和语言切换留在 Menu，不占用 Toolbar。

## 2. 撤销与重做

- Toolbar：撤销 / 重做；
- `Ctrl+Z`：撤销；
- `Ctrl+Y` 或 `Ctrl+Shift+Z`：重做；
- 当前存在活动 Tool 时，撤销/重做暂时禁用，先完成或取消当前命令。

按钮状态与 `CadHistory.CanUndo/CanRedo` 实时联动。

## 3. 视图

常用 Toolbar 入口：

- 上；
- 前；
- 右；
- 轴测；
- 充满；
- 线框；
- 着色。

“充满”对应内部 `view.fit`，用于让当前模型填充可视区域。完整正交视图和四个轴测方向仍可在“视图”Menu 中选择。

## 4. Tool 参数条

参数条固定在底部状态栏上方，始终保持稳定高度，因此启动或退出 Tool 不会让 Viewport 上下跳动。

有参数时显示真实参数编辑器；无参数时该行内容为空。

例如正多边形：

```text
正多边形： 边数 [6]  方式 [内接]
```

边数允许 3~360。在指定圆心之前也可以直接键入边数并回车。

参数联动规则：

- 修改参数条 → 写回当前 Tool；
- Tool/Preview 的实际尺寸变化 → 可反向刷新参数栏；
- 正在输入或下拉选择的控件不会被实时刷新覆盖。

## 5. 绘图

Menu 中保留完整二维入口：点、直线、多段线、自由多边形、正多边形、矩形、圆、圆弧、椭圆、样条。

Toolbar 只保留高频二维：直线、多段线、矩形、圆、圆弧、正多边形。

### 自由多边形

逐点指定任意顶点形成 Polygon。

### 正多边形

依次确定：

1. 边数；
2. 内接/外切；
3. 圆心；
4. 半径。

## 6. 三维与 Feature

Menu 中保留 Box、Cylinder、Cone、Frustum、Sphere、Ellipsoid、Torus、Helix、Extrude、Revolve、Sweep、Loft。

Toolbar 只保留高频：长方体、圆柱体、球体、拉伸、旋转。

## 7. 工作平面

底部 XY / YZ / XZ 是真实绘图工作平面。

绘图过程中切换工作平面时：

- 如果当前阶段允许切换，立即切换并刷新 Snap/Tracking/Preview；
- 如果当前 Tool 使用 fixed temporary ToolPlane，系统先 StepBack 到最近可安全切换的阶段，再切换；
- 不允许把已经确认的几何阶段强行解释到另一个平面。

## 8. ESC / Backspace / Enter

- `Esc`：取消当前 Tool，同时清空 Entity / Subobject / Preselection；
- `Backspace`：回退当前 Tool 阶段；
- `Enter/Space`：提交当前有效输入或完成 Tool；
- Commit/Cancel 后 Preview、Snap、Tracking 等 transient 必须清理。

## 9. PropertyGrid

选择实体后，右侧 Properties 显示 Core Descriptor 属性。

- Numeric 左对齐；
- Layer 使用下拉；
- Boolean 使用勾选；
- Enum/Choice 使用下拉；
- Color 使用自定义 ColorTable；
- Measurement 只读。

Point / Vector / Normal 使用三行：

```text
X  [ ... ]
Y  [ ... ]
Z  [ ... ]
```

## 10. Layers

Layers 面板支持过滤、新建、重命名、删除非默认层、当前层、显示/隐藏、颜色、线型、线宽和锁定。

线型与线宽列已经加宽，ComboBox 应能显示完整值；右侧工程属性区整体也比旧布局更宽。

默认 Layer `0` 不允许删除或重命名。

## 11. SNAP / ORTHO / POLAR

- SNAP：对象捕捉；
- ORTHO：正交跟踪；
- POLAR：极轴跟踪。

Snap、Grip、Preview、Tracking、Selection Window 都属于 transient，不进入 Document/History。

## 12. 保存与打开

保存后再次打开，应恢复正式 Document / Entity / Layer / Feature 状态。Preview、Snap marker、Grip marker、Tracking guide 等 transient 不进入持久化。

## 13. 功能是否“完成”的判断

不能因为源码中存在 Entity/Tool/Action 就认为功能完成。必须实际验证：

`入口 → 参数 → Prompt → Preview → Exact Input → Commit → Property → Grip/Snap → Save/Open`

并确认 Commit/Cancel 后没有 Preview 或 Native transient 残留。
