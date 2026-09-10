# 01 产品定位与开发目标

## 1. 产品定位

OCCAD 不是 OCCT API Demo，也不是把内核接口直接暴露到 UI。目标是一个轻量、可用、架构完整的桌面 CAD 框架和应用基础，稳定覆盖二维/三维绘制、编辑、选择、捕捉、夹点、属性、图层、文档、撤销重做和视口交互。

短期不追求成熟 CAD 的实体数量，但核心框架必须完整到新增 Entity、Tool、文件格式或业务模块时不需要重构主窗口和交互底座。

## 2. 从 OCCTBIM-Source 继承的思想

借鉴其 Document/Model、Entity、Tool、Action、Viewport、Dock、PropertyEditor、Grip、Snap 的职责边界，不照搬 Qt 控件、单例和具体类名。

- Entity 是带身份、图层、外观、几何、属性、Grip、Snap、复制/恢复和序列化语义的业务对象；
- Tool 是 Activate → 阶段输入 → Preview → Commit/Cancel/StepBack 的持续状态机；
- Action 是统一命令入口，Menu、ToolBar、快捷键共享稳定 Action Id；
- Document 是持久 Entity 状态的唯一所有者；
- Viewport 负责输入映射和显示，不拥有 CAD 业务规则；
- Grip/Snap 语义由 Entity 提供；
- Property/Dock 观察当前 Document/Selection，不保存第二份业务模型。

## 3. 首个可用版本

文档：New/Open/Save/SaveAs、modified state、Undo/Redo、异常不损坏当前文档。

2D：Point、Line、Polyline、Rectangle、Polygon、RegularPolygon、Circle、Arc、Ellipse、Spline 都走统一 Tool/Preview/Snap/Precision/Grip/Property/History/Serialization 契约。

3D：Box、Cylinder、Cone、Frustum、Sphere、Torus。Cone 与 Frustum 是两个独立 Entity/Tool，不以 TopRadius=0 混合类型。

编辑：至少 Delete、Move、Copy、Grip Edit 完整；Rotate、Scale、Mirror 继续沿相同框架扩展。

选择：Point Pick、Replace/Add/Remove/Toggle、Window/Crossing、Preselection、Locked/Hidden/Selectable 规则一致。

精确绘图：WorkPlane、Snap、ORTHO、POLAR、Axis/Angle/Length lock 使用统一 ResolvePoint 链。

属性/图层：ByLayer 与自定义颜色/线宽/线型都可用；颜色直接 ColorDialog；double UI 显示 3 位小数但保存完整精度。

## 4. UI 产品目标

唯一主界面是Avalonia 原生紧凑型 `Menu + 常驻 ToolBar + Viewport + Dock + StatusBar + 非模态 ToolPanel`。不再恢复 Ribbon，也不维护第三方 UI 主题体系。常用命令保持浅层菜单；ToolBar 放公共阶段输入；Radius/Width/Height 等稳定参数进入 ToolPanel。

## 5. 非目标

当前阶段不以所有 DWG/DXF Entity、完整参数约束、BIM 专业对象、大量高级曲面命令、插件市场或大型 smoke/check 框架作为完成标准。

## 6. 质量目标

稳定性：普通非法输入、退化几何、Grip/Preview/Property/File parse 失败不得直接退出应用，可恢复操作必须回滚。

一致性：Appearance、Point Resolve、Selection、History、Preview、Property 更新各自只有一个权威路径。

可扩展性：新增基础 Entity 原则上新增 Entity、Tool、注册和必要 serializer/localization，不修改 MainWindow 大型 switch。

性能：PointerMove 避免无意义 delete/recreate；批量显示使用 batch；大文件解析分阶段并提供进度。

国际化：中文、英文完整覆盖用户可见字符串，内部 ID 稳定不本地化。

## 7. 完成判定

一个功能只有在入口、Tool 生命周期、Preview/最终一致、正常/异常退出、Undo/Redo、UI 状态、本地化、持久化和扩展契约全部一致后才算完成。具体以 `12-ACCEPTANCE-CHECKLIST.md` 为发布门槛。
