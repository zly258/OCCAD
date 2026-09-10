# 03 交互规范

## 唯一交互链

鼠标、键盘、Command Line、Precision、Snap、Tracking、Grip 全部进入 Workspace 与 Active Tool。MainWindow 只负责转发和呈现，不直接修改 Entity 几何。

```text
Ribbon / Shortcut / Command Line / Viewport
                   ↓
          CadActionManager / ToolManager
                   ↓
      ResolvePoint / Selection / Preview
                   ↓
          Document / History commit
```

## Tool 生命周期

`Idle → Activate → Drawing/WaitForSelect → Preview → Commit Stage → Next/Complete`。

- `Esc`：取消当前 Tool；空闲时清空正式选择。
- `Backspace`：StepBack。
- `Enter`：由 ToolManager 接受当前有效阶段或 Finish。
- 右键：有活动 Tool 时走统一 Secondary Action；空闲时打开视口上下文菜单。
- Point Step 不得复用 stale/off-viewport 坐标。

## 视口导航

视口导航优先交给 OcctCSharpBridge 的原生 Viewport：中键平移/导航、滚轮缩放；`Shift + 中键` 旋转；中键双击 `FitAll`。导航期间不进入 CAD Selection 或 Tool Pointer 流程。

绘图状态使用 CAD 十字光标；导航状态切换为移动光标。Snap 标记、Grip 和框选覆盖层不能被普通 UI 控件遮挡。

## 工作平面与辅助绘图

绘图阶段：

- `T` → XY；
- `F` → XZ；
- `S` → YZ；
- `F3` → Object Snap；
- `F8` → Orthogonal Tracking；
- `F10` → Polar Tracking；
- `Tab / Shift+Tab` → 在有效 Snap Candidate 间循环。

工作平面切换只能通过 `CadToolManager.TryChangeDrawingPlane`，UI 不直接篡改 Tool Plane。

## 点解析

`Screen → view ray / active WorkPlane → Object Snap → Tracking → explicit axis/angle/length constraint → final world point`。

Tool 不重复 XY 投影、Polar 数学或锁定优先级。Command Line 的坐标、长度、角度和 Tool 参数输入统一进入 `CadCommandManager`。

## Selection / Preselection / Subobject

Selection、Preselection、SubobjectSelection 独立，不能相互代替。

- 点选：默认 Replace；`Ctrl` Add；`Shift` Remove；`Ctrl+Shift` Toggle。
- 左→右框选：Window。
- 右→左框选：Crossing。
- Hidden / Locked / Non-selectable 不进入正式 Selection。
- WaitForSelect Tool 复用同一套选择手势，不建立第二套临时 Selection。
- Subobject Selection 使用稳定 Reference；重建几何后由 Reference Resolver 重新定位。

## Grip

单对象正常选择时可显示 Grip。Grip 编辑：

`capture original → duplicate preview → suppress real presentation → MoveGrip(preview) → accept → apply once to real Entity → one History entry → clear preview → restore real presentation`。

PointerMove 不修改持久 Entity。

## Preview / Commit

Preview 永远不属于 Document、Selection 或 History。

普通 Commit：`Validate → Document/History mutation → Clear Preview → Complete Tool`。

Trim / Extend / Fillet / Chamfer 等 Replacement 操作在真实 mutation 成功前保留最后有效 preview 和 source suppression；失败时 Tool 保持可继续使用，不能留下残影。

## Command Line

底部 Command Line 是唯一完整 Prompt 与精确输入入口：

- 输入 Action Alias/ID 启动命令；
- Tool 活动时接受坐标、长度、角度、参数和 Tool Option；
- 空输入重复上一命令或接受当前阶段；
- `↑ / ↓` 浏览命令历史；
- 执行、取消后焦点返回 Viewport。

不再保留第二套悬浮命令框或重复 Tool Prompt 面板。

## Property / Layer

Entity Property 使用 `Capture → Validate → Apply → presentation/dependency refresh → History`。Layer 同样走 Core-owned transaction。

直接修改 Color / LineStyle / LineWidth 时必须正确处理对应 ByLayer 状态；ByLayer 切换同样进入 History。UI 不能直接写 Entity 或 Layer 字段绕过 transaction。

## Viewport Context Menu

空闲右键菜单按 Source 主流程收口，只保留：ShowAll、Hide、Isolate、SelectAll、SelectInvert、Move、Copy、Delete、Properties。不可执行项由 `CanExecute` 禁用，不显示无实现的占位动作。

## Prompt / Error

完整 Tool Prompt 只由 Command Line 区域显示；Status 仅显示选择、图层、Drafting、WorkPlane、坐标等短状态。可恢复失败留在 Tool/Grip/Property/Layer 局部事务内；Fatal failure 不做伪恢复。
