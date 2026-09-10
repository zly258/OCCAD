# 04 系统架构

## 1. 总体分层

OCCAD 采用分层、事件驱动、可注册扩展架构。参考 OCCTBIM-Source 的职责划分，但不照搬 Qt 单例：

```text
Presentation (WPF)
  MainWindow / Menu / ToolBar / Dock / ToolPanel / StatusBar / PropertyGrid
        ↓ observes + invokes
Application / Interaction
  Workspace / ActionManager / ToolManager / Selection / Snap / Tracking / Grip / Preview
        ↓
Domain
  Document / Entity / Layer / History / Registries / Serialization
        ↓
Geometry & Presentation Bridge
  OcctNet / OcctCSharpBridge / OCCT
```

依赖方向固定为 `WPF → Core → OcctNet`。Core 不引用 WPF；Entity 不引用 MainWindow；Tool 不直接操作控件。

## 2. Workspace

`CadWorkspace` 是会话组合根，持有 Document、Layers、Selection、Subobjects、Preselection、WorkPlane、Snap、Tracking、Precision、Preview、Grips、History、Tools、Actions 和 Engine 连接。Workspace 负责组装，不成为新的 God Object；具体规则留在各 Manager/Service。

## 3. Document

Document 是持久 Entity 状态唯一所有者。负责 Add/Remove/Find、Presentation 映射、`ResolveAppearance`、Entity Changed 处理、ChangeSet、Attach/Detach Engine、Document change event。Document 不负责鼠标、Tool 阶段、Menu/ToolBar 状态。

## 4. Entity

Entity 包含稳定 Id/Type、Name/Layer、Visible/Selectable、Color/LineWidth/LineStyle/Transparency/Material/DisplayMode 及 ByLayer 状态、几何数据与 BuildShape、Duplicate/Restore、Transform、Snap、Grip、Changing/Changed、序列化语义。

一种实体一个明确类型。Cone 与 Frustum 分离；不以大量 mode 枚举把多个实体塞入 GenericEntity。

## 5. Registry

Entity/Tool/Action 使用显式 Registry，建立稳定 ID 到 factory/metadata/serializer 的关系。禁止 UI 反射发现类型，禁止新增一个实体必须修改 MainWindow 大型 switch。

## 6. Action

Action 是瞬时命令入口：文件、Undo/Redo/Delete、视图、显示模式、激活 Tool、切换 Dock 等。Menu、ToolBar、快捷键共享 Action ID。Action 可以激活 Tool，但不承载持续 Pointer 状态。

## 7. Tool

Tool 是持续交互状态机，统一 Id、State、Activate、Stage/Prompt、Pointer/Keyboard、CanFinish、StepBack、PrecisionReferencePoint/Inputs、ParameterPanel、Preview、Commit、Cancel/Complete。任意时刻只有一个主 Tool 活跃。

Tool 专属稳定参数通过 `CadToolPanelDescriptor` 描述，WPF 使用唯一 `CadToolPanel` 基类呈现；Core 不知道具体 Window。

## 8. Selection / Preselection / Subobject

三类状态独立：Hover Preselection、正式 Entity Selection、Vertex/Edge/Face SubobjectSelection。Viewport 负责 hit 输入，SelectionManager 负责 modifier、Window/Crossing、selectability 规则。

## 9. WorkPlane / Precision / Tracking

统一点解析：screen/ray → WorkPlane → Snap → direction tracking → length → final point。明确区分用户持久 DrawingPlane、Tool 临时 Plane、Grip 自带 Plane。一次 Snap/Grip 不得永久篡改用户平面。

## 10. Snap

Entity 提供 Snap 语义，SnapManager 负责候选、优先级、像素容差和 transient marker。临时 snap override 与持久模式分离。

## 11. Grip

Entity 返回 GripPoint 的 Index、Kind、Position、ConstraintOrigin、PrecisionInputs、WorkPlane。GripManager 只显示、hit-test、hot 和启动 GripEditTool，不推断实体几何含义。

GripEditTool 对 Duplicate Preview 做 MoveGrip，失败回滚，Accept 后一次应用真实 Entity 和 History。

## 12. Preview

PreviewManager 保存 transient Entity/Presentation，不进入 Document。它支持单/多 Preview，原子替换，失败保留上一有效帧，并通过统一 Appearance Resolver 解析最终外观和当前层 ByLayer。

## 13. History

History 只保存业务状态，不保存 WPF/AIS 对象。连续 MouseMove 不产生 History；一次 Tool/Grip/Property Commit 产生一个逻辑 Entry。

## 14. Layer / Appearance

Layer 包含 Name、Color、LineWidth、LineStyle、Visible、Locked。Entity 独立 ByLayer flags 通过 Document 唯一外观解析器获得最终值。Preview 与正式 Presentation 不允许各写一套算法。

## 15. Property / Dock

PropertyGrid 是编辑视图，不是数据模型。Descriptor/Converter/Editor 只适配本地化、数值格式、ColorDialog。Dock 是 Document/Selection 的视图，由事件增量刷新，Model/Layer/Property 不直接互相驱动业务。

## 16. Presentation 生命周期

实体身份不依赖 ViewerObjectId。Entity 几何变更触发 presentation rebuild；外观变更触发 appearance apply。Preview、Grip、Snap、Preselection、Selection rectangle 分别拥有独立 transient 生命周期。

## 17. 异常与事务

所有可修改状态的路径采用 Capture → Validate/Build → Apply → History；失败 Restore。UI Dispatcher 只是最后兜底，不替代 Tool/Grip/Property/Document 局部事务。

## 18. 扩展原则

新增 Entity：Entity + 注册 + serializer/localization + 对应 Tool；新增 Tool：注册，不改 Pointer dispatcher；新增 Action：注册并供多个 UI 入口共用；新增 Grip/Snap 类型：扩语义和 renderer，不在每个 Tool 做实体类型判断。
