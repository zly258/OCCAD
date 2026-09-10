# 06 开发路线

## 1. 策略

按“稳定性 → UI/交互底座 → 每个 Entity 完整闭环 → 文件与性能 → 高级建模”推进，不按按钮数量推进。每阶段结束前清理旧路径并按验收清单回归。

## 2. P0 稳定性与编译

先保证当前分支持续可编译；修复 WPF/WinForms 类型歧义、XAML 名称/事件错误和无效资源。完成 Tool/Grip/Property/Preview 的局部事务异常边界；Grip 拖拽不得闪退；Preview 原子替换并保留上一有效帧；Snap marker 隐藏后可重新显示；Active Tool 导航不破坏状态。

同时统一几何兜底：finite、正尺寸、容差、向量、索引、工作平面求交、ViewerObject 生命周期。

## 3. P1 原生经典 UI 收口

删除所有 Ribbon 残留依赖、命名、Style 和 Runtime Patch。主界面固定为 Menu + 常驻 ToolBar + Docks + StatusBar + CadToolPanel。清理 `ToolParameterHost`、`FactorInput`、旧 Layer column installer、重复色表/颜色 editor、临时 visual-tree patch。

菜单保持浅层；ToolBar 只放 S/F/T、CurrentLayer、L/A、锁、Finish/Cancel；Tool 专属 Radius/Width/Height 等进入非模态 Panel。Dock、ColorDialog、DPI、本地化统一。

## 4. P2 Point Resolve / Selection / Snap / Grip

完成唯一 ResolvePoint 流程；分离用户持久 WorkPlane 与 Tool 临时 Plane；ORTHO/POLAR/Axis/Angle/Length 优先级统一。

完成 Replace/Add/Remove/Toggle、Window/Crossing、Preselection/Subobject；统一 Hidden/Locked/Selectable 规则。逐 Entity 审核 Snap 与 Grip Kind/WorkPlane/MoveGrip；marker 固定屏幕尺寸且无闪烁。

## 5. P3 ToolPanel 与基础 Entity 闭环

2D：Point、Line、Polyline、Rectangle、Polygon、RegularPolygon、Circle、Arc、Ellipse、Spline。

3D：Box、Cylinder、Cone、Frustum、Sphere、Torus。Cone/Frustum 永久保持两个 Entity/Tool。

每个逐项完成 Tool、Preview、ParameterPanel（需要时）、Snap、Grip、Precision、Property、ByLayer、History、Serialization、Localization、退化输入和失败回滚。

## 6. P4 基础编辑

Move、Copy、Grip Edit 先做完整；再完成 Rotate、Scale、Mirror。编辑 Tool 对预选/后选对象有一致策略，MouseMove 只改 Preview Copy，Accept 一次提交。Delete/Hide/Isolate/ShowAll 保持 Action/state 操作。

## 7. P5 Layer / Property / Model

Layer 使用稳定集合/局部刷新，不整表重绑。完成单击 Color/Visible/Locked/LineWidth/LineStyle；当前层与被编辑层分离。Property 完成多选共同属性、ByLayer/custom、enum 本地化、3 位显示全精度存储和失败回滚。ModelTree 增量刷新并与 Viewport selection 同步。

## 8. P6 Document / File / DWG-DXF

稳定 serializer 版本、Entity Registry 反序列化、未知数据安全拒绝、modified/save-point 与 History 一致。大文件按读取→解析→模型→Viewer→完成分阶段，底部进度可取消。

DWG/DXF 通过 Parser/Adapter → CadEntity，解析器不直接操作 WPF/Viewer。优先完整几何、图层、颜色、线型、线宽、块/属性等实际展示，再扩实体类型。

## 9. P7 性能

PointerMove 热路径去重；presentation update 优先于 delete/recreate；display batch；Snap/Grip marker 缓存；Layer/Property/ModelTree 不全量刷新；大文件解析后台化；必要时再增加空间索引，不提前复杂化。

## 10. P8 高级建模

核心框架稳定后再增加 Extrude、Revolve、Boolean、Fillet/Chamfer 等，并继续复用同一 Tool/Preview/History/Property/Exception 契约，不建立第二套“3D 专用框架”。

## 11. 每阶段完成条件

运行 `build.ps1`；手工验证 Tool 激活、Preview、Snap/Tracking、精度输入、ToolPanel、Finish/Cancel/Backspace/RightClick、Undo/Redo、Property、Layer、Grip 非法位置、Active Tool 导航、中文/英文、Save/Open。阶段结束删除过渡实现并 squash 为 coherent commit。

详细发布门槛见 `12-ACCEPTANCE-CHECKLIST.md`。
