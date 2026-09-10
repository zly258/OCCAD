# 03 交互规范

## 1. 唯一交互链

鼠标、键盘、Precision、Snap、Tracking、Grip 全部进入 Workspace 与 Active Tool。MainWindow 只负责命令触发和状态显示，不直接修改 Entity 几何。

```text
Menu / Toolbar / Shortcut
          ↓
CadActionManager
          ↓
Action / CadToolManager / CadTool
          ↓
ResolvePoint / WorkPlane / Tool State / Preview
          ↓
Transaction / Document / History
```

初版不显示永久 Command Input；当前 Tool 的操作提示显示在底部状态栏，参数显示在状态栏正上方的固定参数条。

## 2. Tool 生命周期

统一生命周期：

`Idle → Activate → Drawing/WaitForSelect → Preview → Commit Stage → Next/Complete → Neutral`

规则：

- 激活新 Tool 前取消当前 Tool；
- Activate 时清理 Preselection、Subobject 和 Grip；
- 每个 Tool 独占自己的 Transient/Preview 生命周期；
- `Backspace` = StepBack；
- `Enter/Space` 按统一 ToolManager 规则接受当前有效阶段或 Finish；
- 右键在可完成时 Finish，否则 Cancel；
- Complete/Cancel 后必须恢复 Neutral 状态；
- Point Step 不复用 stale/off-viewport 坐标。

### Esc

`Esc` 是应用级 CAD 取消动作，不只是某个 Tool 的局部键处理：

```text
Cancel active Tool
      ↓
Clear Entity Selection
Clear Subobject Selection
Clear Preselection
      ↓
Clear Snap / Tracking
      ↓
Return focus to Viewport
```

无论焦点在 Viewport、Tool 参数 TextBox 还是主窗口普通控件，最终语义必须一致。清空选择是幂等操作，因此不同输入层不得因为重复清理而留下不一致状态。

## 3. 工作平面

`WorkPlane` 是 Tool 点输入的正式几何平面，不是 UI 辅助线。

XY / YZ / XZ 切换必须统一经过 `CadToolManager.TryChangeDrawingPlane()`，不能由不同按钮/快捷键各自实现一套回退逻辑。

规则：

- 没有临时固定施工平面时，立即应用用户请求的 preset；
- 当前 Tool 如果处于固定临时施工平面阶段，Core 先通过 `StepBackCurrent()` 回退到最近的可切换阶段，再设置 preset；
- User Plane Lock 或 fixed Grip Plane 仍然阻止切换；
- 平面切换后清理 Snap / Tracking；
- Active Tool 收到 `OnWorkPlaneChanged()`，并使用最后有效指针重建 Preview；
- 缓存 Normal/XAxis/YAxis 的平面型 Tool 必须同步更新缓存；
- Tool 完成或取消后必须 `EndToolPlane()`；
- 工作平面不得在 Tool 结束后残留 Active 状态。

不允许在一个已经固定高度轴/局部轴的阶段中直接旋转施工平面，否则已确认点和后续输入会落在不同坐标框架中。

## 4. Tool 参数与实时值

`CadTool.ParameterPanel` 是参数 UI 唯一来源。

双向链路：

```text
Editor Commit → tool.TrySetParameter() → Tool state → Preview
Preview/Tool update → measurable actual value → Editor display
```

要求：

- UI 不直接写 Tool 私有字段；
- Preview 真实尺寸优先于鼠标输入的临时文本；
- 当前获得焦点的 TextBox/ComboBox/CheckBox 不得被自动刷新重建；
- 参数条固定占位，Tool 切换/参数显隐不能改变 Viewport 高度；
- ToolUpdated/ToolChanged 后 UI 从当前 Tool 重建，不保存第二份参数状态。

## 5. 点解析

统一点解析链：

`Screen → View Ray / Effective WorkPlane → Object Snap → Tracking → Axis/Angle/Length Constraint → Final World Point`

Tool 不重复实现 XY 投影、Polar 或锁定优先级。

## 6. Snap / Selection

Entity 提供语义 Snap；SnapManager 负责候选收集、屏幕容差、深度、类型优先级和临时模式。

Selection、Preselection、SubobjectSelection 相互独立：

- 左→右框选 = Window；
- 右→左框选 = Crossing；
- Hidden / Locked / Non-selectable 不进入 Formal Selection；
- 激活普通绘图 Tool 时 Grip 清空；
- Tool 结束后按当前 Selection 恢复 Grip；
- `Esc` 是“显式清空正式交互状态”，Entity/Subobject/Preselection 都必须清空。

## 7. Grip

Grip 编辑采用事务式 Preview：

`capture original → duplicate preview → suppress real presentation → MoveGrip(preview) → Preview.Update → accept → apply once → one History entry → clear preview → restore presentation`

PointerMove 不修改持久 Entity。

## 8. Preview / Commit

Preview 永远不属于 Document、Selection 或 History。

创建类 Commit 边界必须遵循：

`Validate → Clear/Suppress Preview → Persistent mutation → Complete Tool → Install History → Redraw`

如果正式 mutation 在 Tool 仍可继续的情况下失败，可以恢复最后有效 Preview；如果 Tool 已完成，则不得制造“持久实体 + 旧 Preview”共存的假状态。

鼠标移动产生的中间几何是临时样本。`ArgumentException`、`InvalidOperationException`、`ArithmeticException` 和预览阶段的 `OcctException` 可以清理当前 Preview/Snap/Tracking 并保持 Tool 激活；最终 Commit 失败必须向上报告，不能伪装成成功。

## 9. Entity / Tool / Action 一致性

产品功能面必须保持：

`Entity Registry ↔ Tool Registry ↔ Action Registry ↔ Menu/Toolbar ↔ Model Tree`

一致。

禁止在 Model Tree 或 UI 中维护第二套过时 Entity 白名单。内部 `Path` 等仅用于 Feature 持久化的 Entity 不作为普通用户模型节点展示。

初版不恢复 Move / Copy / Rotate / Scale / Mirror / Array / Trim / Extend / Fillet / Chamfer 等编辑命令；模型树也不提供 Delete 入口。

## 10. Property / Layer

Entity Property 使用：

`Capture → Validate → Apply → Presentation/Dependency Refresh → History`

Layer 同样走 Core-owned transaction。直接修改 Color / LineStyle / LineWidth 自动关闭对应 ByLayer；ByLayer 切换也进入 History。

## 11. Prompt / Error

- 当前 Tool Prompt 显示在底部状态栏；
- 空闲状态保持空白，不显示“命令：就绪”或 `Ready`；
- 可恢复 Preview 失败不弹致命错误窗口；
- Property/Layer/文件操作失败明确反馈；
- OOM、StackOverflow、AccessViolation 等 Fatal failure 不做伪恢复。
