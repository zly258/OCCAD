# 04 Architecture

## 1. Goal

OCCAD is not an OCCT API wrapper. It is a maintainable and extensible CAD product core. The primary rule is: **domain and interaction state have explicit owners; native viewer objects are derived presentation; UI code does not duplicate business rules.**

## 2. Layers and dependency direction

```text
┌──────────────────────────────────────────────┐
│ OCCAD.Avalonia                               │
│ Shell / Toolbar / Panels / Viewport Adapter │
└──────────────────────┬───────────────────────┘
                       │ calls / observes
┌──────────────────────▼───────────────────────┐
│ OCCAD.Core                                   │
│ Workspace / Document / Entity / Tool         │
│ Selection / Snap / Grip / History / Layer    │
│ Geometry / Persistence / Actions             │
└──────────────────────┬───────────────────────┘
                       │ native geometry/view
┌──────────────────────▼───────────────────────┐
│ OcctNet / OcctCSharpBridge / OCCT            │
└──────────────────────────────────────────────┘
```

Allowed dependency direction:

`OCCAD.Avalonia → OCCAD.Core → OcctNet`

Forbidden:

- Core referencing Avalonia;
- Entity/Document depending on UI controls;
- Avalonia maintaining a second Document, Selection, History, Layer, or Tool state machine;
- reflection dispatch, global singletons, or service locators used to bypass explicit dependencies.

## 3. Composition root

`CadWorkspace` is the composition root for one CAD session. It coordinates:

- `CadDocument`
- `CadLayerManager`
- `CadSelectionManager`
- `CadSubobjectSelectionManager`
- `CadPreselectionManager`
- `CadWorkPlane`
- `CadDraftingSettings`
- `CadSnapManager`
- `CadTrackingManager`
- `CadPrecisionInputManager`
- `CadPreviewManager`
- `CadGripManager`
- `CadTransientScene`
- `CadHistory`
- `CadToolManager`
- `CadActionManager`
- `CadWorkspaceEvents`

Business behavior belongs in the responsible Core owner before an Avalonia adapter is added.

## 4. Authoritative state and derived presentation

### Authoritative state

- Document entity membership;
- Entity geometry, placement, layer, appearance, and metadata;
- layer collection and current layer;
- History state and Undo/Redo stacks;
- formal Entity/Subobject selection;
- Tool stage, parameters, precision, and work-plane semantics.

### Derived presentation

- `ViewerObject`
- selection highlight
- snap marker
- grip marker
- tracking guide
- preview presentation
- preselection highlight
- selection rectangle
- subobject marker
- Toolbar / Panel / Dialog / Status Strip text and control state

The initial shell does not display a permanent Command Line or Dynamic HUD, but related input/precision infrastructure still belongs to presentation/interaction adaptation rather than authoritative Document data.

A post-state presentation observer cannot retroactively turn an established operation into failure. Strict internal synchronization that keeps model and native presentation coherent may reject and roll back a mutation.

## 5. Three event classes

### Pre-state / veto

Examples: `Changing`.

- runs before mutation;
- may validate or reject the operation;
- exceptions propagate.

### Strict internal state propagation

Examples: internal Entity/Layer `StateChanged`.

- used by Document, LayerManager, native presentation, and mandatory internal synchronization;
- failures propagate;
- the mutation owner rolls back;
- not a normal UI notification surface.

### Post-state notification

Examples: public `Changed`, `ToolChanged`, `History.Changed`, `Snap.CurrentChanged`, `Grip.HotChanged`.

- state is already authoritative;
- observers are invoked independently;
- recoverable observer failures are diagnostic only and may not invalidate the operation or starve later observers;
- fatal failures such as `OutOfMemoryException`, `StackOverflowException`, and `AccessViolationException` are not swallowed.

Every new event must explicitly fit one of these classes.

## 6. Document and Entity

`CadDocument` is the sole owner of persistent Entity membership and is responsible for:

- add/remove;
- ViewerObject mapping;
- engine attach/detach;
- presentation build/rebuild/delete;
- effective appearance resolution;
- strict Entity propagation;
- layer-driven appearance updates;
- document change-set aggregation.

`CadEntity` owns:

- identity/type;
- valid geometry;
- placement;
- layer/appearance overrides;
- duplicate/restore and geometry-snapshot semantics;
- snap/grip geometry semantics;
- persistence geometry.

Entity does not own UI or History and does not construct Toolbar/Panel controls.

## 7. Tool, Action, and Shell

- `Action` is the stable stateless command entry point;
- `Tool` is a stateful staged interaction command;
- `CadToolManager` owns Tool lifecycle;
- the grouped Toolbar, panels, and viewport input adapters call or observe the same Core Action/Tool state;
- Circle/Arc/Ellipse/RegularPolygon variants activate one Tool with different initial parameters instead of duplicating geometry implementations;
- the initial shell does not expose a permanent Command Line, Floating Tool Panel, or large Ribbon.

Typical lifecycle:

```text
Activate
  ↓
Begin Tool transient owner
  ↓
Pointer / parameter / precision / staged state
  ↓
Preview
  ↓
Commit model mutation
  ↓
Complete Tool
  ↓
Install History
  ↓
Return Neutral
```

Cancel and deactivate release Tool-owned transient state and satisfy the neutral contract.

## 8. Transaction and History

Use operation-specific lightweight history for high-frequency edits and explicit `CadTransaction` snapshots for compound batch work.

Invariants:

1. History advances only after the model operation is authoritative;
2. if history is not installed, failed mutations roll back;
3. if history is installed, notification failure cannot roll back model state;
4. Tool completion is part of create/feature commit semantics;
5. when an outer transaction owns history, inner lightweight mutations do not install nested Undo records;
6. Preview never enters History.

See [10-TRANSACTION-RESOURCE-CONTRACT.md](10-TRANSACTION-RESOURCE-CONTRACT.md).

## 9. Transient and native ownership

`CadTransientScene` coordinates ToolPreview, Snap, Tracking, GripDrag, Preselection, SelectionWindow, and SelectionMarkers.

Rules:

- Tool-lifetime cleanup is strict;
- Workspace/UI disposal is best-effort for recoverable native cleanup failures;
- native handles are not forgotten until deletion is confirmed;
- on recoverable deletion failure, prefer hidden + non-selectable and retain ownership for retry;
- Preview and markers never enter Document or History.

## 10. Selection, Snap, and Grip

- Selection owns formal Entity selection;
- Subobject Selection is separate;
- Preselection represents hover only;
- Snap owns candidate discovery, priority, screen/depth ordering, hysteresis, cycling, temporary modes, marker, cache, and invalidation;
- Entity declares snap/grip geometry semantics;
- Grip pointer movement edits duplicate/preview state; commit edits the formal Entity.

## 11. Property and Layer

- property descriptors and value semantics belong to Core;
- `CadPropertyTransaction` / `CadTransaction.ApplyEntities` own formal property mutation;
- PropertyGrid Controller constructs editors, converts input, and refreshes UI only;
- LayerManager is the sole owner of layer collection/current-layer state;
- ByLayer effective appearance is resolved through the Core/Document path;
- editable planar `Normal` is exposed through descriptor metadata while mutation still follows normal Entity/history/presentation rules.

## 12. Persistence and Exchange

- Entity Registry IDs are part of the persistence protocol and are stable identifiers;
- geometry/placement/appearance data is separate from transient presentation;
- Feature Entities may use persistence-only registration while their Tools own context-sensitive creation;
- Exchange service is the external CAD-format boundary and third-party format state does not become authoritative UI state directly;
- breaking schema changes require an ADR and an explicit compatibility strategy.

## 13. Avalonia boundary

Avalonia may:

- call Core actions/tools/APIs;
- observe Core notifications;
- own viewport input adaptation, layout, controls, dialogs, and localization display;
- convert user input into Core property/selection/tool parameters.

Avalonia must not:

- implement parallel Entity transactions or Undo/Redo;
- bypass Selection managers for formal selection;
- duplicate Tool geometry algorithms in controllers;
- scatter native marker ownership across unowned lists;
- use localized display text as stable Action/Tool/Entity identifiers.

## 14. Extension decision checklist

Before implementing a feature answer:

1. Is it Domain, Interaction, or Presentation state?
2. Who is the single owner?
3. Does it require Undo/Redo?
4. Does it enter Document?
5. Is it transient?
6. Who owns each native handle and when is it deleted?
7. What state is authoritative if failure occurs?
8. Is each event veto, strict propagation, or notification?
9. Does persistence ID/schema change?
10. What is the minimum local build plus manual/native validation?

If these answers are unclear, implementation should not begin.

## 15. ADR-required changes

Create an ADR for changes that:

- add projects/assemblies or alter dependency direction;
- replace transaction/history semantics;
- change Tool lifecycle or command-session semantics;
- change Document/Entity ownership;
- introduce global services, plugin containers, threading, or native async scheduling;
- change persistence compatibility strategy.

Use `docs/adr/0000-template.md`.
