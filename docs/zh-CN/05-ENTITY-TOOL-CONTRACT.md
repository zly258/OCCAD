# 05 Entity 与 Tool 开发契约

## 1. 为什么需要统一契约

OCCTBIM-Source 的 Entity 同时提供 shape、visual、properties、gripPoints、snapPoints、编辑副本和序列化；Tool 则统一处理阶段输入与 Preview。OCCAD 应把这两套契约固定下来，避免每增加一个实体就重新发明交互。

## 2. Entity 最小契约

每个实体必须具备：

- 稳定类型 Id；
- 独立几何字段，字段值始终有效；
- `BuildShape(OcctEngine)`；
- `Duplicate()`；
- `RestoreGeometry(snapshot)`；
- `Translate/Rotate/Scale`；
- `GetSnapPoints()`；
- `GetGripPoints()`；
- `MoveGrip(index, targetPoint)`；
- 属性变化事件；
- 可序列化状态。

几何属性 setter 必须验证 finite、positive、range 等约束。禁止把 NaN、Infinity、负半径、零方向向量等非法状态先写入字段再期待 BuildShape 兜底。

## 3. Entity 文件组织

推荐：

```text
Entities/
  CadLine.cs
  CadCircle.cs
  CadArc.cs
  CadRectangle.cs
  CadPolyline.cs
  CadBox.cs
  CadCylinder.cs
  ...
Tools/
  LineTool.cs
  CircleTool.cs
  ArcTool.cs
  RectangleTool.cs
  BoxTool.cs
  CylinderTool.cs
  ...
```

一个基本实体一个文件；一个主要 Tool 一个文件。共享几何算法放到明确的 geometry helper/service，不复制到多个 Tool。

## 4. Duplicate 与 Restore

`Duplicate()` 必须复制完整业务状态，包括几何和外观，但生成“新对象实例”。是否保留 Id 由调用契约明确：用于编辑快照时不依赖 Id 区分；用于真正 Copy 新实体时必须生成新 Id。

`RestoreGeometry()` 只恢复几何，不覆盖 Name/Layer/Appearance；`RestoreState()` 恢复完整业务状态。

## 5. Snap 契约

Entity 决定有哪些语义点和曲线。例如：

- Line：Endpoint、Midpoint；
- Circle：Center、Quadrant、Nearest/Tangent curve；
- Arc：Endpoint、Center、Mid/Quadrant as applicable；
- Polyline：Vertex、segment midpoint；
- Box：corner/center/edge-derived points as later needed。

SnapManager 只做候选收集、屏幕距离、优先级和策略过滤。

## 6. Grip 契约

每个 Grip 必须说明：

- `Index`：稳定索引；
- `Kind`：Control/Vertex/Midpoint/Center/Radius/Axis/Height；
- `Position`；
- `ConstraintOrigin`；
- `WorkPlane`；
- `PrecisionInputs`。

`MoveGrip` 的目标点是解析后的世界坐标，不再自行处理鼠标、Snap、Ortho。

常见示例：

### Line

- Start：Vertex；
- Mid：Midpoint，移动整个 Line；
- End：Vertex。

### Circle

- Center：Center，移动圆心；
- Radius grip：Radius，沿合理工作平面改变半径。

### Box

- Base/corner grips：控制底面范围；
- Height grip：沿高度轴改变 Height；
- 中心/移动 grip：整体移动。

Grip 拖拽中不得逐帧修改真实 Entity。

## 7. Tool 最小契约

每个 Tool 需要明确：

- Id；
- DisplayName localization key；
- Tool State；
- Stage 数量与每个 Stage Prompt；
- Pointer left/right/move 行为；
- Keyboard Enter/Esc/Backspace 行为；
- `CanFinish`；
- `PrecisionInputs`；
- `PrecisionReferencePoint`；
- 是否锁 WorkPlane；
- `ParameterPanel`；
- Preview 生成；
- Commit；
- Cancel 清理。

## 8. Tool 参数与阶段参数

严格区分两类参数：

### 阶段动态参数

由 pointer + precision 产生：当前点、长度、角度、位移向量等。显示在 ToolBar/HUD。

### 稳定 Tool 参数

Radius、Width、Height、Sides、Mode 等。显示在浮动 ToolPanel，并可以即时修改 Preview。

同一个值如果既能鼠标确定又能 Panel 输入，应共享一个 Tool 内部字段，而不是两份状态。

## 9. Preview 契约

Preview Entity 使用与最终 Entity 相同的几何对象类型和参数。不要另写“简化 preview shape 算法”导致 Commit 后形状跳变。

PreviewManager 负责 presentation；Tool 负责提供 preview business entity。

Preview 更新失败时：

- 当前有效 Preview 保留；
- Tool 不自动完成；
- 用户可继续移动/输入；
- StatusBar 给出可理解的提示。

## 10. Commit 契约

Commit 顺序：

```text
Validate current state
→ create/finalize Entity
→ apply current layer and appearance semantics
→ Document add/update inside change set
→ record one History entry
→ clear Preview/transient state
→ complete Tool
```

禁止先删除 Preview/原对象，再尝试构造最终 Shape；这样失败会造成视觉和业务状态丢失。

## 11. Cancel / StepBack

Cancel 必须清除 Tool 自己创建的所有 transient state，并恢复临时 WorkPlane/locks。不得删除用户原有实体。

StepBack 回退一个阶段，不等同于 Cancel Tool。Polyline 等多阶段工具要能逐点回退。

## 12. Property 契约

Entity 暴露的属性必须具有稳定的业务含义。UI DisplayName/Category/Description 通过 localization descriptor 映射，不把中文直接写进 Core property attribute。

Double 显示 3 位小数，但 setter 接收完整 double。Color 使用 ColorDialog；枚举显示本地化名称但保存原 enum。

## 13. 异常与验证

所有 Entity/Tool 都遵循：

- 输入边界先验证；
- 可恢复失败只影响当前操作；
- 修改真实对象前 Capture；
- 修改成功后才记录 History；
- Catch 后必须恢复一致状态，不能只是空 catch。

## 14. 新增 Entity/Tool 的完成清单

新增功能只有同时满足以下条件才算完成：

- Entity 几何与属性；
- Registration；
- Tool 阶段与 Prompt；
- Preview；
- Snap；
- Grip；
- Precision；
- PropertyGrid；
- Layer/ByLayer；
- Undo/Redo；
- Serialization；
- 中文/英文；
- 错误和退化输入；
- 取消、回退、右键完成；
- 导航期间 Tool 不丢状态。
