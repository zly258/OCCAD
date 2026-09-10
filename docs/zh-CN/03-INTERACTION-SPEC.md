# 03 交互规范

## 1. 唯一交互链

鼠标、键盘、Precision、Snap、Tracking、Grip 全部进入 Workspace 与 Active Tool。MainWindow 只负责命令触发和状态显示，不直接修改 Entity 几何。

```text
Toolbar / Shortcut
       ↓
CadActionManager
       ↓
Action / CadTool
       ↓
ResolvePoint / Tool State / Preview
       ↓
Document / History Commit
```

初版不显示永久 Command Input；当前 Tool 的操作提示显示在底部状态栏。

## 2. Tool 生命周期

统一生命周期：

`Idle → Activate → Drawing/WaitForSelect → Preview → Commit Stage → Next/Complete → Neutral`

规则：

- 激活新 Tool 前必须取消当前 Tool；
- Activate 时清理 Preselection、Subobject 和 Grip；
- 每个 Tool 独占自己的 Transient/Preview 生命周期；
- Esc = Cancel；
- Backspace = StepBack；
- Enter/Space 按统一 ToolManager 规则接受当前有效阶段或 Finish；
- 右键在可完成时 Finish，否则 Cancel；
- Complete/Cancel 后必须恢复 Neutral 状态；
- Point Step 不复用 stale/off-viewport 坐标。

这些行为语义参考 OCCTBIM-Source 的 Tool/ToolManager 交互方式，但 OCCAD 保留当前更严格的 Transaction、Transient Owner 和 Neutral State 契约，不回退到旧的 UI/Viewer 主导架构。

## 3. 工作平面

`WorkPlane` 是 Tool 点输入的正式几何平面，不是 UI 辅助线。

- Tool 可在阶段开始时固定工作平面；
- Tool 固定后用户不能强制切换；
- 切换 XY/YZ/XZ 后必须清理 Snap/Tracking 并重建 Preview；
- Tool 完成或取消后必须 `EndToolPlane()`；
- 工作平面不得在 Tool 结束后残留 Active 状态。

## 4. 点解析

统一点解析链：

`Screen → View Ray / Active WorkPlane → Object Snap → Tracking → Axis/Angle/Length Constraint → Final World Point`

Tool 不重复实现 XY 投影、Polar 或锁定优先级。

## 5. Snap / Selection

Entity 提供语义 Snap；SnapManager 负责候选收集、屏幕容差、深度、类型优先级和临时模式。

Selection、Preselection、SubobjectSelection 相互独立：

- 左→右框选 = Window；
- 右→左框选 = Crossing；
- Hidden / Locked / Non-selectable 不进入 Formal Selection；
- 激活普通绘图 Tool 时 Grip 必须清空；
- Tool 结束后按当前 Selection 恢复 Grip。

## 6. Grip

Grip 编辑采用事务式 Preview：

`capture original → duplicate preview → suppress real presentation → MoveGrip(preview) → Preview.Update → accept → apply once → one History entry → clear preview → restore presentation`

PointerMove 不修改持久 Entity。

## 7. Preview / Commit

Preview 永远不属于 Document、Selection 或 History。

普通 Commit：

`Validate → Document/History mutation → Clear Preview → Complete Tool`

鼠标移动产生的中间几何是临时样本。`ArgumentException`、`InvalidOperationException`、`ArithmeticException` 和 OCCT 在预览阶段报告的 `OcctException` 应清理当前 Preview/Snap/Tracking 并保持 Tool 激活；最终正式 Commit 的失败不能被伪装成成功。

这样可以避免 Revolve、Sweep、Loft 等在某些临时鼠标位置产生不可构造几何时直接打断整个命令。

## 8. Entity / Tool / Action 一致性

产品功能面必须保持：

`Entity Registry ↔ Tool Registry ↔ Action Registry ↔ Toolbar ↔ Model Tree`

一致。

禁止在 Model Tree 或 UI 中维护第二套过时 Entity 白名单。内部 `Path` 等仅用于 Feature 持久化的 Entity 不作为普通用户模型节点展示。

初版不恢复 Move / Copy / Rotate / Scale / Mirror / Array / Trim / Extend / Fillet / Chamfer 等编辑命令；模型树也不提供 Delete 入口。

## 9. Property / Layer

Entity Property 使用：

`Capture → Validate → Apply → Presentation/Dependency Refresh → History`

Layer 同样走 Core-owned transaction。直接修改 Color / LineStyle / LineWidth 自动关闭对应 ByLayer；ByLayer 切换也进入 History。

## 10. Prompt / Error

- 当前 Tool Prompt 显示在底部操作提示区；
- 空闲状态保持空白，不显示“命令：就绪”或 `Ready`；
- 可恢复 Preview 失败不弹致命错误窗口；
- Property/Layer/文件操作失败必须明确反馈；
- OOM、StackOverflow、AccessViolation 等 Fatal failure 不做伪恢复。
