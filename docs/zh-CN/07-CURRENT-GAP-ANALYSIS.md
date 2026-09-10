# 07 当前实现差距分析

## 1. 目的

本文档不是功能愿望清单，而是以当前 `cad` 分支为基线，对照 `OCCTBIM-Source` 的模块边界和本项目既定目标，列出仍需收口的真实差距。开发顺序以消除这些差距为主，不再优先增加孤立功能。

## 2. 当前已经形成的基础

当前 Core 已经有 Workspace、Document、Layer、Entity Registry、Tool Registry、Action、Selection、Subobject、Preselection、WorkPlane、Snap、Tracking、Precision、Preview、Grip、History 等主要组件；Entity 已有独立的 ByLayer 颜色、线宽、线型状态和显示属性；2D/3D Entity 已按类型拆分；Cone 与 Frustum 已是独立 Entity。

当前 2D Entity 包括 Point、Line、Polyline、Rectangle、Polygon、RegularPolygon、Circle、Arc、Ellipse、Spline。当前 3D Entity 包括 Box、Cylinder、Cone、Frustum、Sphere、Torus。

这些类“存在”不代表功能已经完成，后续按第 8 节完成矩阵验收。

## 3. UI 差距

### 3.1 主界面

目标统一为原生 WPF 经典界面：`Menu + ToolBar + Viewport + Dock + StatusBar + 非模态 ToolPanel`。不再恢复 Ribbon，不再保留 Ribbon 命名、Style、Runtime Patch 或第三方 Ribbon 依赖。

当前仍需清理：

- `*RibbonButton` 等历史命名，即使控件已经是 MenuItem，也应逐步改为真实职责名；
- `ToolParameterHost`、`FactorInput` 等已经被浮动 ToolPanel 替代的隐藏旧控件；
- XAML 与运行时代码重复创建/修改同一 UI 的路径；
- `MainWindow` 过多 partial 中仅为修补 UI 而存在的代码；
- 硬编码 `SNAP/ORTHO/POLAR/AXIS/Polar 15°` 等可见文本；
- Dock 标题栏、搜索区、按钮区、表格行高和边距仍需统一。

### 3.2 Menu

常用命令最多一层展开。禁止形成“菜单 → 分类 → 子分类 → 命令”的三级路径。Draw 可通过分隔线组织 2D 与 3D，View 可通过分隔线组织方向与显示，Settings 只放语言、捕捉/跟踪配置等低频项。

### 3.3 ToolBar 与 ToolPanel

ToolBar 只保留当前层、S/F/T、阶段 L/A、锁定、Finish/Cancel 等公共交互。

Radius、Diameter、Width、Height、Depth、Sides、MajorRadius、MinorRadius 等实体/工具稳定参数必须进入 ToolPanel。一个 Tool 只有确实存在稳定参数时才提供 Panel，不要求每个 Tool 为了形式统一而创建空 Panel。

## 4. Preview 差距

Preview 已开始使用实体真实外观，但最终标准是：

`PreviewAppearance = ResolveAppearance(PreviewEntity, CurrentLayerContext)`。

需要继续确认：

- 新建实体在 AddEntity 前能正确继承当前绘图层，而不是默认 Layer 0；
- ByLayer Preview 与最终 Document Presentation 完全一致；
- Preview 更新失败保留最后有效帧；
- 不因鼠标经过退化位置而清空上一帧有效预览；
- 移动/复制/夹点编辑的多实体 Preview 也遵守同一规则；
- PointerMove 中减少 Shape 删除/重建，能更新 presentation state 时不重建对象。

## 5. Grip 差距

Grip 已具备 Kind、WorkPlane、ConstraintOrigin 和 PrecisionInputs，这是正确方向。仍需逐 Entity 对齐几何语义：

- Line：端点、必要时中点；
- Circle：中心、半径；
- Arc：中心/起终点/半径或与定义方式一致的控制点；
- Rectangle：四角、边中点、中心按编辑语义决定；
- Polyline/Polygon/Spline：顶点/控制点；
- Box：基点、长宽高方向；
- Cylinder/Cone/Frustum/Sphere/Torus：中心、半径、轴、高度等语义 Grip。

Grip Marker 要小、实心、固定屏幕尺寸、形状清晰。Hot 只轻微改变颜色/尺寸。Grip 拖拽必须始终走 Duplicate Preview → Validate → Commit/rollback，真实 Entity 不能在 MouseMove 阶段逐帧损坏。

## 6. Selection / Snap / WorkPlane 差距

需要完成统一验收：

- Point Pick 与 Window/Crossing 使用同一 Selection 规则；
- Replace/Add/Remove/Toggle 行为一致；
- Preselection 与正式 Selection 分离；
- Locked/Hidden/Selectable=false 规则在 Pick、Box、Grip、Property 中一致；
- Snap 优先级固定且可解释；
- Snap Marker 隐藏后能正确再次显示；
- 临时 Snap 不污染持久 Snap；
- 用户持久 WorkPlane 与 Tool 临时 WorkPlane 分离；
- Tool 结束后不意外恢复到过期平面；
- S/F/T 改绘图平面而不是相机方向。

## 7. Property / Layer 差距

Layer UI 必须实现一次点击即可操作，不通过重绑 `ItemsSource` 破坏当前编辑器。图层属性变化只更新相关项，禁止每次 `ItemsSource = null` 后整表重建。

Entity Property 必须明确支持：

- ColorByLayer / custom Color；
- LineWidthByLayer / custom LineWidth；
- LineStyleByLayer / custom LineStyle；
- enum 本地化显示；
- double 显示 3 位小数但保持真实精度；
- 多选共同属性；
- 修改失败回滚；
- 修改形成一次 History。

颜色按钮点击直接打开 ColorDialog。ByLayer 是独立属性，不伪装成特殊颜色值。

## 8. Entity 完成矩阵

每个 Entity 都必须逐项检查：

| 能力 | 必须 |
|---|---|
| 独立 Entity 类型/稳定 ID | 是 |
| BuildShape | 是 |
| Duplicate / RestoreGeometry | 是 |
| Translate / Rotate / Scale 合理支持 | 是 |
| SnapPoints / precision snap curves | 是 |
| GripPoints / MoveGrip | 是 |
| Tool 创建流程 | 是 |
| 实时 Preview | 是 |
| WorkPlane / precision | 是 |
| ByLayer 外观 | 是 |
| Property 编辑 | 是 |
| History | 是 |
| Serialization | 是 |
| 中文/英文显示 | 是 |
| 退化输入保护 | 是 |
| Preview/Grip 失败回滚 | 是 |

一个 Entity 只有上述闭环达到预期后才标记“完成”。

## 9. 稳定性差距

普通用户输入不允许导致进程退出。需要在局部事务层、Viewport/Input 分发层和 WPF Dispatcher 层形成三层边界，但不可恢复异常不伪装成功。

所有 `BuildShape`、`MoveGrip`、Preview、Property Apply、File Parse 都要考虑：NaN、Infinity、0/负尺寸、过小长度、退化向量、平行射线、空 Shape、OCCT 构造失败、已删除 ViewerObject、无效索引。

## 10. 性能差距

重点不是线程越多越好，而是避免 PointerMove 热路径的无效工作：

- 不重复创建相同 Marker pixmap；
- 不重复删除并创建同一个 overlay；
- Layer/Property 不整表刷新；
- Selection/Grip 更新使用批处理；
- 文件解析与大模型构建分阶段并报告进度；
- UI 线程只做必须的 UI/Viewer 调度。

## 11. 当前最高优先级

P0：可编译、不可闪退、Preview/Grip/Selection 状态正确。

P1：清理旧 UI 路径，完成原生经典界面和 ToolPanel。

P2：逐 Entity 完成 Snap/Grip/Property/History/Serialization 闭环。

P3：文件 I/O、大文件性能、DWG/DXF 展示完整性。

P4：再增加 Extrude/Revolve/Boolean 等复杂建模。
