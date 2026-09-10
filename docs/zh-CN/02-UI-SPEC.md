# 02 UI 规范

## 1. 唯一界面基线

OCCAD 初版只允许一套主界面：**经典 CAD Menu + 最多两行紧凑 Toolbar + 底部固定 Tool 参数条**。

禁止恢复或并存：

- Ribbon；
- 三行分组命令区；
- 第二套 Toolbar；
- Floating Tool Panel；
- 永久 Dynamic HUD；
- 永久底部 Command Input；
- 仅为了视觉完整而存在的占位命令。

UI 只暴露已经注册且能进入真实 Core 路径的功能。

## 2. 主窗口结构

```text
MainWindow
├─ Menu
│  ├─ 文件
│  ├─ 绘图
│  ├─ 建模
│  ├─ 视图
│  ├─ 窗口
│  └─ 语言
├─ Toolbar Row 1
│  └─ Undo / Redo | Layer | Views / Fill | Display mode
├─ Toolbar Row 2
│  └─ 常用 2D | 常用 3D / Feature
├─ Workspace
│  ├─ Model Tree
│  ├─ CAD Viewport
│  └─ Layers / Properties
├─ Fixed Active Tool Parameter Strip
└─ Status Strip
   ├─ Current Operation Prompt
   └─ XY / YZ / XZ / SNAP / ORTHO / POLAR
```

Toolbar 可以只有一行，也可以使用两行；当前基线使用两行高频入口。参数条始终固定在底部状态栏上方，不得因为 Tool 有无参数改变 Viewport 高度。

## 3. 主题原则

应用使用 Avalonia 12 原生 `FluentTheme`，不启用 `DensityStyle.Compact`，不注册 OCCAD 全局 Button/TextBox/ComboBox/TreeView 皮肤。

`CadTheme` 只允许保留：

- Panel/Property/Status 等布局尺寸；
- Viewport、Overlay、Snap/Grip 等 CAD 场景视觉；
- CAD 表格式区域确有必要的颜色/分割线。

普通控件优先使用原生 Fluent 状态。自定义 ColorTable 属于 CAD 业务控件，继续保留。

## 4. Menu

顶层分类固定为文件、绘图、建模、视图、窗口、语言。

Circle / Arc / Ellipse / Regular Polygon 等具有多种绘制方法的命令使用二级菜单，不复制 Tool 实现。

文件操作和语言切换保留在 Menu，不占用高频 Toolbar 空间。

## 5. Toolbar

Toolbar 只保留高频命令，不承担“把所有命令平铺出来”的职责。

当前推荐布局：

```text
Row 1
撤销 重做 | 当前图层 | 上 前 右 轴测 充满 | 线框 着色

Row 2
直线 多段线 矩形 圆 圆弧 正多边形 | 长方体 圆柱体 球体 拉伸 旋转
```

要求：

- 最多两行；
- 每行空间不足时允许水平滚动；
- 不使用功能图标；
- 文本短而明确；
- 使用原生 Button / ComboBox；
- 不恢复 New/Open/Save/Language 等低频 Toolbar 按钮；
- Undo/Redo 必须与 `CadHistory.CanUndo/CanRedo` 联动；
- Tool 活动期间 Undo/Redo 置为不可用；
- `view.fit` 的中文界面名称统一显示为“充满”。

## 6. 当前 Tool 参数条

参数条固定停靠在底部状态栏上方，保持稳定高度。当前 Tool 无参数时内容为空，但该行仍保留，避免 Viewport 上下跳动。

Descriptor 映射：

```text
Integer        → TextBox
Double         → TextBox
OptionalDouble → TextBox
Boolean        → CheckBox
Choice         → ComboBox
String         → TextBox
```

输入必须调用 `tool.TrySetParameter()`，不能由 UI 直接修改 Tool 私有字段。

参数联动要求：

- UI 编辑值写回当前 Tool；
- Tool/Preview 可观测的实际尺寸可反向刷新参数栏；
- 用户正在编辑或选择的控件不得被实时刷新覆盖；
- Tool/语言切换后重新绑定真实参数状态。

### 正多边形

```text
正多边形： 边数 [6]  方式 [内接]
```

- 边数范围 3~360；
- 模式为 Inscribed / Circumscribed；
- 指定圆心前可直接键入边数并回车；
- 指定圆心后，数字输入重新属于当前半径/精确输入阶段。

## 7. Model Tree

- 左侧窄面板；
- 原生 TextBox 过滤；
- 原生 TreeView；
- 不使用装饰图标；
- Entity 类型来自 `CadEntityRegistry`；
- 内部 `Path` 等持久化辅助实体不作为普通节点；
- 节点与 Workspace Selection 同步。

## 8. Layers

- 右上区域；
- 过滤、新建、重命名、删除使用原生控件；
- 默认 Layer 不允许重命名/删除；
- 当前层、可见性、颜色、线型、线宽、锁定直接绑定真实 Layer 状态；
- 线型和线宽列必须有足够宽度显示 ComboBox 内容，不得像旧布局一样只露出一个字符；
- 当前右侧面板采用更宽的工程属性区，Name 继续占剩余空间；
- 颜色单元格和 ColorTable 可保留 CAD 专用视觉。

## 9. PropertyGrid

PropertyGrid 由 Core Descriptor 驱动。

- Property / Value 两列；
- 标签和值左对齐；
- Numeric 左对齐；
- 分类使用原生 `Expander`；
- Layer 使用 ComboBox；
- Boolean 使用 CheckBox；
- Enum/Choice 使用 ComboBox；
- Color 使用 OCCAD ColorTable/Color Dialog；
- Measurement 只读。

Point / Vector / Normal 必须纵向三行：

```text
X  [ ... ]
Y  [ ... ]
Z  [ ... ]
```

## 10. Viewport 与视图

- 默认深色；
- 左下角 Triedron 保留；
- ViewCube 默认关闭；
- Snap / Grip / Preview / Tracking / Selection Window 只在对应生命周期出现；
- `view.fit` 对用户统一显示为“充满 / Fill”；
- Preview/Transient 必须在 Commit/Cancel 后清理。

## 11. Status Strip

底部只显示：当前操作提示、XY / YZ / XZ、SNAP、ORTHO、POLAR。空闲时提示区域可以为空。

禁止永久显示 Ready、Command: Ready、版本字符串、永久 XYZ 坐标、重复 WorkPlane/Drafting 标题和第二个命令输入框。

## 12. Dialog

Dialog 使用原生 Fluent Button/TextBox/ComboBox。按钮内容水平、垂直居中；设置类表单使用自适应 Label 列和剩余空间 Value 列。

## 13. 本地化

- `zh-CN` / `en-US`；
- Action/Tool ID 不随语言变化；
- 切换语言必须刷新 Menu、Toolbar、参数条、Panel 和状态提示；
- 语言偏好写回应用设置。

## 14. UI 验收

一个功能只有同时满足以下条件才算完成：入口真实可用、必要参数可见、Prompt/Preview/Exact Input 正确、Commit/Cancel 返回 neutral、Property 可修改、Snap/Grip/Selection 正确、Save/Open 可恢复、无 Native transient 残留，并通过 Windows/Linux 实际 build 与手工交互验证。
