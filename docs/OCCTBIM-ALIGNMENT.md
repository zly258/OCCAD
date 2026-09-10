# OCCAD / OCCTBIM-Source architecture alignment

Reference baseline: `zly258/OCCTBIM-Source` branch `release-1.0`.

This document is the architectural contract for the OCCAD refactor. The goal is semantic alignment with the reference CAD core, not a file-for-file C++ port and not preservation of the previous Avalonia UI structure.

## 1. Refactor order

1. Remove the existing UI implementation and keep only a bootable empty Avalonia shell.
2. Stabilize the core object model and ownership boundaries.
3. Align Entity, Property, Layer, Tool/Interaction, Grip, Snap, WorkPlane, Settings and Action/Transaction semantics.
4. Validate the core without UI-specific behavior.
5. Rebuild UI as a thin presentation adapter over the aligned core.

UI parity is intentionally deferred until the core contracts are stable.

## 2. Canonical ownership

### Document

`CadDocument` is the aggregate root for persistent drawing state. It owns entity membership, identity lookup, presentation synchronization and committed change publication. UI code must never mutate entity collections directly.

### Entity

`CadEntity` is the canonical owner of persistent entity data and geometry semantics. It owns identity, layer reference, appearance overrides, geometry, placement, property exposure, grip definitions and snap definitions. Viewer presentation is a derived representation of entity state.

The reference contract represented by `Entity` is preserved: shape/presentation, layer assignment, appearance, properties, grip points, snap points, edit-copy/new-copy semantics and modification notification all belong to the entity/domain boundary.

### Property

Properties are metadata over canonical entity/layer values; they are not a second state store. Descriptors define name, display metadata, grouping, editor semantic, ordering, read-only state and optional editor constraints. Property mutation must enter through a transaction boundary so multi-selection edits are atomic and undoable.

No Avalonia control, panel or view model may own authoritative CAD property values.

### Layer

Layer management is a domain service. A layer has stable identity, mutable display name, color, line type/width and visibility/state. The default layer is stable and undeletable. Entity-to-layer references must converge on stable layer identity rather than UI display text.

The layer manager is responsible for lookup, current layer, add/remove/update, uniqueness rules, deletion guards and propagation of ByLayer appearance.

### Interaction and Tools

`Interaction/` contains the interaction framework only: tool lifecycle, input resolution, preview/transient state, tracking, precision input and manager/registry contracts.

Concrete commands live in `Tools/`. A tool is a state machine with one lifecycle:

`Activate -> Input -> Preview -> Commit/Finish | Cancel -> Deactivate`

No tool owns persistent document state outside a committed transaction. Switching/cancelling a tool must clear every transient object it created.

### Grip

Grip points are entity-derived data (`entityId + local index + position/constraint`). `CadGripManager` manages visible/active grip state; `GripEditTool` performs the edit as a normal transaction-backed interaction. Grip editing is not a second editing architecture.

### Snap

Snap points are entity-derived data. `CadSnapManager` owns candidate collection/ranking and enhanced geometric/BRep snapping. Snap results are transient and deterministic. The viewport/UI only renders the chosen snap marker and never computes CAD snap semantics.

### WorkPlane

The active work plane is a core interaction service. Point resolution, snapping, tracking and tools consume the same work-plane state. Camera orientation is not drawing-plane state.

### Settings

`Settings/` owns UI-independent application/drafting settings. `CadSettingsStore` provides key/value persistence semantics corresponding to the reference `SettingsManager`; typed drafting settings remain core data. UI dialogs will only bind to this store after UI reconstruction.

### Action / Transaction / History

Actions are command entry points. Tools and property/layer changes commit through transaction/history boundaries. One logical user operation produces one atomic history record. Preview must never become history.

## 3. Physical project layout

```text
OCCAD.Core/
  Actions/          command boundary
  Document/         document and persistence
  Entities/         entity base, concrete entities, entity-derived grip/snap data
  Geometry/         pure geometry algorithms
  Layers/           layer domain and manager
  Properties/       property metadata/value conversion
  Selection/        selection/subobject state
  Grips/            grip session manager
  Snapping/         snap candidate engine/geometry
  WorkPlane/        drawing plane service
  Interaction/      lifecycle/input/preview/tracking infrastructure
  Tools/            concrete interactive commands
  Settings/         application/drafting settings
  History/          undo/redo records
```

`OCCAD.Avalonia` is temporarily reduced to `Program + empty MainWindow + manifest`. It must not regain CAD behavior until the core alignment checklist below is complete.

## 4. Alignment checklist

- [ ] Entity layer reference uses stable layer ID throughout document/persistence/tools.
- [ ] Entity property contract no longer depends on UI implementation details.
- [ ] Layer manager maps stable ID and name separately and has deletion guards for referenced/current/default layers.
- [ ] Tool lifecycle has one owner and one transient cleanup path.
- [ ] Preview scene has explicit ownership and zero residual objects after commit/cancel/switch.
- [ ] Grip points are entity-derived and grip drag commits atomically.
- [ ] Snap points/candidates are entity-derived; tolerance is screen-to-world deterministic.
- [ ] Window/Crossing, preselection and subobject selection are coordinated by the same interaction session.
- [ ] WorkPlane is independent of camera and shared by input/snap/tools.
- [ ] Settings are core-owned and persistence is UI-independent.
- [ ] Core compiles/tests without Avalonia UI behavior.
- [ ] Only after all above pass: rebuild compact industrial UI from the core contracts.

## 5. Explicit deletion rule

During this refactor, duplicated mechanisms are removed rather than adapted. A UI controller, helper, setting, preview path, snap path or grip path that duplicates a core responsibility must be deleted. Compatibility shims are allowed only at persistence/API boundaries and must be temporary and documented.
