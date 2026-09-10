# 04 系统架构

## 分层

```text
Presentation — OCCAD.Avalonia
        ↓
Application Core — CadApplicationCore
  Settings / Command Session / Document Session
        ↓
CAD Workspace — OCCAD.Core
  Document / Entity / Layer / Action / Tool / Selection / Snap / Grip / Preview / History
        ↓
OcctNet / OcctNet.Avalonia / OcctCSharpBridge / OCCT
```

依赖固定为 `Avalonia → Core → OcctNet`。Avalonia 不创建平行的 CAD Session 或状态容器。

## Ownership

`CadApplicationCore` 是应用级 composition root，唯一持有 `CadSettingsStore`、`CadCommandManager`、`CadDocumentSession` 与一个 `CadWorkspace`。`CadWorkspace` 是 CAD 模型和交互服务的 composition root，而不是全局服务定位器。

Document 是持久 Entity 唯一 owner 和最终 appearance resolver。Entity 负责稳定 Id/Type、合法 Geometry、`LayerId`、Appearance override + ByLayer、Placement、Snap/Grip 语义、Duplicate/Restore/Transform 与 Persistence。Viewer object 是派生 presentation。

## Property / Layer

`CadPropertyCatalog` / `CadPropertyDescriptor` / `CadValueDescriptor` 定义属性语义，`TypeDescriptor` 只是 CLR adapter。`CadPropertyTransaction` 原子修改 Entity。Layer 修改集中到 Core transaction/history；Avalonia 不实现第二套 rollback/history。

## Command / Action / Tool

`CadCommandManager` 是应用拥有的一次 command-line session，不使用静态 Workspace 查找表。Action 是稳定用户操作入口；Tool 拥有 Stage、Prompt、InputKind、InteractionPolicy、Precision、WorkPlane、Parameters、Preview、Finish/Cancel/StepBack。前端只提交命令和输入，不持有第二套命令历史或 Tool 状态。

## Transient / Redraw

Selection、Preselection、Subobject、Grip、Snap、Tracking、Preview 与 Window/Crossing selection rectangle 的 native presentation 都由 Core owner 管理，并统一参加 `CadTransientScene` 生命周期。Tool 完成、取消、切换或失败后必须恢复 neutral state；Viewer/presentation 永远不是模型权威数据。

Bridge 已安排 redraw 时 OCCAD 不重复 redraw。`OCCTBIM-Source/release-1.0` 只用于核对职责边界和行为，不复制其 Qt、Singleton 或数据库结构。
