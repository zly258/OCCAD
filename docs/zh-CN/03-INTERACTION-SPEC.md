# 03 交互规范

## 唯一交互链
鼠标、键盘、Command Line、Precision、Snap、Tracking、Grip 全部进入 Workspace 与 Active Tool。MainWindow 不直接修改 Entity 几何。

## Tool 生命周期
`Idle → Activate → Drawing/WaitForSelect → Preview → Commit Stage → Next/Complete`。Esc=Cancel；Backspace=StepBack；Enter/Space 按统一 ToolManager 规则接受当前有效非 Point Step 或 Finish；右键有意义时 Finish，否则 Cancel。Point Step 不复用 stale/off-viewport 坐标。

## 点解析
`Screen → view ray / active WorkPlane → Object Snap → Tracking → axis/angle/length constraint → final world point`。Tool 不重复 XY 投影、Polar 和锁定优先级。

## Snap / Selection
Entity 提供语义 Snap；SnapManager 负责候选排名。Selection、Preselection、SubobjectSelection 独立。左→右为 Window，右→左为 Crossing。Hidden/Locked/Non-selectable 不进入 Formal Selection。

## Grip
Grip 编辑：capture original → duplicate preview → 抑制真实 presentation → 每帧恢复 original 到 preview → MoveGrip(preview) → Preview.Update → Accept → 一次写回真实 Entity → 一条 History → clear preview → restore real presentation。PointerMove 不修改持久 Entity。

## Preview / Commit
普通 Commit：Validate → Document/History mutation → Clear Preview → Complete Tool。Replacement 操作 mutation 期间保持最后有效 preview 和 source suppression；成功后再 cleanup，失败时 Tool 保持可用。

## Property / Layer
Entity Property 使用 `Capture → Validate → Apply → refresh → History`。Layer 同样走 Core-owned transaction。直接修改 Color/LineStyle/LineWidth 自动关闭对应 ByLayer；ByLayer 切换也进入 History。

## Prompt / Error
完整 Tool Prompt 只由 Command Line 显示。可恢复失败留在 Tool/Grip/Property/Layer 局部事务内；Fatal failure 不做伪恢复。
