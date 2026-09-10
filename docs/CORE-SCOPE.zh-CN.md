# OCCAD 核心范围

本轮先收缩默认功能面，优先稳定 CAD 的核心交互与实体生命周期。

## 默认保留

- 二维：Point、Line、Polyline、Rectangle、Circle、Arc
- 三维基本体：Box、Cylinder、Cone、Sphere
- 编辑：Move、Copy、Delete
- 核心交互：Selection、Preselection、Snap、Grip、WorkPlane、Precision Input、Preview
- 文档与历史：New、Clear、Undo、Redo，以及现有 Save/Open 基础设施
- Layer 与 Property
- View 与显示模式
- Distance 测量

## 暂时退出默认功能面

Dimension、Text、Ellipse/Spline/Polygon、Array、Mirror/Rotate/Scale、Trim/Extend/Offset/Join/Break、Fillet/Chamfer、Region/Extrude/Revolve/Boolean/Sweep/Loft/Shell、复杂实体特征与路径编辑等。

第一轮不立即物理删除这些实现，避免一次性破坏大量依赖关系。核心交互稳定并通过本地编译/回归验证后，再进行第二轮源码清理。

## 交互所有权约束

持久 Entity 归 Document 管理；Preview、Tracking 等临时显示对象必须归当前活动 Tool 管理。Commit、Finish、Cancel、Tool 替换以及 Activate 失败都必须汇合到同一个中性状态清理路径。任何绘图 Tool 退出后都不能遗留 Preview、Snap、Tracking、Preselection、临时锁或全局 OCCT Highlight 状态。
