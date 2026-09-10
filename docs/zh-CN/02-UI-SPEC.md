# 02 UI 规范

## 目标

OCCAD 的界面以 `OCCTBIM-Source/release-1.0` 的 CAD 工作流为基准，但不照搬 Qt 控件和历史布局。原则是：**核心命令完整、界面层级少、视口面积优先、同一状态只有一个入口和一个 owner**。

## 固定工作区

主窗口只保留六个长期区域：

```text
┌──────────────────────────── Ribbon ────────────────────────────┐
├──────── Model ────────┬──────────── Viewport ────────────┬─────┤
│                       │                                   │属性/│
│                       │                                   │图层 │
├───────────────────────┴───────────────────────────────────┴─────┤
│ Command Line                                                   │
├────────────────────────────────────────────────────────────────┤
│ Status                                                         │
└────────────────────────────────────────────────────────────────┘
```

- Ribbon：紧凑、低高度，只显示已有 Core Action。
- Model：模型对象浏览与选择，不复制 Property 功能。
- Viewport：唯一主要工作区，保留 ViewCube/Triedron 和原生导航。
- 右侧 Inspector：`属性 / 图层` 两个 Tab，共用同一列，不永久堆叠两个 Dock。
- Command Line：唯一完整 Prompt、命令和精确输入入口。
- Status：只显示选择、当前图层、Snap、Ortho、Polar、WorkPlane、坐标。

## 明确删除或不恢复的 UI

以下内容在没有真实产品语义前不得加入：

- 大型品牌横幅、欢迎横幅、装饰性标题区；
- 第二套 View 工具条、重复的选择/视图按钮；
- 假文档 Tab、没有多文档生命周期支撑的“+”页签；
- 独立悬浮 Tool Panel 与 Command Line 重复 Prompt；
- 常驻 Log、Memory Monitor、Component Dock；
- 没有 Core Action/Transaction 支撑的按钮；
- 仅为“看起来像 CAD”而存在的占位命令、不可执行菜单和假设置项。

## Ribbon

Ribbon 参考 Source 的命令分组，但做产品收口：

- 开始：Undo / Redo / Delete / Select / Measure；
- 绘图：Line / Polyline / Circle / Arc / Ellipse / Rectangle / Polygon / Spline；
- 三维：Source 已有且 OCCAD 已实现的 Primitive 与 Extrude/Revolve/Sweep/Loft；
- 修改：Move / Copy / Rotate / Scale / Mirror / Array / Offset / Trim / Extend / Fillet / Chamfer；
- 注释：CenterLine / Text / Dimension；
- 视图：Fit / Orientation / Display / Hide / Isolate / ShowAll。

一个按钮只有在 `CadActionManager` 中存在真实 Action 时才能显示。Core 中存在但不属于当前 Source 主工作流的能力可以保留，但默认 UI 不必暴露。

## Property / Layer

Property 由 `CadPropertyService` 动态描述并通过 Core transaction 修改；UI 不直接写 Entity 字段。稳定 Entity ID 属于数据层信息，默认 Inspector 不显示。

Layer 只保留：当前层、新建、可见、锁定和必要的属性编辑。Entity 通过稳定 `LayerId` 关联 Layer，名称只是用户可编辑显示值。

ByLayer 必须使用“随层”开关表达；关闭随层后才允许直接编辑 Color / LineStyle / LineWidth。

## 视觉规则

- 应用外壳使用浅色紧凑工业风，Viewport 使用深色背景。
- 默认字号和控件高度以 100%/125% 缩放下都能完整显示为准。
- 不依赖大图标表达命令；文字必须可读，图标仅在信息增益明确时使用。
- 分隔、边框、Header 使用弱对比；选中和关键状态使用单一蓝色强调。
- 不为视觉效果牺牲视口尺寸。

## 职责边界

Avalonia 只能：呈现 Core 状态、调用 Action/Tool/Property/Layer transaction、转发输入。它不得拥有第二套 Selection、Snap、Grip、WorkPlane、Preview、History、Layer 或 Entity 状态。
