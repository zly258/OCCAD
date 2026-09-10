# 04 Architecture

## 1. Goal

OCCAD is not an OCCT API wrapper. It is a maintainable and extensible CAD product core. The primary rule is: **domain and interaction state have explicit owners; native viewer objects are derived presentation; UI code does not duplicate business rules.**

The architecture prioritizes:

1. Document/Entity as model truth;
2. ViewerObject as derived presentation;
3. a Tool lifecycle that can complete, cancel, and step back safely;
4. atomic model mutation and Undo/Redo;
5. explicit transient ownership for Preview/Snap/Grip/Tracking;
6. UI adapters rather than a second CAD kernel.

## 2. Layers and dependency direction

```text
┌───────────────────────────────────────────────────────────┐
│ OCCAD.Avalonia                                            │
│ Shell / Menu / Toolbar / Panels / Dialogs / Viewport     │
│ Input Adapter / Localization / Settings Presentation      │
└───────────────────────────┬───────────────────────────────┘
                            │ calls / observes
┌───────────────────────────▼───────────────────────────────┐
│ OCCAD.Core                                                │
│ Workspace / Document / Entity / Layer                     │
│ Action / Command / Tool / Transaction / History           │
│ Selection / Snap / Grip / WorkPlane / Precision           │
│ Preview / Tracking / Geometry / Persistence / Exchange    │
└───────────────────────────┬───────────────────────────────┘
                            │ managed/native bridge
┌───────────────────────────▼───────────────────────────────┐
│ OcctNet / OcctCSharpBridge                                │
└───────────────────────────┬───────────────────────────────┘
                            │ OCCT API
┌───────────────────────────▼───────────────────────────────┐
│ Open CASCADE Technology                                   │
└───────────────────────────────────────────────────────────┘
```

Allowed dependency direction:

`OCCAD.Avalonia → OCCAD.Core → OcctNet → OcctCSharpBridge/OCCT`

Forbidden:

- Core referencing Avalonia;
- Entity/Document depending on UI controls;
- Avalonia maintaining a second Document, Selection, History, Layer, or Tool state machine;
- reflection dispatch, global singletons, or service locators used to bypass explicit dependencies;
- UI controllers owning native transient handles outside the established owners;
- localized text used as a business identifier.

## 3. Composition root

`CadWorkspace` is the composition root for one CAD session and coordinates:

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

One `CadWorkspace` represents one CAD session. Workspace owners collaborate explicitly but do not share a formal Document through global static state.

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

## 7. Tool, Action, Command, and Shell

Responsibilities are intentionally separate:

- `CadAction`: stable stateless command entry point;
- `CadCommandDescriptor` / `CadCommandManager`: command metadata and text-input adaptation;
- `CadTool`: stateful staged interaction state machine;
- Shell: maps Menu/Toolbar/Input to the same Core Action/Tool system.

Circle/Arc/Ellipse/RegularPolygon variants activate one Tool with different initial parameters rather than duplicating geometry implementations.

Typical lifecycle:

```text
Action.Execute
  ↓
CadToolManager.Activate
  ↓
CadTransientScene.BeginToolSession
  ↓
Tool.Activate
  ↓
Pointer / Selection / Parameter / Exact Input
  ↓
Preview
  ↓
Commit mutation
  ↓
Tool completion / Deactivate
  ↓
Install History
  ↓
Reset neutral state
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
6. Preview never enters History;
7. no-op operations do not create History entries or incorrectly change Modified state.

### Create commit

```text
Build candidate Entity
→ Add to Document
→ complete Tool / release transient
→ install AddEntities history
```

If Tool completion fails before History is installed, the created Entity must be removed again.

### Edit commit

```text
Capture before state
→ mutate formal Entity
→ complete Tool
→ capture after state
→ install history
```

If mutation or cleanup fails before History is installed, restore the before state.

See [10-TRANSACTION-RESOURCE-CONTRACT.md](10-TRANSACTION-RESOURCE-CONTRACT.md).

## 9. Transient and native ownership

`CadTransientScene` coordinates:

- ToolPreview;
- Snap;
- Tracking;
- GripDrag;
- Preselection;
- SelectionWindow;
- SelectionMarkers.

Rules:

- Tool-lifetime cleanup is strict;
- Workspace/UI disposal is best-effort for recoverable native cleanup failures;
- native handles are not forgotten until deletion is confirmed;
- on recoverable deletion failure, prefer hidden + non-selectable and retain ownership for retry;
- Preview and markers never enter Document or History;
- a Tool owner is released only after its channels no longer retain state.

If owner identity were dropped before native cleanup succeeded, a visible ghost could survive without a lifecycle owner and a later Tool session could start on top of it. The current design keeps residual state attached to the original session until cleanup succeeds.

## 10. Selection, Snap, and Grip

### Selection

- Entity Selection owns formal entity selection;
- Subobject Selection is separate;
- Preselection is hover only;
- Window/Crossing is converted by the viewport adapter into Core selection operations;
- viewer highlighting is not authoritative selection state.

### Snap

Snap owns candidate discovery, priority, screen/depth ordering, hysteresis, cycling, temporary modes, markers, cache, and invalidation. Entities declare only their snap geometry semantics.

### Grip

Grip pointer movement changes duplicate/preview state. Only final confirmation mutates the formal Entity.

The visual convention is a filled rectangular normal Grip and circular hot/drag markers.

## 11. WorkPlane, Drafting, and Precision

`CadWorkPlane` owns the drafting plane; `CadDraftingSettings` owns ORTHO/POLAR and related drafting state; `CadPrecisionInputManager` owns length, angle, factor, and offset-point exact input.

A Tool may request a temporary ToolPlane but must not create a parallel permanent work-plane state.

When XY/YZ/XZ changes:

1. ToolManager checks whether the current stage permits a change;
2. if required, the Tool steps back to a compatible stage;
3. WorkPlane changes;
4. Snap/Tracking clear;
5. Preview is resolved again on the new plane.

## 12. Property and Layer

- property descriptors and value semantics belong to Core;
- `CadPropertyTransaction` / `CadTransaction.ApplyEntities` own formal property mutation;
- PropertyGrid Controller constructs editors, converts input, and refreshes UI only;
- LayerManager is the sole owner of layer collection/current-layer state;
- ByLayer effective appearance is resolved through the Core/Document path;
- editable planar `Normal` is exposed through descriptor metadata while mutation still follows normal Entity/history/presentation rules.

## 13. Persistence and Exchange

- Entity Registry IDs are part of the persistence protocol and are stable identifiers;
- geometry/placement/appearance data is separate from transient presentation;
- Feature Entities may use persistence-only registration while their Tools own context-sensitive creation;
- Exchange service is the external CAD-format boundary and third-party format state does not become authoritative UI state directly;
- breaking schema changes require an ADR and an explicit compatibility strategy.

The initial release primarily claims OCCAD document persistence. External format directions enter the formal feature matrix only after each direction is validated.

## 14. Avalonia boundary

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

## 15. MainWindow responsibility split

MainWindow uses partial files to separate shell concerns:

- `MainWindow.cs`: base window and primary composition;
- `MainWindow.ClassicShell.cs`: Menu/Toolbar/parameter strip;
- `MainWindow.ClassicStability.cs`: stable layout, Esc cleanup, live parameter refresh;
- `MainWindow.Actions.cs`: Action and shortcut entry points;
- `MainWindow.Events.cs`: Workspace event projection;
- `MainWindow.Files.cs`: New/Open/Save;
- `MainWindow.Settings.cs`: settings application;
- `MainWindow.OperationStatus.cs` / `CommandStatus.cs`: operation feedback.

These files split UI responsibility only. They are not additional business-state owners.

## 16. Pointer-move data flow

Typical drafting path:

```text
OcctAvaloniaViewport.PreviewPointerInput
→ CadPointerMoveScheduler
→ CadViewportInteractionController.ProcessPointer
→ CadToolManager.HandlePointer
→ active CadTool
→ CadWorkspace.ResolvePoint
   ├─ WorkPlane
   ├─ Snap
   ├─ Drafting / Tracking
   └─ Precision context
→ CadPreviewManager
→ OcctNet Viewer presentation
```

Performance optimization belongs in pointer scheduling, snap caching, replacement preview, and redraw batching. Model truth must not be moved into the UI to gain speed.

## 17. Property-edit data flow

```text
Property editor
→ Descriptor convert/validate
→ CadPropertyTransaction / ApplyEntities
→ Entity mutation
→ Document strict propagation
→ presentation rebuild/update
→ History install
→ public Changed notification
→ Properties/Model/Layer UI refresh
```

A strict presentation failure before History installation rolls back Entity state. A post-state UI observer failure does not invalidate the formal mutation.

## 18. Undo/Redo data flow

UI does not call `CadHistory.Undo/Redo` directly. It calls:

```text
CadWorkspace.Undo / Redo
→ cancel active Tool
→ clear Selection/Subobject/Preselection/Grip
→ Document change set
→ CadHistory Undo/Redo
→ presentation/state notifications
```

This prevents history changes from coexisting with an active Tool or stale Grip transient state.

## 19. Engine lifecycle

### Attach

After engine initialization:

- Document builds formal presentation;
- Selection/Snap/Grip managers attach to the engine;
- viewport interaction synchronizes automatic highlighting;
- Workspace transient presentation can be rebuilt from formal state.

### Engine recreation

Old native handles cannot remain valid across engine recreation. UI transient controllers discard old marker handles and derive required presentation again from formal Core state.

### Dispose

Workspace/UI teardown must:

- cancel the Tool;
- clear transient state;
- remove/release native presentation;
- unsubscribe events;
- log recoverable cleanup failures without aborting the rest of teardown.

## 20. Extension decision checklist

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

## 21. Feature placement examples

### New drawable Entity

```text
Entity geometry/state
→ Registry persistence
→ Snap/Grip semantics
→ Tool
→ Action registration
→ Menu/Toolbar when product-facing
→ Property descriptors
→ localization
→ save/open regression
```

### New modify command

```text
Define selection contract
→ preview strategy
→ formal mutation API
→ atomic history
→ Tool lifecycle
→ Action
→ UI entry
→ cancel/undo/native cleanup regression
```

### New UI panel

A panel may only observe/call existing Core owners. If it needs to maintain formal CAD state itself, the responsibility belongs in Core first.

## 22. ADR-required changes

Create an ADR for changes that:

- add projects/assemblies or alter dependency direction;
- replace transaction/history semantics;
- change Tool lifecycle or command-session semantics;
- change Document/Entity ownership;
- introduce global services, plugin containers, threading, or native async scheduling;
- change persistence compatibility strategy.

Use `docs/adr/0000-template.md`.

## 23. Initial-release architecture boundary

The initial release deliberately does not add:

- a plugin container;
- a third business assembly;
- a second command system;
- parallel ViewModel domain state;
- UI-only Copy/Rotate/Scale/Mirror buttons without complete Tool lifecycle;
- Import/Export as a prerequisite for the core editing loop.

Post-release features should continue to evolve within the current ownership, transaction, transient, and history contracts.
