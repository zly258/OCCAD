# 09 Extension Guide

## 1. Principle

Extend OCCAD through existing owners, registries, and lifecycles instead of creating parallel subsystems. Read these first:

- [04 Architecture](04-ARCHITECTURE.md)
- [05 Entity and Tool Contract](05-ENTITY-TOOL-CONTRACT.md)
- [10 Transaction, Event, and Native Resource Contracts](10-TRANSACTION-RESOURCE-CONTRACT.md)
- [14 Initial Release Feature Matrix](14-FEATURE-MATRIX.md)

A product feature should form this chain:

```text
Entity / Geometry
  ↓
Registry
  ↓
Tool / Action
  ↓
Preview / Commit / Cancel
  ↓
Property / Grip / Selection / Snap
  ↓
History / Persistence / Presentation
  ↓
Toolbar / Panel Adapter
  ↓
Local Build + Manual/Native Validation
```

Do not start an extension by adding a button.

## 2. Adding an Entity

A new Entity defines:

- geometry model;
- Placement versus intrinsic Geometry boundary;
- duplicate and restore/snapshot semantics;
- native presentation;
- snap points/curves;
- grips;
- persistence;
- PropertyGrid exposure;
- a stable Entity Registry ID when persisted.

Standard path:

1. add the Entity under `src/OCCAD.Core/Entities`;
2. implement input validation and geometry invariants;
3. implement duplicate/snapshot/restore;
4. implement native presentation;
5. implement Snap/Grip semantics;
6. register it through `CadCoreRegistration` / `CadEntityRegistry`;
7. add geometry read/write when persistent;
8. validate create → property → grip → save/open → history/presentation behavior.

Entity must not reference Avalonia, own History, own formal Selection, or mutate the formal Document model from pointer movement.

## 3. Adding a drawing Tool

Tool is an explicit staged state machine.

Standard path:

1. define a stable Tool ID;
2. define each stage's `InputKind`, prompt, pointer/parameter/precision semantics;
3. make parameter/precision input and pointer input share the same geometry-solving path;
4. keep Preview transient and duplicate-based;
5. validate geometry before Commit;
6. use the Workspace/Transaction formal create path;
7. keep Tool completion, History installation, and rollback within the common atomic contract;
8. make StepBack reverse one semantic step only;
9. make Cancel/Deactivate clear ToolPreview/Snap/Tracking/GripDrag ownership;
10. return the workspace to neutral.

Do not add Preview entities to Document, create History on pointer movement, keep Tool geometry state in an Avalonia controller, or duplicate the same geometry algorithm for several UI entries.

Circle/Arc/Ellipse/RegularPolygon show the preferred variant pattern: several Actions initialize different parameters on one Tool.

## 4. Adding a modify Tool

Recommended pattern for Move/Rotate/Scale/Mirror/Grip-style edits:

```text
Capture source state
  ↓
Create duplicate preview
  ↓
Pointer/parameter updates preview only
  ↓
Commit formal entities atomically
  ↓
Complete Tool
  ↓
Install one Undo entry
```

Failure before completion/history installation must roll model state back.

Generic Modify Tools are currently outside the registered initial product surface. Restoring them requires a complete interaction regression, not merely restoring historical toolbar buttons.

## 5. Adding an Action

Action is a stable stateless command entry point.

1. define a stable Action ID;
2. register it with `CadActionManager` / `CadCoreRegistration`;
3. route it to an existing Workspace/Core API or Tool activation;
4. let Toolbar/Menu/shortcuts reference only the Action ID;
5. drive availability from `CanExecute()`.

Do not use reflection-based string dispatch and do not use localized display text as a business ID.

## 6. Adding a property

Define:

- type/value semantic;
- multi-selection behavior;
- Mixed Value behavior;
- Geometry / Appearance / Metadata impact;
- no-op semantics;
- ByLayer semantics when applicable;
- Grip/Presentation refresh requirements;
- read-only Measurement semantics when applicable.

Property edits use `CadPropertyTransaction` / `CadTransaction.ApplyEntities` or another Core transaction path. PropertyGrid controllers do not implement a parallel Undo mechanism.

For dynamically injected descriptors such as planar `Normal`, honor TypeDescriptor nullable contracts, use stable initialization, and perform a real Entity mutation rather than exposing display-only fields.

## 7. Adding Snap semantics

Entity declares deterministic entity-specific snap geometry. `CadSnapManager` owns:

- candidate collection;
- priority;
- screen/depth ordering;
- hysteresis;
- cycling;
- temporary modes;
- marker state;
- cache/invalidation.

A new snap type defines runtime mode membership, priority group, geometry source, WorkPlane/reference-point behavior, marker representation, and cache invalidation.

Do not scatter Entity-specific snap geometry into Viewport/UI code.

## 8. Adding Grip semantics

Entity defines grip Kind, world position, and edit geometry semantics. `CadGripManager` owns marker/hot/hit/native ownership. Grip Tool owns preview/commit.

At minimum validate initial position, drag preview, invalid-target no-commit behavior, one logical history operation for a valid edit, Undo/Redo restoration, and marker/GripDrag cleanup.

## 9. Adding Selection behavior

Formal selection belongs to Core. UI input adapts into:

- Replace
- Add
- Remove
- Toggle
- Window
- Crossing
- Entity/Subobject scope

New filters must account for viewer synchronization, existing-selection validity, Grip visibility, and Preselection validity.

## 10. Adding transient presentation

Every temporary native object defines:

- channel;
- Tool/Workspace lifetime;
- owner;
- `HasState`;
- `Clear`;
- engine-recreation behavior;
- deletion-failure strategy.

Prefer registration with `CadTransientScene`.

On recoverable native deletion failure, do not forget the handle. Prefer hidden + non-selectable and retain ownership for later retry.

## 11. Adding an Avalonia Panel, Dialog, or Toolbar entry

UI extensions remain adapters:

- input → Core API;
- Core notification → UI refresh;
- no parallel business state.

Panels observe Core state rather than treating raw viewer state as authoritative. A Toolbar entry is added only after Action/Tool registration. The initial shell uses grouped three-row text buttons and does not require command-family duplication or functional icons.

## 12. Adding localization text

- IDs remain stable language-neutral identifiers;
- `zh-CN` / `en-US` affect display text only;
- Tool prompts, property labels, and action captions use localization keys;
- language resources remain synchronized;
- switching language never changes model/history/persistence data.

## 13. Persistence extensions

Define schema compatibility, defaults for missing values, invalid-value behavior, stable Entity Registry IDs, geometry/presentation separation, and whether a Feature Entity requires Document context. Breaking schema changes require an ADR.

## 14. Adding an exchange format

Do not add only a file-picker extension.

An exchange format must define:

1. Import, Export, or both;
2. Bridge/OCCT API ownership;
3. failure and unsupported-entity semantics;
4. how imported state enters formal Document ownership;
5. whether export operates on Selection or the full Document;
6. native/runtime dependencies;
7. manual validation samples.

## 15. Extension validation

The repository does not treat GitHub Actions or an automatic smoke framework as the authoritative acceptance path. At minimum run:

```powershell
.\build.ps1
```

and perform local Windows manual/native validation appropriate to the feature.

Check Create/Preview/Commit/Cancel, parameter/precision versus pointer paths, transient cleanup, Property/Grip, Undo/Redo, Save/Open, language switching, and native presentation versus authoritative model state.

## 16. Definition of done

An extension is complete only when:

- Core ownership is explicit;
- Registry/ID semantics are explicit;
- UI has no parallel business logic;
- parameter/precision and pointer geometry paths agree;
- Preview never contaminates Document/History;
- Cancel returns neutral;
- one user commit produces one logical history operation;
- Undo/Redo are symmetric;
- observer failure cannot invalidate authoritative state;
- native ownership is explainable;
- persistence/schema behavior is defined;
- local build and manual/native validation are complete;
- Chinese and English documentation are updated together.
