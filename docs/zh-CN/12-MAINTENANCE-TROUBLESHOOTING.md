# 12 维护与故障排查

## 1. 维护目标

维护 OCCAD 时优先定位“哪一层的不变量被破坏”，而不是先在 UI 上打补丁。常见问题通常落在：

- Tool 生命周期。
- Preview/Transient ownership。
- Document/Viewer presentation 同步。
- Selection/Snap/Grip cache/marker。
- Transaction/History rollback。
- Avalonia adapter 生命周期。
- Engine recreation。

## 2. 推荐排查顺序

出现交互 Bug 时按以下顺序检查：

1. **Model state 是否正确？**
2. **History state 是否正确？**
3. **Active Tool / Stage 是否正确？**
4. **Preview/Snap/Tracking/GripDrag 是否残留？**
5. **ViewerObject 是否与 Entity 一一对应？**
6. **Selection/Grip 是否与正式状态一致？**
7. **UI 是否只是显示错，而 Core 实际正确？**

不要反过来先改颜色、刷新或 UI event handler 掩盖 Core 状态问题。

## 3. 预览残留 / Ghost Preview

症状：

- 完成绘图后还留着最后一帧 preview。
- Cancel 后场景仍有临时形状。
- Undo 后出现旧 preview。

检查：

- `CadPreviewManager.HasTransient`。
- `CadTransientScene.CurrentToolOwner`。
- Tool `OnCanceled` / `OnDeactivated` 是否全部执行。
- Preview native handle delete 失败后是否仍被 owner 持有。
- replacement preview 是否恢复 source。

不要用“强制全场景 redraw”代替 ownership 修复。

## 4. Snap marker / candidate 残留

检查：

- `Snap.Current`。
- `Snap.Candidates`。
- `CurrentCandidateIndex`。
- `TemporaryModes`。
- marker handle。
- Document geometry change 后 cache 是否 invalidated。
- Tool cancel/Undo 是否调用统一 neutral cleanup。

如果 marker delete 失败，重点确认 handle 没有被置空后丢失。

## 5. Grip 残留 / 位置错误

检查：

- `Grips.Entities` 是否等于正式 Selection。
- geometry/property change 后是否触发 grip refresh。
- `_markers` 与 `_grips` count 是否一致。
- retired marker 是否 hidden/non-selectable。
- GripDrag marker 是否清理。

若模型几何正确但 Grip 错，优先查 refresh/native marker，不要重新修改 Entity geometry。

## 6. Undo/Redo 后模型与显示不一致

先分别验证：

- Entity geometry/placement。
- History `CurrentStateId`。
- `ViewerObject` 是否存在于 engine。
- Document viewer-object reverse mapping。
- engine object count。

如果模型正确、显示错误：查 strict Entity/Layer → Document → native propagation。

如果模型本身错误：查 History entry snapshot/restore 和 rollback。

不要在 Undo 后调用全量 `Regenerate()` 来掩盖错误 history snapshot，除非问题确实只属于 presentation rebuild。

## 7. “操作成功但 API 报失败”

典型原因是 post-state observer 抛异常。

检查是否存在：

```csharp
Changed?.Invoke(...)
```

并判断事件是否本应是 notification-only。

如果状态已经 authoritative，改为逐 observer 隔离；如果该事件用于 strict internal synchronization，则不能吞异常，而应保留 rollback。

## 8. “模型变了但 View 没变”

这通常意味着错误地把 strict internal propagation 当成普通 notification 吞掉了。

重点检查：

- Entity `StateChanged` → Document。
- Layer `StateChanged` → LayerManager → Document。
- presentation rebuild/apply appearance 是否抛异常。
- caller 是否正确 rollback。

## 9. Native object count 持续增长

常见来源：

- Preview delete 失败后 handle 丢失。
- Grip/Snap/Subobject marker 重建中途失败。
- temporary edge/subshape 未 delete。
- engine recreation 旧对象未清理。
- Workspace Dispose 被前一个异常截断。

排查时记录每次用户动作前后：

- persistent Entity count。
- engine object count。
- transient channel state。

增长若与操作次数线性相关，优先查 ownership。

## 10. 第一次显示不刷新 / Engine recreation

检查：

- engine initialized 时机。
- `Workspace.AttachEngine()` 顺序。
- Document persistent presentation 是否 rebuild。
- Selection/Subobject/Snap/Tracking/Preview/Grip 是否 reattach。
- Avalonia viewport lifecycle 是否在 engine 重建后重新同步 controller。

不要在业务层到处插入随机 `InvalidateVisual` / redraw；优先修 attach/rebuild 时序。

## 11. WorkPlane / S-F-T 异常

检查：

- UserPlane / ToolPlane / GripPlane。
- fixed/locked 状态。
- Tool 是否允许当前阶段切平面。
- 切换后 Snap/Tracking 是否清理。
- 当前 pointer 是否重新 resolve。

WorkPlane 是几何约束状态，不应靠 UI 文本作为真相。

## 12. Selection 不一致

分别检查：

- Core `Selection.Selected` / `Primary`。
- native viewer selection。
- `Grips.Entities`。
- Subobject selection。
- layer lock/visibility 后 validity refresh。

正式 Selection 是 authoritative；native highlight 是 presentation side effect。

## 13. Property 修改后异常

检查：

- property 是否走 Core transaction。
- before/after 是否 no-op。
- geometry property 是否正确 rebuild presentation。
- selected Entity 的 Grip 是否刷新。
- 一次 property edit 是否只产生一个 Undo。

PropertyGrid 不应自己做 rollback/history。

## 14. Dispose / 退出异常

Workspace/UI teardown 必须 best-effort 处理 recoverable cleanup failure。

检查：

- 是否先退订内部 observer。
- 每个 cleanup 是否独立 try。
- fatal exception 是否被错误吞掉。
- transient channel registration Dispose 是否会截断后续事件退订。

## 15. 大文件维护策略

遇到 20KB+ Controller/Manager 不应机械拆文件。

只有满足以下条件时拆：

- 能定义清晰 owner。
- 有稳定输入/输出 contract。
- 能降低 native/resource ownership 模糊度。
- 有测试保护。

典型可抽对象：

- marker owner。
- scheduler。
- command/session coordinator。
- pure geometry service。

不要抽“Helper”只为了减少行数。

## 16. 文档与代码漂移

每次维护发现文档与源码不一致：

1. 先确定当前正确 contract。
2. 修实现或修文档。
3. 若属于长期架构取舍，补 ADR。
4. 不保留两套互相矛盾的说明。

## 17. 何时停止重构

当核心链满足：

```text
Create
→ Preview
→ Exact/Pointer input
→ Commit
→ Select
→ Property
→ Grip
→ Transform
→ Undo/Redo
→ Cancel
```

且 build/回归稳定时，停止横向“优化”。之后只针对：

- 可复现 Bug。
- 明确性能问题。
- 有业务需求的扩展。
- 能量化收益的结构债务。

稳定 CAD 核心比持续重构更重要。
