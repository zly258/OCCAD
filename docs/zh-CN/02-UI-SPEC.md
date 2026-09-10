# 02 UI 规范

## 1. 唯一界面基线

OCCAD 初版只允许一套主界面：**经典 CAD Menu + 单行 Toolbar + 底部固定 Tool 参数条 + 状态栏**。

禁止恢复或并存：

- Ribbon；
- 旧 CleanShell / Ribbon 兼容层；
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
├─ Workspace
│  ├─ Model Tree
│  ├─ CAD Viewport
│  └─ Layers / Properties
├─ Fixed Tool Parameter Strip   ← 始终占固定高度，内容按 Tool 更新
└─ Status Strip
   ├─ Current Operation Prompt
   └─ XY / YZ / XZ / SNAP / ORTHO / POLAR
```

Tool 参数条必须位于状态栏正上方。Tool 有无参数时都不改变这条区域的高度，避免 Viewport 在命令启动、阶段变化和退出时上下跳动。

## 3. 主题原则

应用使用 Avalonia 12 原生 `FluentTheme`，不维护 OCCAD 自定义全局控件皮肤。

`CadTheme` 只允许保留：

- Panel / Property / Status 等布局尺寸；
- Viewport、Overlay、Snap/Grip 等 CAD 场景必要颜色；
- CAD 表格式区域必要的边界和间距。

普通 Button / TextBox / ComboBox / CheckBox / Menu 必须优先使用原生 Fluent 状态，不重新引入 `cad-input`、`cad-compact`、`cad-primary` 等历史样式类。

自定义 ColorTable 属于 CAD 业务控件，继续保留。

## 4. Menu

顶层分类固定为：文件、绘图、建模、视图、窗口、语言。

Circle / Arc / Ellipse / Regular Polygon 等具有多种绘制方法的命令使用二级菜单，不复制 Tool 实现。

## 5. Toolbar

Toolbar 只是一行高频入口，不承担“把所有命令平铺出来”的职责。

要求：

- 单行；
- 横向空间不足时允许水平滚动；
- 当前阶段不使用功能图标；
- 文本短而明确；
- 使用原生 Button；
- 不强制统一自定义背景/边框；
- 当前图层 ComboBox 可放在工具栏末端；
- 语言切换可使用 ComboBox 或 Menu，不维护第二套语言状态。

## 6. 固定 Tool 参数条

`CadTool.ParameterPanel` 是参数条唯一数据源。参数条固定停靠在底部状态栏上方，不再作为顶部 Shell 的子控件反复挂载。

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

参数联动遵循两条规则：

1. **UI → Tool**：用户确认输入后写入当前 Tool，并立即刷新 Preview；
2. **Tool/Preview → UI**：可测量的实际 Preview 值反向刷新参数编辑器，但焦点正在编辑的控件绝不能被刷新覆盖。

当前必须持续验证的实时值包括：

- Circle：Radius / Diameter；
- Rectangle：Width / Height；
- Ellipse：Major Radius / Minor Radius；
- Box：Length / Width / Height；
- Cylinder：Radius / Height。

参数条的存在不能导致 Viewport 高度随 Tool 状态变化。

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
- 颜色单元格和 ColorTable 可保留 CAD 专用视觉。

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

Point / Vector / Normal 使用纵向三行工程输入：

```text
X  [ ... ]
Y  [ ... ]
Z  [ ... ]
```

Circle / Arc / Ellipse / Rectangle / Regular Polygon 的 `Normal` 可编辑，并通过真实几何旋转/事务路径生效。

## 10. Viewport

- 默认深色；
- 左下角 Triedron 保留；
- ViewCube 默认关闭；
- Snap / Grip / Preview / Tracking / Selection Window 只在对应生命周期出现；
- 不显示永久版本号、OCCT 字样、Ready、帮助水印；
- Preview/Transient 必须在 Commit/Cancel 后清理。

## 11. Status Strip

底部只显示当前操作提示、XY / YZ / XZ、SNAP、ORTHO、POLAR。空闲时提示区域可以为空。

禁止永久显示 `Ready`、`Command: Ready`、版本字符串、永久 XYZ 坐标、重复的 WorkPlane/Drafting 标题、第二个命令输入框。

## 12. Dialog

Dialog 使用原生 Fluent Button/TextBox/ComboBox，不建立单独 MessageBox 皮肤体系。

统一规则：

- 操作按钮内容水平、垂直居中；
- 设置页标签列使用共享 `Auto` 宽度，根据中英文最长标签自适应；
- Value 列使用剩余可用宽度；
- 不用固定 Label 宽度制造大面积空白或长文本截断；
- ColorTable 等 CAD 业务控件可保留必要自定义视觉。

## 13. 本地化

- `zh-CN` / `en-US`；
- Action/Tool ID 永远不随语言变化；
- UI 文本通过 localization key；
- 切换语言必须刷新 Menu、Toolbar、Tool 参数条、Panel 和状态提示；
- Tool 参数条在语言重建期间必须保持单一 Visual Parent；
- 语言偏好写回应用设置。

## 14. UI 验收

至少验证：

1. Entity / Tool / Action 注册一致；
2. Menu/Toolbar 入口可用；
3. Tool 参数条固定在底部且不引起 Viewport 跳动；
4. 参数输入与实际 Preview 值联动，编辑焦点不被打断；
5. Prompt、Preview、精确输入正确；
6. `Esc` 取消 Tool 并清空正式选择状态；
7. XY/YZ/XZ 在二维和分阶段三维 Tool 中行为一致；
8. Property 修改、Snap / Grip / Selection 按预期工作；
9. 设置页中英文长标签自适应，所有 Dialog 操作按钮居中；
10. Save/Open 能恢复；
11. Commit / Cancel 后无 Native transient 残留；
12. 125% / 150% DPI 布局可用；
13. Windows/Linux 实际 build 和手工交互验证通过后才能标记完成。

“源码存在”或“Action 已注册”不能作为完成依据。
