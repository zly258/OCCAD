# 02 UI 规范

## 1. 唯一界面基线

OCCAD 初版只允许一套主界面：**经典 CAD Menu + 单行 Toolbar + 当前 Tool 参数条**。

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
├─ Single-row Toolbar
├─ Active Tool Parameter Strip   ← 当前 Tool 有参数时才显示
├─ Workspace
│  ├─ Model Tree
│  ├─ CAD Viewport
│  └─ Layers / Properties
└─ Status Strip
   ├─ Current Operation Prompt
   └─ XY / YZ / XZ / SNAP / ORTHO / POLAR
```

## 3. 主题原则

应用使用 Avalonia 12 原生 `FluentTheme`，不启用 `DensityStyle.Compact`，不注册 OCCAD 全局 Button/TextBox/ComboBox/TreeView 皮肤。

`CadTheme` 只允许保留：

- Panel/Property/Status 等布局尺寸；
- Viewport、Overlay、Snap/Grip 等 CAD 场景视觉；
- CAD 表格式区域确有必要的颜色/分割线。

普通控件必须优先使用原生 Fluent 状态。

自定义 ColorTable 属于 CAD 业务控件，继续保留。

## 4. Menu

顶层分类固定为：

- 文件；
- 绘图；
- 建模；
- 视图；
- 窗口；
- 语言。

Circle / Arc / Ellipse / Regular Polygon 等具有多种绘制方法的命令使用二级菜单，不复制 Tool 实现。

例如：

```text
绘图
├─ 点
├─ 直线
├─ 多段线
├─ 自由多边形
├─ 正多边形
│  ├─ 内接
│  └─ 外切
├─ 矩形
├─ 圆
├─ 圆弧
├─ 椭圆
└─ 样条
```

## 5. Toolbar

Toolbar 只是一行高频入口，不承担“把所有命令平铺出来”的职责。

要求：

- 单行；
- 横向空间不足时允许水平滚动；
- 不使用功能图标；
- 文本短而明确；
- 使用原生 Button；
- 不强制统一自定义背景/边框；
- 当前图层 ComboBox 可放在工具栏末端；
- 语言切换可使用 ComboBox 或 Menu，不维护第二套语言状态。

## 6. 当前 Tool 参数条

这是初版必须存在的工程输入入口。

当 `CadTool.ParameterPanel` 有参数时，Toolbar 下方显示参数条；没有参数时整条隐藏。

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

### 正多边形

```text
正多边形： 边数 [6]  方式 [内接]
```

- 边数范围 3~360；
- 模式为 Inscribed / Circumscribed；
- 指定圆心前，可直接键入边数并回车；
- 指定圆心后，数字输入重新属于当前半径/精确输入阶段。

同一机制服务于 Box、Cylinder、Cone、Frustum、Sphere、Ellipsoid、Torus、Helix、Ellipse、Extrude 等已有参数 Tool。

## 7. Model Tree

- 左侧窄面板；
- 原生 TextBox 过滤；
- 原生 TreeView；
- 不使用实体装饰图标；
- Entity 类型来自 `CadEntityRegistry`；
- 内部 `Path` 等持久化辅助实体不作为普通节点；
- 节点与 Workspace Selection 同步；
- 初版 ContextMenu 只保留 Properties、Show/Hide、Lock/Unlock、Rename。

## 8. Layers

- 右上区域；
- 过滤、新建、重命名、删除使用原生控件；
- 默认 Layer 不允许重命名/删除；
- 当前层、可见性、颜色、线型、线宽、锁定均直接绑定真实 Layer 状态；
- 颜色单元格和 ColorTable 可保留 CAD 专用视觉；
- 不重新引入 `cad-input`、`cad-compact` 等全局样式类。

## 9. PropertyGrid

PropertyGrid 由 Core Descriptor 驱动。

布局要求：

- Property / Value 两列；
- 标签和值左对齐；
- Numeric 左对齐；
- 分类使用原生 `Expander`；
- Mixed Value 明确显示；
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

不得再使用横向 `X Y Z` 三输入框压缩布局。

Circle / Arc / Ellipse / Rectangle / Regular Polygon 的 `Normal` 可编辑，并通过真实几何旋转/事务路径生效。

## 10. Viewport

- 默认深色；
- 左下角 Triedron 保留；
- ViewCube 默认关闭；
- Snap / Grip / Preview / Tracking / Selection Window 只在对应生命周期出现；
- 不显示永久版本号、OCCT 字样、Ready、帮助水印；
- Preview/Transient 必须在 Commit/Cancel 后清理。

## 11. Status Strip

底部只显示：

- 当前操作提示；
- XY / YZ / XZ；
- SNAP；
- ORTHO；
- POLAR。

空闲时提示区域可以为空。

禁止永久显示：

- `Ready`；
- `Command: Ready`；
- 版本字符串；
- 永久 XYZ 坐标；
- 重复的 WorkPlane/Drafting 标题；
- 第二个命令输入框。

## 12. Dialog

Dialog 使用原生 Fluent Button/TextBox/ComboBox，不建立单独“MessageBox 皮肤体系”。

允许 CAD 业务型控件使用必要的自定义视觉，例如 ColorTable。

## 13. 本地化

- `zh-CN` / `en-US`；
- Action/Tool ID 永远不随语言变化；
- UI 文本通过 localization key；
- 切换语言必须刷新 Menu、Toolbar、Tool 参数条、Panel 和状态提示；
- 语言偏好写回应用设置。

## 14. UI 验收

一个功能只有同时满足以下条件才算“完成”：

1. Entity / Tool / Action 注册一致；
2. Menu/Toolbar 入口可用；
3. 必要参数有可见输入入口；
4. Prompt 明确；
5. Preview 正确；
6. 精确输入正确；
7. Commit / Cancel 返回 neutral；
8. Property 可正确修改；
9. Snap / Grip / Selection 按预期工作；
10. Save/Open 能恢复；
11. 不产生 Native transient 残留；
12. Windows/Linux 实际 build 和手工交互验证通过。

“源码存在”或“Action 已注册”不能作为完成依据。
