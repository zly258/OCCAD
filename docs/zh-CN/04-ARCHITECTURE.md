# 04 系统架构

## 分层
```text
Presentation — OCCAD.Avalonia
        ↓
Application & Interaction — OCCAD.Core
        ↓
Domain — OCCAD.Core
        ↓
OcctNet / OcctNet.Avalonia / OcctCSharpBridge / OCCT
```
依赖固定为 `Avalonia → Core → OcctNet`。

## Ownership
`CadWorkspace` 是 Session composition root。Document 是持久 Entity 唯一 owner 和最终 appearance resolver。Entity 负责 Id/Type、合法 Geometry、Layer、Appearance override + ByLayer、Placement、Snap/Grip、Duplicate/Restore/Transform、Persistence。Viewer object 是派生 presentation。

## Property / Layer
`CadPropertyCatalog` / `CadPropertyDescriptor` / `CadValueDescriptor` 定义属性语义，`TypeDescriptor` 只是 CLR adapter。`CadPropertyTransaction` 原子修改 Entity。Layer 修改集中到 `CadWorkspace`；Avalonia 不实现第二套 rollback/history。

## Tool
Action 是稳定瞬时命令入口。Tool 拥有 Stage、Prompt、InputKind、InteractionPolicy、Precision、WorkPlane、Parameters、Preview、Finish/Cancel/StepBack。`CadToolContext` 不在模型修改 API 里偷偷做 Preview cleanup。

## Transient / Redraw
Selection、Preselection、Subobject、Grip、Snap、Tracking、Preview、selection rectangle、HUD 各自有独立生命周期。模型/History 成功后或 Tool Cancel/Deactivate 时清 transient。Bridge 已安排 redraw 时 OCCAD 不重复 redraw。

`OCCTBIM-Source` 用于核对职责边界和行为，不复制其 Qt/Singleton 结构。
