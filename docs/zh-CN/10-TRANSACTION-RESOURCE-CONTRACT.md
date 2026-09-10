# 10 事务、事件与 Native 资源契约

## 1. 为什么需要单独规范

CAD 的难点不是“把几何算出来”，而是保证模型、History、Tool、Preview、Selection、Snap/Grip 和 Native Viewer 在成功与失败路径下仍然一致。本文件定义这些跨模块不变量。

## 2. Authoritative State 原则

任何操作必须明确哪一步以后状态成为 authoritative。

典型顺序：

```text
validate
  ↓
mutate model / build required presentation
  ↓
complete Tool-owned lifecycle obligations
  ↓
install History
  ↓
publish notification
```

一旦 History 已经成功安装，后续普通 notification 失败不得反向 rollback 模型。

## 3. Lightweight operation

适用于：

- Move
- Rotate
- Scale
- Mirror
- Property edit
- Grip edit
- Create/Delete small entity sets

规则：

1. Capture before state。
2. Apply mutation。
3. Capture after state。
4. 判断 no-op。
5. 必要时完成 Tool contract。
6. Install history。
7. History 未安装且失败时 rollback。

高频操作不应使用全 Workspace snapshot。

## 4. Explicit `CadTransaction`

适用于批量、复合、多步修改。

外层 transaction：

- capture workspace snapshot；
- suspend inner history recording；
- 可聚合 Document change-set / display batch；
- `Commit()` 后只安装一个 Undo；
- Dispose 未 Commit 时恢复 snapshot。

禁止嵌套显式 transaction。

在外层 transaction 内允许 lightweight `Execute()` / entity mutation，但内部不得创建嵌套 history entry。

## 5. Create commit

Tool 创建实体时，完成顺序属于业务契约：

1. Preview 已有最后有效形态。
2. 持久 Entity 加入 Document。
3. Tool completion / transient release 成功。
4. 安装 Create History。

如果第 2 步成功但第 3 步失败，且 History 尚未安装，则必须移除刚创建的 Entity。

## 6. Transform / Property / Grip commit

变换类操作应使用 lightweight entity snapshot。

- before：每个目标 Entity snapshot。
- mutate：真实 Entity。
- complete：如属于 Tool commit，则完成 Tool。
- after：最终 state。
- history：一个用户动作一个 entry。

任何目标中途失败时，已修改目标按 reverse order 恢复。

## 7. No-op contract

以下操作不得污染 History：

- Move displacement = 0。
- Rotate = 0° / 360° 等价。
- Scale = 1。
- Property 值未变化。
- Layer state 未变化。
- Snapshot 前后 StateEquals / GeometryEquals。

No-op 同样不得错误地把文档标记为 Modified。

## 8. History contract

`CadHistory` 的核心状态是：

- Undo stack。
- Redo stack。
- `CurrentStateId`。

规则：

- entry.Redo/Undo 失败时，History 指针不得先移动。
- 操作成功后再移动 stack/state id。
- `History.Changed` 是 post-state notification。
- observer 失败不能让 Undo/Redo API 报“模型没变”。

## 9. Modified state

`CadWorkspace` 使用 saved history state ID 与当前 history state 比较。

- `MarkSaved()` 记录当前 state ID。
- Undo 回到 saved state → `IsModified=false`。
- Redo 离开 saved state → `IsModified=true`。
- notification failure 不改变 modified truth。

## 10. Event contract

### 10.1 Strict event

用于：

- pre-state validation/veto。
- internal model→document→native required synchronization。

异常必须传播。

### 10.2 Notification event

用于：

- UI refresh。
- HUD/status update。
- external integration observer。

规则：

```text
state already authoritative
for each observer:
    try invoke
    catch recoverable -> diagnostic only
```

不得让一个 observer 阻塞后续 observer。

## 11. Recoverable 与 Fatal

Presentation/cleanup 层可以吞 recoverable failure，但必须保留诊断和 ownership。

至少以下视为 fatal：

- `OutOfMemoryException`
- `StackOverflowException`
- `AccessViolationException`

`AggregateException` / `InnerException` 必须递归分类，不能因为外层是 `InvalidOperationException` 就把内部 `AccessViolationException` 当 recoverable。

## 12. Preview contract

Preview：

- 不属于 Document。
- 不进入 Selection。
- 不进入 History。
- Tool owns its semantic lifetime。
- clear failure 不得丢 native handle ownership。
- replacement preview 必须能恢复 source visibility/presentation。

Preview commit 的核心目标是：**没有中间一帧“preview 已消失、真实模型又没提交”的错误状态，也不能留下 ghost preview。**

## 13. `CadTransientScene`

典型 channel：

- ToolPreview
- Snap
- Tracking
- GripDrag
- Preselection
- SelectionWindow
- SelectionMarkers

### Tool lifetime

严格清理。

`ClearOwner(owner)` 必须：

- 尝试所有匹配 channel。
- 无论单个 clear 是否失败，都释放 owner/session bookkeeping。
- 最后再报告 cleanup failure。

### Workspace lifetime

`ClearAll()` 使用 best-effort 对 recoverable cleanup failure，不让一个 transient 通道截断 Workspace teardown。

## 14. Native handle ownership

每个 native object 必须遵守：

```text
create → owner stores handle immediately
      → configure
      → use
      → delete confirmed
      → forget handle
```

错误顺序：

```text
create → configure throws → local handle lost
```

或：

```text
delete throws → list.Clear() → handle lost
```

正确策略：

- 临时创建列表先收集 handle。
- 配置失败时 retire。
- delete batch 失败再 individual retry。
- 仍失败：hidden + non-selectable。
- handle 放入 retired list，后续 cleanup retry。

## 15. Engine recreation

Engine 重建时必须明确：

- persistent Document presentation 重建。
- old engine stale presentation 尽力清理。
- transient manager 重新 attach。
- 不把旧 engine handle 当成新 engine handle。
- formal state（Document/Selection 等）不能因为 View 重建丢失。

## 16. Undo/Redo neutralization

在 Undo/Redo 之前，Workspace 必须先退出交互命令并清理 transient：

- ActiveTool → null。
- Tool owner → 0。
- Preview → clear。
- Snap current/candidates/temporary modes → clear/reset。
- Tracking → clear。
- GripDrag → clear。
- Preselection → clear。
- formal selection/grips 按当前产品 contract 处理。
- pointer observation 清理。

然后才能移动 History。

## 17. Dispose contract

Dispose 是 teardown，不是正常业务 transaction。

- 先退订内部 observer，避免 teardown 期间重入。
- 每个 cleanup step 独立尝试。
- recoverable failure 记录后继续。
- fatal failure 不静默吞掉。
- 最终清空 Engine/pointer references。

## 18. Code review 问题

跨 transaction/native 生命周期的改动必须能回答：

- authoritative point 在哪？
- History 何时安装？
- 失败时 rollback 谁负责？
- observer 能否 veto？
- native handle 是否可能丢？
- Tool owner 是否一定归零？
- Undo/Redo 是否只产生一个用户语义步骤？
- Dispose 时单点失败会不会截断整个 teardown？
